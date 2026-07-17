using System.Collections.Concurrent;
using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Domain.Caixa;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Infrastructure.InMemory;

public sealed class InMemoryMovimentoFinanceiroRepository : IMovimentoFinanceiroRepository
{
    private readonly ConcurrentDictionary<string, MovimentoFinanceiro> _porId = new();

    public Task<MovimentoFinanceiro?> ObterPorIdAsync(string id, CancellationToken ct = default)
        => Task.FromResult(_porId.GetValueOrDefault(id));

    public Task<MovimentoFinanceiro?> BuscarPorOrigemAsync(string businessId, string sourceRefChave, CancellationToken ct = default)
        => Task.FromResult(_porId.Values.FirstOrDefault(m => m.BusinessId == businessId && m.Origem.Chave == sourceRefChave));

    public Task<MovimentoFinanceiro?> BuscarEstornoDeAsync(string movimentoOriginalId, CancellationToken ct = default)
        => Task.FromResult(_porId.Values.FirstOrDefault(m => m.ReversalOfId == movimentoOriginalId));

    public Task<IReadOnlyList<MovimentoFinanceiro>> ListarPorContaOrigemAsync(string businessId, string contaOrigemId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<MovimentoFinanceiro>>(_porId.Values
            .Where(m => m.BusinessId == businessId && m.ContaOrigemId == contaOrigemId)
            .ToList());

    public Task SalvarAsync(MovimentoFinanceiro movimento, CancellationToken ct = default)
    {
        _porId[movimento.Id] = movimento;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<MovimentoFinanceiro>> ListarPorPeriodoAsync(string businessId, DateTimeOffset inicio, DateTimeOffset fim, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<MovimentoFinanceiro>>(_porId.Values
            .Where(m => m.BusinessId == businessId && m.DataMovimento >= inicio && m.DataMovimento <= fim)
            .ToList());

    public Task<Money> CalcularSaldoAsync(string businessId, string? contaBancariaCaixaId, DateTimeOffset ateData, CancellationToken ct = default)
    {
        var relevantes = _porId.Values.Where(m => m.BusinessId == businessId && m.DataMovimento <= ateData);
        if (contaBancariaCaixaId is not null)
            relevantes = relevantes.Where(m => m.ContaBancariaCaixaId == contaBancariaCaixaId);

        var saldo = relevantes.Aggregate(Money.Zero, (acumulado, m) =>
            m.Tipo == TipoMovimentoFinanceiro.Entrada ? acumulado + m.Valor : acumulado - m.Valor);

        return Task.FromResult(saldo);
    }
}
