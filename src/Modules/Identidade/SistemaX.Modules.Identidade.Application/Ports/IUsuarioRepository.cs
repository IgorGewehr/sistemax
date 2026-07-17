using SistemaX.Modules.Identidade.Domain.Usuarios;

namespace SistemaX.Modules.Identidade.Application.Ports;

public interface IUsuarioRepository
{
    Task<Usuario?> ObterPorIdAsync(string id, CancellationToken ct = default);

    /// <summary>Lista os usuários do tenant. <paramref name="incluirInativos"/> = <c>false</c> é o
    /// caminho de autenticação (<c>AutenticarPorPinUseCase</c> só considera candidatos ativos);
    /// <c>true</c> é o caminho de administração (<c>GET /usuarios</c> mostra todos, ativos e
    /// inativos, para o admin poder reativar alguém).</summary>
    Task<IReadOnlyList<Usuario>> ListarAsync(string businessId, bool incluirInativos, CancellationToken ct = default);

    Task SalvarAsync(Usuario usuario, CancellationToken ct = default);
}
