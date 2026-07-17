using SistemaX.Modules.Fiscal.Application.Ports;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Fiscal.Tests.Contracts;

/// <summary>Contract test do port <see cref="IFormaPagamentoDocumentoFiscalRepository"/> (gap #3,
/// emissao-mapping.md §4.5/§11) — roda 2× (InMemory + SQLite).</summary>
public abstract class FormaPagamentoDocumentoFiscalRepositoryContractTests
{
    protected const string DocA = "documento-a";
    protected const string DocB = "documento-b";

    protected abstract IFormaPagamentoDocumentoFiscalRepository CriarRepositorio();

    [Fact]
    public async Task Obter_de_documento_sem_pagamentos_retorna_lista_vazia()
    {
        var repo = CriarRepositorio();
        var pagamentos = await repo.ObterPorDocumentoAsync(DocA);
        Assert.Empty(pagamentos);
    }

    [Fact]
    public async Task Vincular_e_obter_retorna_os_mesmos_pagamentos_na_ordem()
    {
        var repo = CriarRepositorio();
        IReadOnlyList<FormaPagamentoParaEmitir> pagamentos =
        [
            new FormaPagamentoParaEmitir("dinheiro", Money.DeReais(30)),
            new FormaPagamentoParaEmitir("cartao_credito", Money.DeReais(70)),
        ];

        await repo.VincularAsync(DocA, pagamentos);
        var lidos = await repo.ObterPorDocumentoAsync(DocA);

        Assert.Equal(2, lidos.Count);
        Assert.Equal("dinheiro", lidos[0].Metodo);
        Assert.Equal(Money.DeReais(30), lidos[0].Valor);
        Assert.Equal("cartao_credito", lidos[1].Metodo);
        Assert.Equal(Money.DeReais(70), lidos[1].Valor);
    }

    [Fact]
    public async Task Vincular_novamente_o_mesmo_documento_substitui_a_lista_anterior()
    {
        var repo = CriarRepositorio();
        await repo.VincularAsync(DocA, [new FormaPagamentoParaEmitir("dinheiro", Money.DeReais(100))]);
        await repo.VincularAsync(DocA, [new FormaPagamentoParaEmitir("pix", Money.DeReais(50)), new FormaPagamentoParaEmitir("cartao_debito", Money.DeReais(50))]);

        var lidos = await repo.ObterPorDocumentoAsync(DocA);
        Assert.Equal(2, lidos.Count);
        Assert.Equal("pix", lidos[0].Metodo);
        Assert.Equal("cartao_debito", lidos[1].Metodo);
    }

    [Fact]
    public async Task Documentos_diferentes_tem_pagamentos_independentes()
    {
        var repo = CriarRepositorio();
        await repo.VincularAsync(DocA, [new FormaPagamentoParaEmitir("dinheiro", Money.DeReais(10))]);
        await repo.VincularAsync(DocB, [new FormaPagamentoParaEmitir("pix", Money.DeReais(20))]);

        Assert.Equal("dinheiro", (await repo.ObterPorDocumentoAsync(DocA))[0].Metodo);
        Assert.Equal("pix", (await repo.ObterPorDocumentoAsync(DocB))[0].Metodo);
    }
}
