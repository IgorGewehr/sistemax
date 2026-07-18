using System.Collections.Concurrent;
using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Domain.ContasAPagarReceber;

namespace SistemaX.Modules.Financeiro.Infrastructure.InMemory;

/// <summary>Espelha <see cref="InMemoryContaAReceberRepository"/> — ver comentário lá sobre extensão para SQLite.</summary>
public sealed class InMemoryContaAPagarRepository : IContaAPagarRepository
{
    private readonly ConcurrentDictionary<string, ContaAPagar> _porId = new();

    public Task<ContaAPagar?> ObterPorIdAsync(string id, CancellationToken ct = default)
        => Task.FromResult(_porId.GetValueOrDefault(id));

    public Task<ContaAPagar?> BuscarPorOrigemAsync(string businessId, string sourceRefChave, CancellationToken ct = default)
        => Task.FromResult(_porId.Values.FirstOrDefault(c => c.BusinessId == businessId && c.SourceRef.Chave == sourceRefChave));

    public Task SalvarAsync(ContaAPagar conta, CancellationToken ct = default)
    {
        _porId[conta.Id] = conta;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ContaAPagar>> ListarPorCompetenciaAsync(string businessId, DateTimeOffset inicio, DateTimeOffset fim, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<ContaAPagar>>(_porId.Values
            .Where(c => c.BusinessId == businessId && c.DataCompetencia >= inicio && c.DataCompetencia <= fim)
            .ToList());

    public Task<IReadOnlyList<ContaAPagar>> ListarAbertasAteAsync(string businessId, DateTimeOffset referencia, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<ContaAPagar>>(_porId.Values
            .Where(c => c.BusinessId == businessId)
            .Where(c => c.Parcelas.Any(p => p.Status is Domain.Comum.StatusFinanceiro.Aberto or Domain.Comum.StatusFinanceiro.Parcial or Domain.Comum.StatusFinanceiro.Atrasado)
                        && c.Parcelas.Any(p => p.Vencimento <= referencia))
            .ToList());

    public Task<IReadOnlyList<ContaAPagar>> ListarPorCategoriaAsync(string businessId, string categoriaId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<ContaAPagar>>(_porId.Values
            .Where(c => c.BusinessId == businessId && c.CategoriaId == categoriaId)
            .ToList());
}
