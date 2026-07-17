using Microsoft.Extensions.DependencyInjection;
using SistemaX.Modules.Financeiro.Application.Caixa;
using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Domain.Caixa;

namespace SistemaX.Modules.Financeiro.Infrastructure.Seed;

/// <summary>
/// Semente de bootstrap do domínio Bancário (docs/wiring/financeiro-telas-restantes.md §3) —
/// IDEMPOTENTE, roda em TODO boot (mesmo espírito de <c>IdentidadeBootstrapSeeder</c>): sem ela, a
/// tela Bancário fica sem NENHUMA conta/forma cadastrada e <c>FatoRecebiveisProjection</c> cai
/// sempre no fallback conservador (0%, D+0) por falta de <c>FormaDePagamento</c> pra resolver.
///
/// A conta-caixa padrão nasce com o MESMO id que <c>ClassificadorFormaPagamento.ContaCaixaPadraoId</c>
/// já usava como referência hardcoded em <c>MovimentoFinanceiro</c> (escrito pelos handlers de
/// <c>VendaConcluida</c>/<c>PedidoPago</c>) — sem esse pin, o saldo derivado dela nunca bateria com
/// o ledger já existente. As formas de pagamento nascem com os MESMOS números que a antiga
/// <c>ConfiguracaoDeRecebiveisOptions.PadraoDeMercado</c> hardcodava (removida nesta reconciliação —
/// <c>FormaDePagamento</c> é agora o LAR ÚNICO de MDR/lag).
/// </summary>
public static class FinanceiroBootstrapSeeder
{
    public static async Task SemearAsync(IServiceProvider provider, string businessId, CancellationToken ct = default)
    {
        await using var scope = provider.CreateAsyncScope();
        var sp = scope.ServiceProvider;

        var bancoPrincipalId = await SemearContasAsync(sp, businessId, ct).ConfigureAwait(false);
        await SemearFormasDePagamentoAsync(sp, businessId, bancoPrincipalId, ct).ConfigureAwait(false);
    }

    /// <summary>Cadastra as duas contas padrão e devolve o id de "Banco Principal" — usado como
    /// <c>ContaLiquidacaoId</c> das formas eletrônicas semeadas em seguida. Se as contas já
    /// existirem (idempotência), resolve o id da existente do tipo <c>ContaCorrente</c> em vez de
    /// recriar.</summary>
    private static async Task<string> SemearContasAsync(IServiceProvider sp, string businessId, CancellationToken ct)
    {
        var repo = sp.GetRequiredService<IContaBancariaCaixaRepository>();
        var existentes = await repo.ListarAsync(businessId, ct: ct).ConfigureAwait(false);
        if (existentes.Count > 0)
        {
            return existentes.FirstOrDefault(c => c.Tipo == TipoContaBancariaCaixa.ContaCorrente)?.Id
                ?? ClassificadorFormaPagamento.ContaCaixaPadraoId;
        }

        var caixa = ContaBancariaCaixa.Criar(
            businessId, "Caixa", TipoContaBancariaCaixa.CaixaFisico,
            id: ClassificadorFormaPagamento.ContaCaixaPadraoId).Valor;
        await repo.SalvarAsync(caixa, ct).ConfigureAwait(false);

        var banco = ContaBancariaCaixa.Criar(businessId, "Banco Principal", TipoContaBancariaCaixa.ContaCorrente).Valor;
        await repo.SalvarAsync(banco, ct).ConfigureAwait(false);

        return banco.Id;
    }

    private static async Task SemearFormasDePagamentoAsync(IServiceProvider sp, string businessId, string contaBancoId, CancellationToken ct)
    {
        var repo = sp.GetRequiredService<IFormaDePagamentoRepository>();
        if ((await repo.ListarAsync(businessId, ct: ct).ConfigureAwait(false)).Count > 0)
        {
            return;
        }

        async Task NovaAsync(string nome, TipoFormaPagamento tipo, decimal taxaPercentual, int lagDias, string contaLiquidacaoId)
        {
            var forma = FormaDePagamento.Criar(businessId, nome, tipo, taxaPercentual, lagDias, contaLiquidacaoId).Valor;
            await repo.SalvarAsync(forma, ct).ConfigureAwait(false);
        }

        // Mesmos números que a antiga ConfiguracaoDeRecebiveisOptions.PadraoDeMercado hardcodava
        // (removida — FormaDePagamento é agora o LAR ÚNICO): taxas típicas de maquininha no Brasil
        // em 2025. Dinheiro cai no caixa físico; as formas eletrônicas caem no banco principal.
        await NovaAsync("dinheiro", TipoFormaPagamento.Dinheiro, 0m, 0, ClassificadorFormaPagamento.ContaCaixaPadraoId).ConfigureAwait(false);
        await NovaAsync("pix", TipoFormaPagamento.Pix, 0m, 0, contaBancoId).ConfigureAwait(false);
        await NovaAsync("debito", TipoFormaPagamento.Debito, 0.0139m, 1, contaBancoId).ConfigureAwait(false);
        await NovaAsync("credito", TipoFormaPagamento.Credito, 0.0349m, 30, contaBancoId).ConfigureAwait(false);
        await NovaAsync("boleto", TipoFormaPagamento.Boleto, 0.02m, 2, contaBancoId).ConfigureAwait(false);
    }
}
