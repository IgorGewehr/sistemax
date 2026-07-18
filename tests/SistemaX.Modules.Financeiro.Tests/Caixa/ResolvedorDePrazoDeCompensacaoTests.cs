using SistemaX.Modules.Financeiro.Application.Caixa;
using SistemaX.Modules.Financeiro.Domain.Caixa;
using SistemaX.Modules.Financeiro.Infrastructure.InMemory;

namespace SistemaX.Modules.Financeiro.Tests.Caixa;

/// <summary>
/// P2-7 (docs/financeiro/revisao-domain-fit-cnpj.md) — LAR único de à-vista/prazo de compensação:
/// resolve contra a <c>FormaDePagamento</c> cadastrada, com fallback conservador só quando não há
/// cadastro.
/// </summary>
public sealed class ResolvedorDePrazoDeCompensacaoTests
{
    private const string BusinessId = "business-1";

    [Fact]
    public async Task ResolverAsync_ComFormaCadastrada_UsaPrazoRealCadastradoNaoOFallback()
    {
        // Débito cadastrado com D+1 — o fallback antigo (ClassificadorFormaPagamento) diria D+30
        // pra qualquer forma que não seja dinheiro/pix. A forma cadastrada tem que vencer.
        var repositorio = new InMemoryFormaDePagamentoRepository();
        await repositorio.SalvarAsync(FormaDePagamento.Criar(BusinessId, "debito", TipoFormaPagamento.Debito, 0.0139m, 1).Valor);
        var resolvedor = new ResolvedorDePrazoDeCompensacao(repositorio);

        var (ehAVista, prazoDias) = await resolvedor.ResolverAsync(BusinessId, "debito");

        Assert.False(ehAVista);
        Assert.Equal(1, prazoDias); // não 30 — o prazo real da forma cadastrada
    }

    [Fact]
    public async Task ResolverAsync_ComFormaCadastradaPrazoZero_EhAVista()
    {
        var repositorio = new InMemoryFormaDePagamentoRepository();
        await repositorio.SalvarAsync(FormaDePagamento.Criar(BusinessId, "pix", TipoFormaPagamento.Pix, 0m, 0).Valor);
        var resolvedor = new ResolvedorDePrazoDeCompensacao(repositorio);

        var (ehAVista, prazoDias) = await resolvedor.ResolverAsync(BusinessId, "pix");

        Assert.True(ehAVista);
        Assert.Equal(0, prazoDias);
    }

    [Fact]
    public async Task ResolverAsync_SemFormaCadastrada_CaiNoFallbackConservador()
    {
        var resolvedor = new ResolvedorDePrazoDeCompensacao(new InMemoryFormaDePagamentoRepository());

        var (ehAVistaDinheiro, prazoDinheiro) = await resolvedor.ResolverAsync(BusinessId, "dinheiro");
        Assert.True(ehAVistaDinheiro);
        Assert.Equal(0, prazoDinheiro);

        var (ehAVistaCartao, prazoCartao) = await resolvedor.ResolverAsync(BusinessId, "cartao-desconhecido");
        Assert.False(ehAVistaCartao);
        Assert.Equal(ClassificadorFormaPagamento.PrazoPadraoDiasAPrazo, prazoCartao);
    }
}
