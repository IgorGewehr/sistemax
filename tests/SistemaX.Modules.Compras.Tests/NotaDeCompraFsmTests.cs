using SistemaX.Modules.Compras.Domain.Notas;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Compras.Tests;

/// <summary>
/// FSM de <see cref="NotaDeCompra"/> (plano §3.2) e as invariantes validadas em
/// <see cref="NotaDeCompra.ConfirmarRecebimento"/> (plano §3.3).
/// </summary>
public class NotaDeCompraFsmTests
{
    [Fact]
    public void Importar_SemItens_Falha()
    {
        var totais = TotaisDaNota.Criar(10_000, 10_000).Valor;

        var resultado = NotaDeCompra.Importar(
            ComprasTestBuilder.TenantId, ComprasTestBuilder.LojaId, OrigemNota.Manual, "1", "1", DateTimeOffset.UtcNow, totais, []);

        Assert.True(resultado.Falha);
        Assert.Equal("compras.nota.sem_itens", resultado.Erro.Codigo);
    }

    [Fact]
    public void Importar_OrigemXmlSemChaveDeAcesso_Falha()
    {
        var totais = TotaisDaNota.Criar(10_000, 10_000).Valor;
        var itens = new[] { ComprasTestBuilder.Item(1, 10_000) };

        var resultado = NotaDeCompra.Importar(
            ComprasTestBuilder.TenantId, ComprasTestBuilder.LojaId, OrigemNota.XmlUpload, "1", "1", DateTimeOffset.UtcNow, totais, itens);

        Assert.True(resultado.Falha);
        Assert.Equal("compras.nota.chave_obrigatoria", resultado.Erro.Codigo);
    }

    [Fact]
    public void Importar_OrigemManual_DispensaChaveDeAcesso()
    {
        var totais = TotaisDaNota.Criar(10_000, 10_000).Valor;
        var itens = new[] { ComprasTestBuilder.Item(1, 10_000) };

        var resultado = NotaDeCompra.Importar(
            ComprasTestBuilder.TenantId, ComprasTestBuilder.LojaId, OrigemNota.Manual, "1", "1", DateTimeOffset.UtcNow, totais, itens);

        Assert.True(resultado.Sucesso);
        Assert.Equal(StatusNotaDeCompra.Importada, resultado.Valor.Status);
        Assert.Null(resultado.Valor.ChaveDeAcesso);
    }

    [Fact]
    public void AbrirConferencia_DeImportada_TransicionaComSucesso()
    {
        var totais = TotaisDaNota.Criar(10_000, 10_000).Valor;
        var nota = NotaDeCompra.Importar(
            ComprasTestBuilder.TenantId, ComprasTestBuilder.LojaId, OrigemNota.Manual, "1", "1",
            DateTimeOffset.UtcNow, totais, [ComprasTestBuilder.Item(1, 10_000)]).Valor;

        var resultado = nota.AbrirConferencia();

        Assert.True(resultado.Sucesso);
        Assert.Equal(StatusNotaDeCompra.EmConferencia, nota.Status);
    }

    [Fact]
    public void AbrirConferencia_Novamente_Falha()
    {
        var nota = ComprasTestBuilder.NotaEmConferencia();

        var resultado = nota.AbrirConferencia();

        Assert.True(resultado.Falha);
        Assert.Equal("fsm.transicao_invalida", resultado.Erro.Codigo);
    }

    [Fact]
    public void ConfirmarRecebimento_SemFornecedorVinculado_Falha()
    {
        var nota = ComprasTestBuilder.NotaEmConferencia(fornecedorId: null);

        var resultado = nota.ConfirmarRecebimento("user-1", "Operador", DateTimeOffset.UtcNow);

        Assert.True(resultado.Falha);
        Assert.Equal("compras.nota.sem_fornecedor", resultado.Erro.Codigo);
    }

