using SistemaX.Modules.Fiscal.Domain.Comum;
using SistemaX.Modules.Fiscal.Domain.Documentos;
using SistemaX.Modules.Fiscal.Domain.Ncm;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Fiscal.Tests;

/// <summary>R4/R8 do CLAUDE.md: toda FSM tem teste de transição válida E inválida, verificando o
/// código de erro (não só "deu erro").</summary>
public class DocumentoFiscalTests
{
    private static ItemDocumentoFiscal ItemComIcms() => new(
        "produto-1", "Produto 1", "12345678", null, OrigemMercadoria.Nacional, "5102",
        new Quantidade(1000), new Money(1000), Money.Zero,
        [new TributoResolvidoItem(TipoTributo.Icms, "102", new Money(1000), new Percentual(180_000), new Money(180))]);

    [Fact]
    public void AdicionarItemResolvido_SemIcmsNoBag_Falha()
    {
        var doc = DocumentoFiscal.Abrir("tenant-1", TipoDocumentoFiscal.NFCe, new SourceRef("vendas", "v1"));
        var itemSemIcms = ItemComIcms() with { Tributos = [] };

        var resultado = doc.AdicionarItemResolvido(itemSemIcms);

        Assert.True(resultado.Falha);
        Assert.Equal("fiscal.item.icms_nao_resolvido", resultado.Erro.Codigo);
    }

    [Fact]
    public void AlocarNumero_SemItens_Falha()
    {
        var doc = DocumentoFiscal.Abrir("tenant-1", TipoDocumentoFiscal.NFCe, new SourceRef("vendas", "v2"));

        var resultado = doc.AlocarNumero("1", 1);

        Assert.True(resultado.Falha);
        Assert.Equal("fiscal.documento.sem_itens", resultado.Erro.Codigo);
    }

    [Fact]
    public void FluxoFeliz_RascunhoParaNumeroAlocado()
    {
        var doc = DocumentoFiscal.Abrir("tenant-1", TipoDocumentoFiscal.NFCe, new SourceRef("vendas", "v3"));
        Assert.True(doc.AdicionarItemResolvido(ItemComIcms()).Sucesso);

        var resultado = doc.AlocarNumero("1", 42);

        Assert.True(resultado.Sucesso);
        Assert.Equal(StatusDocumentoFiscal.NumeroAlocado, doc.Status);
        Assert.Equal(42, doc.Numero);
        Assert.Contains(doc.DomainEvents, e => e is NumeroFiscalAlocadoDomainEvent);
    }

    [Fact]
    public void AlocarNumero_DepoisDeJaAlocado_FalhaPorTransicaoInvalida()
    {
        var doc = DocumentoFiscal.Abrir("tenant-1", TipoDocumentoFiscal.NFCe, new SourceRef("vendas", "v4"));
        doc.AdicionarItemResolvido(ItemComIcms());
        doc.AlocarNumero("1", 1);

        var resultado = doc.AlocarNumero("1", 2);

        Assert.True(resultado.Falha);
        Assert.Equal("fsm.transicao_invalida", resultado.Erro.Codigo);
    }

    [Fact]
    public void Cancelar_DocumentoNaoAutorizado_FalhaPorTransicaoInvalida()
    {
        var doc = DocumentoFiscal.Abrir("tenant-1", TipoDocumentoFiscal.NFCe, new SourceRef("vendas", "v5"));

        var resultado = doc.Cancelar("Cliente desistiu da compra");

        Assert.True(resultado.Falha);
        Assert.Equal("fsm.transicao_invalida", resultado.Erro.Codigo);
    }

    [Fact]
    public void Cancelar_ComJustificativaCurta_Falha()
    {
        var doc = DocumentoFiscal.Abrir("tenant-1", TipoDocumentoFiscal.NFCe, new SourceRef("vendas", "v6"));
        doc.AdicionarItemResolvido(ItemComIcms());
        doc.AlocarNumero("1", 1);
        doc.RegistrarAutorizacao("chave-123", "protocolo-123", DateTimeOffset.UtcNow);

        var resultado = doc.Cancelar("curta");

        Assert.True(resultado.Falha);
        Assert.Equal("fiscal.documento.justificativa_curta", resultado.Erro.Codigo);
    }

    [Fact]
    public void Cancelar_DocumentoAutorizado_Sucesso()
    {
        var doc = DocumentoFiscal.Abrir("tenant-1", TipoDocumentoFiscal.NFCe, new SourceRef("vendas", "v7"));
        doc.AdicionarItemResolvido(ItemComIcms());
        doc.AlocarNumero("1", 1);
        doc.RegistrarAutorizacao("chave-123", "protocolo-123", DateTimeOffset.UtcNow);

        var resultado = doc.Cancelar("Cliente desistiu da compra hoje");

        Assert.True(resultado.Sucesso);
        Assert.Equal(StatusDocumentoFiscal.Cancelado, doc.Status);
    }

    [Fact]
    public void RegistrarAutorizacao_PersisteProtocoloJuntoComAChave()
    {
        var doc = DocumentoFiscal.Abrir("tenant-1", TipoDocumentoFiscal.NFCe, new SourceRef("vendas", "v10"));
        doc.AdicionarItemResolvido(ItemComIcms());
        doc.AlocarNumero("1", 1);

        var resultado = doc.RegistrarAutorizacao("chave-123", "protocolo-456", DateTimeOffset.UtcNow);

        Assert.True(resultado.Sucesso);
        Assert.Equal("protocolo-456", doc.Protocolo);
    }

    [Fact]
    public void RegistrarAutorizacao_SemProtocoloDoGateway_ProtocoloFicaNulo()
    {
        var doc = DocumentoFiscal.Abrir("tenant-1", TipoDocumentoFiscal.NFCe, new SourceRef("vendas", "v11"));
        doc.AdicionarItemResolvido(ItemComIcms());
        doc.AlocarNumero("1", 1);

        doc.RegistrarAutorizacao("chave-123", null, DateTimeOffset.UtcNow);

        Assert.Null(doc.Protocolo);
    }

    [Fact]
    public void Bloquear_EDesbloquear_VoltaParaRascunho()
    {
        var doc = DocumentoFiscal.Abrir("tenant-1", TipoDocumentoFiscal.NFCe, new SourceRef("vendas", "v8"));

        Assert.True(doc.Bloquear("NCM sem perfil fiscal cadastrado").Sucesso);
        Assert.Equal(StatusDocumentoFiscal.BloqueadoPorConfiguracaoFiscal, doc.Status);

        Assert.True(doc.Desbloquear().Sucesso);
        Assert.Equal(StatusDocumentoFiscal.Rascunho, doc.Status);
    }

    [Fact]
    public void Desistir_DeNumeroAlocado_VaiParaInutilizado()
    {
        var doc = DocumentoFiscal.Abrir("tenant-1", TipoDocumentoFiscal.NFCe, new SourceRef("vendas", "v9"));
        doc.AdicionarItemResolvido(ItemComIcms());
        doc.AlocarNumero("1", 1);

        var resultado = doc.Desistir("Venda cancelada antes da transmissão");

        Assert.True(resultado.Sucesso);
        Assert.Equal(StatusDocumentoFiscal.Inutilizado, doc.Status);
        Assert.Contains(doc.DomainEvents, e => e is NumeroFiscalInutilizadoDomainEvent);
    }
}
