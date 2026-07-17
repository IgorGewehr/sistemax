using System.Collections.Concurrent;
using SistemaX.Modules.Identidade.Application.Ports;
using SistemaX.Modules.Identidade.Domain.Usuarios;

namespace SistemaX.Modules.Identidade.Infrastructure.InMemory;

/// <summary>
/// Adapter in-memory — suficiente para rodar o módulo e os testes sem infraestrutura externa.
/// Mesmo padrão de <c>InMemoryFornecedorRepository</c>: trocar por SQLite mantendo exatamente
/// esta interface de port não muda uma linha de Domain/Application.
/// </summary>
public sealed class InMemoryUsuarioRepository : IUsuarioRepository
{
    private readonly ConcurrentDictionary<string, Usuario> _porId = new();

    public Task<Usuario?> ObterPorIdAsync(string id, CancellationToken ct = default)
        => Task.FromResult(_porId.GetValueOrDefault(id));

    public Task<IReadOnlyList<Usuario>> ListarAsync(string businessId, bool incluirInativos, CancellationToken ct = default)
    {
        IReadOnlyList<Usuario> resultado = _porId.Values
            .Where(u => u.BusinessId == businessId && (incluirInativos || u.Status == StatusUsuario.Ativo))
            .ToList();

        return Task.FromResult(resultado);
    }

    public Task SalvarAsync(Usuario usuario, CancellationToken ct = default)
    {
        _porId[usuario.Id] = usuario;
        return Task.CompletedTask;
    }
}