    [Fact]
    public void ConfirmarRecebimento_ComItemSemMatch_Falha()
    {
        var totais = TotaisDaNota.Criar(10_000, 10_000).Valor;
        var itemSemMatch = ComprasTestBuilder.Item(1, 10_000, produtoId: null, fatorMilesimos: null, matchState: MatchState.SemMatch);
        var nota = NotaDeCompra.Importar(
            ComprasTestBuilder.TenantId, ComprasTestBuilder.LojaId, OrigemNota.Manual, "1", "1",
            DateTimeOffset.UtcNow, totais, [itemSemMatch], ComprasTestBuilder.FornecedorId).Valor;
        nota.AbrirConferencia();

        var resultado = nota.ConfirmarRecebimento("user-1", "Operador", DateTimeOffset.UtcNow);

        Assert.True(resultado.Falha);
        Assert.Equal("compras.item.match_pendente", resultado.Erro.Codigo);
    }

    [Fact]
    public void ConfirmarRecebimento_ComItemSemFatorDeConversao_Falha()
    {
        var totais = TotaisDaNota.Criar(10_000, 10_000).Valor;
        var itemSemFator = ComprasTestBuilder.Item(1, 10_000, fatorMilesimos: null, matchState: MatchState.Manual);
        var nota = NotaDeCompra.Importar(
            ComprasTestBuilder.TenantId, ComprasTestBuilder.LojaId, OrigemNota.Manual, "1", "1",
            DateTimeOffset.UtcNow, totais, [itemSemFator], ComprasTestBuilder.FornecedorId).Valor;
        nota.AbrirConferencia();

        var resultado = nota.ConfirmarRecebimento("user-1", "Operador", DateTimeOffset.UtcNow);

        Assert.True(resultado.Falha);
        Assert.Equal("compras.item.fator_ausente", resultado.Erro.Codigo);
    }

    [Fact]
    public void ConfirmarRecebimento_Sucesso_CongelaCustoDeEntradaETransicionaParaRecebida()
    {
        var nota = ComprasTestBuilder.NotaEmConferencia(vProd1Centavos: 10_000, vProd2Centavos: 10_000, vFreteCentavos: 1_000);

        var resultado = nota.ConfirmarRecebimento("user-1", "Operador", DateTimeOffset.UtcNow);

        Assert.True(resultado.Sucesso);
        Assert.Equal(StatusNotaDeCompra.Recebida, nota.Status);
        Assert.Equal("user-1", nota.RecebidaPorId);
        Assert.Equal("Operador", nota.RecebidaPorNome);
        Assert.NotNull(nota.RecebidaEm);

        var somaCongelada = nota.Itens.Aggregate(Money.Zero, (acc, i) => acc + i.CustoTotalEntrada!.Value);
        Assert.Equal(nota.Totais.VNf, somaCongelada); // Σ CustoTotalEntrada == vNF (invariante 1)
    }

    [Fact]
    public void ConfirmarRecebimento_LevantaDomainEventComItensParaEstoque()
    {
        var nota = ComprasTestBuilder.NotaEmConferencia();

        nota.ConfirmarRecebimento("user-1", "Operador", DateTimeOffset.UtcNow);

        var evento = Assert.Single(nota.DomainEvents.OfType<NotaDeCompraRecebidaDomainEvent>());
        Assert.Equal(nota.Id, evento.CompraId);
        Assert.Equal(2, evento.Itens.Count);
        Assert.Contains(evento.Itens, i => i.ProdutoId == "produto-1");
        Assert.Contains(evento.Itens, i => i.ProdutoId == "produto-2");
    }

