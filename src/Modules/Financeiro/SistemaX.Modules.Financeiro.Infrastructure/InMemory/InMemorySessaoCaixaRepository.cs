using System.Collections.Concurrent;
using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Domain.Caixa;

namespace SistemaX.Modules.Financeiro.Infrastructure.InMemory;

public sealed class InMemorySessaoCaixaRepository : ISessaoCaixaRepository
{
    private readonly ConcurrentDictionary<string, SessaoCaixa> _porId = new();

    private static string Chave(string businessId, string id) => $"{businessId}:{id}";

    public Task<SessaoCaixa?> ObterPorIdAsync(string businessId, string id, CancellationToken ct = default)
        => Task.FromResult(_porId.GetValueOrDefault(Chave(businessId, id)));

    public Task<SessaoCaixa?> ObterAbertaPorContaAsync(string businessId, string contaCaixaId, CancellationToken ct = default)
        => Task.FromResult(_porId.Values
            .Where(s => s.BusinessId == businessId && s.ContaCaixaId == contaCaixaId && s.Status == StatusSessaoCaixa.Aberta)
            .OrderByDescending(s => s.AbertaEm)
            .FirstOrDefault());

    public Task<IReadOnlyList<SessaoCaixa>> ListarAsync(
        string businessId, string contaCaixaId, DateTimeOffset? de = null, DateTimeOffset? ate = null, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<SessaoCaixa>>(_porId.Values
            .Where(s => s.BusinessId == businessId && s.ContaCaixaId == contaCaixaId)
            .Where(s => de is null || s.AbertaEm >= de)
            .Where(s => ate is null || s.AbertaEm <= ate)
            .OrderByDescending(s => s.AbertaEm)
            .ToList());

    public Task SalvarAsync(SessaoCaixa sessao, CancellationToken ct = default)
    {
        _porId[Chave(sessao.BusinessId, sessao.Id)] = sessao;
        return Task.CompletedTask;
    }
}
