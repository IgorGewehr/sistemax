using SistemaX.Modules.Vendas.Application.Ports;
using SistemaX.Modules.Vendas.Domain;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Vendas.Tests.Contracts;

/// <summary>
/// Contract test do port <see cref="IVendaRepository"/> — roda EXATAMENTE o mesmo conjunto de
/// casos contra QUALQUER adapter (hoje: <c>InMemoryVendaRepository</c> e
/// <c>SqliteVendaRepository</c>). Um port só está "pronto para SQLite" quando os mesmos casos
/// passam nos dois adapters.
///
/// MOLDE: cópia de <c>FornecedorRepositoryContractTests</c> (F0) — os NOMES dos métodos de teste
/// documentam o CONTRATO do port, não a implementação de um adapter específico. Fixtures são
/// montadas via <see cref="Venda.Abrir"/>/<c>AdicionarItem</c>/<c>RegistrarPagamento</c>/
/// <c>AplicarDescontoVenda</c>/<c>Concluir</c> — nunca via <c>Reconstituir</c>, que é
/// repositório-interno.
/// </summary>
public abstract class VendaRepositoryContractTests
{
    protected const string TenantA = "loja-a";

    /// <summary>Cada subclasse decide o adapter (e, no caso do SQLite, prepara um banco/arquivo
    /// isolado por teste — xUnit cria uma instância nova da classe de teste por [Fact]).</summary>
    protected abstract IVendaRepository CriarRepositorio();

    [Fact]
    public async Task Salvar_e_buscar_por_id_retorna_a_mesma_venda()
    {
        var repo = CriarRepositorio();
        var venda = Venda.Abrir(TenantA);
        Assert.True(venda.AdicionarItem("produto-1", "Item 1", 2, Money.DeReais(10)).Sucesso);
        Assert.True(venda.AdicionarItem("produto-2", "Item 2", 1, Money.DeReais(20)).Sucesso);
        Assert.True(venda.AplicarDescontoVenda(Money.DeReais(5), "Cliente fidelidade").Sucesso);
        Assert.True(venda.DefinirCliente("cliente-1").Sucesso);

        // Total = (2*10 + 20) - 5 = 35, pago em split: Pix 20 + Dinheiro 15 (com troco de 5).
        var registradoEm = DateTimeOffset.Parse("2026-01-15T10:30:00.1234567+00:00");
        Assert.True(venda.RegistrarPagamento(MetodoPagamento.Pix, Money.DeReais(20), null, registradoEm).Sucesso);
        Assert.True(venda.RegistrarPagamento(MetodoPagamento.Dinheiro, Money.DeReais(15), Money.DeReais(20), registradoEm).Sucesso);

        await repo.SalvarAsync(venda);
        var lida = await repo.ObterPorIdAsync(venda.Id);

        Assert.NotNull(lida);
        Assert.Equal(venda.Id, lida!.Id);
        Assert.Equal(venda.TenantId, lida.TenantId);
        Assert.Equal(venda.Status, lida.Status);
        Assert.Equal(venda.DescontoVenda, lida.DescontoVenda);
        Assert.Equal(venda.MotivoDescontoVenda, lida.MotivoDescontoVenda);
        Assert.Equal(venda.ClienteId, lida.ClienteId);
        Assert.Equal(venda.Total, lida.Total);
        Assert.Equal(venda.TotalPago, lida.TotalPago);

        Assert.Equal(venda.Itens.Count, lida.Itens.Count);
        foreach (var itemOriginal in venda.Itens)
        {
            var itemLido = lida.Itens.Single(i => i.Id == itemOriginal.Id);
            Assert.Equal(itemOriginal.ProdutoId, itemLido.ProdutoId);
            Assert.Equal(itemOriginal.Descricao, itemLido.Descricao);
            Assert.Equal(itemOriginal.Quantidade, itemLido.Quantidade);
            Assert.Equal(itemOriginal.PrecoUnitario, itemLido.PrecoUnitario);
            Assert.Equal(itemOriginal.Desconto, itemLido.Desconto);
        }

        Assert.Equal(venda.Pagamentos.Count, lida.Pagamentos.Count);
        foreach (var pagamentoOriginal in venda.Pagamentos)
        {
            var pagamentoLido = lida.Pagamentos.Single(p => p.Id == pagamentoOriginal.Id);
            Assert.Equal(pagamentoOriginal.Metodo, pagamentoLido.Metodo);
            Assert.Equal(pagamentoOriginal.Valor, pagamentoLido.Valor);
            Assert.Equal(pagamentoOriginal.ValorRecebido, pagamentoLido.ValorRecebido);
            Assert.Equal(pagamentoOriginal.RegistradoEm, pagamentoLido.RegistradoEm);
        }
    }