    [Fact]
    public void ConfirmarRecebimento_ComItemIgnorado_ExcluiDoDomainEventMasContinuaContabilizandoNoRateio()
    {
        var totais = TotaisDaNota.Criar(20_000, 20_000).Valor;
        var itens = new[] { ComprasTestBuilder.Item(1, 10_000, "produto-1"), ComprasTestBuilder.Item(2, 10_000, "produto-2") };
        var nota = NotaDeCompra.Importar(
            ComprasTestBuilder.TenantId, ComprasTestBuilder.LojaId, OrigemNota.Manual, "1", "1",
            DateTimeOffset.UtcNow, totais, itens, ComprasTestBuilder.FornecedorId).Valor;
        nota.AbrirConferencia();
        nota.IgnorarItem(2, "Brinde do fornecedor — não vira estoque");

        var resultado = nota.ConfirmarRecebimento("user-1", "Operador", DateTimeOffset.UtcNow);

        Assert.True(resultado.Sucesso);
        var evento = nota.DomainEvents.OfType<NotaDeCompraRecebidaDomainEvent>().Single();
        Assert.Single(evento.Itens); // só o item 1 vai para o Estoque
        Assert.Equal("produto-1", evento.Itens[0].ProdutoId);

        // mas o rateio ainda usou os 20000 inteiros — item 2 tem custo congelado também (auditoria)
        var item2 = nota.Itens.Single(i => i.NItem == 2);
        Assert.NotNull(item2.CustoTotalEntrada);
    }

    [Fact]
    public void ConfirmarRecebimento_DeNotaJaRecebida_Falha()
    {
        var nota = ComprasTestBuilder.NotaEmConferencia();
        nota.ConfirmarRecebimento("user-1", "Operador", DateTimeOffset.UtcNow);

        var resultado = nota.ConfirmarRecebimento("user-1", "Operador", DateTimeOffset.UtcNow);

        Assert.True(resultado.Falha);
        Assert.Equal("fsm.transicao_invalida", resultado.Erro.Codigo);
    }

    [Fact]
    public void Estornar_DeRecebida_VoltaParaEmConferenciaELevantaDomainEvent()
    {
        var nota = ComprasTestBuilder.NotaEmConferencia();
        nota.ConfirmarRecebimento("user-1", "Operador", DateTimeOffset.UtcNow);
        nota.ClearDomainEvents();

        var resultado = nota.Estornar("user-2", "Supervisor", DateTimeOffset.UtcNow);

        Assert.True(resultado.Sucesso);
        Assert.Equal(StatusNotaDeCompra.EmConferencia, nota.Status);
        var evento = Assert.Single(nota.DomainEvents.OfType<NotaDeCompraEstornadaDomainEvent>());
        Assert.Equal(2, evento.Itens.Count);
    }

    [Fact]
    public void Estornar_DeEmConferencia_Falha()
    {
        var nota = ComprasTestBuilder.NotaEmConferencia();

        var resultado = nota.Estornar("user-1", "Operador", DateTimeOffset.UtcNow);

        Assert.True(resultado.Falha);
        Assert.Equal("fsm.transicao_invalida", resultado.Erro.Codigo);
    }

    [Fact]
    public void Descartar_DeImportada_TransicionaComSucessoEGuardaMotivo()
    {
        var totais = TotaisDaNota.Criar(10_000, 10_000).Valor;
        var nota = NotaDeCompra.Importar(
            ComprasTestBuilder.TenantId, ComprasTestBuilder.LojaId, OrigemNota.Manual, "1", "1",
            DateTimeOffset.UtcNow, totais, [ComprasTestBuilder.Item(1, 10_000)]).Valor;

        var resultado = nota.Descartar("Nota de saída própria classificada errado");

        Assert.True(resultado.Sucesso);
        Assert.Equal(StatusNotaDeCompra.Descartada, nota.Status);
        Assert.Equal("Nota de saída própria classificada errado", nota.MotivoDescarte);
    }

    [Fact]
    public void Descartar_DeRecebida_Falha()
    {
        var nota = ComprasTestBuilder.NotaEmConferencia();
        nota.ConfirmarRecebimento("user-1", "Operador", DateTimeOffset.UtcNow);

        var resultado = nota.Descartar("tentativa inválida");

        Assert.True(resultado.Falha);
        Assert.Equal("fsm.transicao_invalida", resultado.Erro.Codigo);
    }
}
