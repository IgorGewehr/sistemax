using System.Collections.Concurrent;
using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Domain.Projetos;

namespace SistemaX.Modules.Financeiro.Infrastructure.InMemory;

/// <summary>Adapter in-memory de <see cref="IProjetoRepository"/>. Trocável por
/// <see cref="Sqlite.SqliteProjetoRepository"/> sem tocar Domain/Application (mesmo port).</summary>
public sealed class InMemoryProjetoRepository : IProjetoRepository
{
    private readonly ConcurrentDictionary<string, Projeto> _porId = new();

    private static string Chave(string businessId, string id) => $"{businessId}:{id}";

    public Task<IReadOnlyList<Projeto>> ListarAsync(string businessId, bool incluirArquivados, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Projeto>>(
            _porId.Values
                .Where(p => p.BusinessId == businessId && (incluirArquivados || p.Status == StatusProjeto.Ativo))
                .OrderBy(p => p.CriadoEm)
                .ToList());

    public Task<Projeto?> ObterPorIdAsync(string businessId, string projetoId, CancellationToken ct = default)
    {
        var projeto = _porId.GetValueOrDefault(Chave(businessId, projetoId));
        return Task.FromResult(projeto is not null && projeto.BusinessId == businessId ? projeto : null);
    }

    public Task<Projeto?> BuscarPorNomeAsync(string businessId, string nome, CancellationToken ct = default)
        => Task.FromResult(_porId.Values.FirstOrDefault(p =>
            p.BusinessId == businessId && string.Equals(p.Nome, nome, StringComparison.OrdinalIgnoreCase)));

    public Task SalvarAsync(Projeto projeto, CancellationToken ct = default)
    {
        _porId[Chave(projeto.BusinessId, projeto.Id)] = projeto;
        return Task.CompletedTask;
    }
}
