using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Application.ReadModels;

/// <summary>Uma conta/caixa com o saldo ATUAL já calculado — nunca um campo armazenado
/// (docs/financeiro-datamodel.md §2.2): <c>SaldoInicial + soma dos MovimentoFinanceiro</c> até agora.</summary>
public sealed record ContaBancariaResumo(string Id, string Nome, string Tipo, Money Saldo, bool Ativa);

/// <summary>
/// Painel de CONTAS (a tela Bancário — docs/wiring/financeiro-telas-restantes.md §3): lista as
/// contas/caixas cadastradas com o saldo de verdade, cruzando <see cref="IContaBancariaCaixaRepository"/>
/// (o cadastro) com <see cref="IMovimentoFinanceiroRepository.CalcularSaldoAsync"/> (o ledger
/// realizado) — o mesmo par que já resolvia saldo em caixa na Visão Geral
/// (<c>QuantoSobrouDeVerdadeService</c>), agora por conta em vez de agregado do tenant inteiro.
/// </summary>
public sealed class ContasBancariasService(IContaBancariaCaixaRepository contas, IMovimentoFinanceiroRepository movimentos)
{
    public async Task<IReadOnlyList<ContaBancariaResumo>> ListarAsync(string businessId, CancellationToken ct = default)
    {
        var todas = await contas.ListarAsync(businessId, apenasAtivas: false, ct).ConfigureAwait(false);
        var agora = DateTimeOffset.UtcNow;

        var resultado = new List<ContaBancariaResumo>(todas.Count);
        foreach (var conta in todas)
        {
            var movimentado = await movimentos.CalcularSaldoAsync(businessId, conta.Id, agora, ct).ConfigureAwait(false);
            resultado.Add(new ContaBancariaResumo(
                conta.Id, conta.Nome, conta.Tipo.ToString(), conta.SaldoInicial + movimentado, conta.Ativa));
        }

        return resultado;
    }
}