    [Fact]
    public async Task Buscar_por_id_inexistente_retorna_null()
    {
        var repo = CriarRepositorio();

        Assert.Null(await repo.ObterPorIdAsync("venda-que-nao-existe"));
    }

    [Fact]
    public async Task Salvar_de_novo_apos_concluir_reflete_o_novo_status()
    {
        var repo = CriarRepositorio();
        var venda = Venda.Abrir(TenantA);
        Assert.True(venda.AdicionarItem("produto-1", "Item 1", 1, Money.DeReais(50)).Sucesso);
        Assert.True(venda.RegistrarPagamento(MetodoPagamento.Pix, Money.DeReais(50), null, DateTimeOffset.UtcNow).Sucesso);

        await repo.SalvarAsync(venda);

        Assert.True(venda.Concluir().Sucesso);
        await repo.SalvarAsync(venda);

        var lida = await repo.ObterPorIdAsync(venda.Id);
        Assert.Equal(StatusVenda.Concluida, lida!.Status);
    }

    [Fact]
    public async Task Salvar_de_novo_apos_remover_item_nao_deixa_item_orfao()
    {
        var repo = CriarRepositorio();
        var venda = Venda.Abrir(TenantA);
        Assert.True(venda.AdicionarItem("produto-1", "Item 1", 1, Money.DeReais(10)).Sucesso);
        Assert.True(venda.AdicionarItem("produto-2", "Item 2", 1, Money.DeReais(20)).Sucesso);
        await repo.SalvarAsync(venda);

        var itemARemover = venda.Itens.First().Id;
        Assert.True(venda.RemoverItem(itemARemover).Sucesso);
        await repo.SalvarAsync(venda);

        var lida = await repo.ObterPorIdAsync(venda.Id);
        var itemRestante = Assert.Single(lida!.Itens);
        Assert.NotEqual(itemARemover, itemRestante.Id);
    }

    [Fact]
    public async Task Salvar_de_novo_apos_remover_pagamento_nao_deixa_pagamento_orfao()
    {
        var repo = CriarRepositorio();
        var venda = Venda.Abrir(TenantA);
        Assert.True(venda.AdicionarItem("produto-1", "Item 1", 1, Money.DeReais(100)).Sucesso);
        Assert.True(venda.RegistrarPagamento(MetodoPagamento.Pix, Money.DeReais(40), null, DateTimeOffset.UtcNow).Sucesso);
        Assert.True(venda.RegistrarPagamento(MetodoPagamento.Dinheiro, Money.DeReais(30), null, DateTimeOffset.UtcNow).Sucesso);
        await repo.SalvarAsync(venda);

        var pagamentoARemover = venda.Pagamentos.First().Id;
        Assert.True(venda.RemoverPagamento(pagamentoARemover).Sucesso);
        await repo.SalvarAsync(venda);

        var lida = await repo.ObterPorIdAsync(venda.Id);
        var pagamentoRestante = Assert.Single(lida!.Pagamentos);
        Assert.NotEqual(pagamentoARemover, pagamentoRestante.Id);
    }
}
