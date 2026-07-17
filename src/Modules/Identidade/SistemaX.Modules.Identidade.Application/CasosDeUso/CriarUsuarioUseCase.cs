using SistemaX.Modules.Abstractions.Autorizacao;
using SistemaX.Modules.Identidade.Application.Ports;
using SistemaX.Modules.Identidade.Domain.Usuarios;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Identidade.Application.CasosDeUso;

/// <summary>
/// Cadastra um novo usuário — <c>POST /usuarios</c> (RequerPermissao Configuracoes:GerenciarUsuarios).
/// Reforça a unicidade estrutural do PIN entre os usuários ATIVOS do tenant (ADR-0003 §2): sem
/// isso, <see cref="AutenticarPorPinUseCase"/> não consegue identificar univocamente quem logou.
/// </summary>
public sealed class CriarUsuarioUseCase(IUsuarioRepository usuarios)
{
    public async Task<Result<Usuario>> ExecutarAsync(
        string businessId, string nome, string email, string pin, Papel papel, CancellationToken ct = default)
    {
        var ativos = await usuarios.ListarAsync(businessId, incluirInativos: false, ct).ConfigureAwait(false);

        if (ativos.Any(u => u.VerificarPin(pin)))
        {
            return Result.Falhar<Usuario>(new Error(
                "usuario.pin_duplicado", "Este PIN já está em uso por outro usuário ativo desta instalação."));
        }

        var resultado = Usuario.Criar(businessId, nome, email, pin, papel);
        if (resultado.Falha) return resultado;

        await usuarios.SalvarAsync(resultado.Valor, ct).ConfigureAwait(false);
        return resultado;
    }
}
