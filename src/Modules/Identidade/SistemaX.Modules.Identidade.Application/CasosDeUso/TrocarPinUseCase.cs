using SistemaX.Modules.Identidade.Application.Ports;
using SistemaX.Modules.Identidade.Domain.Usuarios;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Identidade.Application.CasosDeUso;

/// <summary>
/// Autoatendimento — <c>POST /api/auth/trocar-pin</c> (Bridge). Ao contrário de
/// <see cref="AlterarUsuarioUseCase"/> (que exige <c>Configuracoes:GerenciarUsuarios</c>, ou seja,
/// só founder/admin mexe no PIN de OUTRO usuário), este caso de uso é para qualquer papel trocar o
/// PRÓPRIO PIN: não há checagem de permissão aqui — a "permissão" é ser o dono da sessão que bateu
/// o <paramref name="pinAtual"/> certo (por isso <c>usuarioId</c> nunca vem do corpo do request, só
/// da sessão Bearer, ver <c>BridgeEndpoints</c>). Zera <see cref="Usuario.PinProvisorio"/> — é o
/// caminho que o wizard de 1º-boot usa para encerrar o "PIN 1234" do founder semeado.
/// </summary>
public sealed class TrocarPinUseCase(IUsuarioRepository usuarios)
{
    public async Task<Result<Usuario>> ExecutarAsync(
        string businessId, string usuarioId, string pinAtual, string pinNovo, CancellationToken ct = default)
    {
        var usuario = await usuarios.ObterPorIdAsync(usuarioId, ct).ConfigureAwait(false);
        if (usuario is null || usuario.BusinessId != businessId)
        {
            return Result.Falhar<Usuario>(new Error(
                "usuario.nao_encontrado", $"Usuário '{usuarioId}' não encontrado."));
        }

        if (!usuario.VerificarPin(pinAtual))
        {
            return Result.Falhar<Usuario>(new Error(
                "auth.pin_atual_incorreto", "PIN atual incorreto."));
        }

        // Servidor é a fonte da verdade da força do PIN — o wizard no web valida igual, mas é
        // bypassável. Recusa dígitos repetidos e sequências (0000, 1234…) para não deixar o
        // operador trocar o "1234" provisório por outro PIN igualmente trivial.
        if (PinTrivial.EhTrivial(pinNovo))
        {
            return Result.Falhar<Usuario>(new Error(
                "auth.pin_trivial",
                "PIN muito fácil de adivinhar — evite dígitos repetidos ou sequências (0000, 1234…)."));
        }

        var ativos = await usuarios.ListarAsync(businessId, incluirInativos: false, ct).ConfigureAwait(false);
        if (ativos.Any(u => u.Id != usuario.Id && u.VerificarPin(pinNovo)))
        {
            return Result.Falhar<Usuario>(new Error(
                "usuario.pin_duplicado", "Este PIN já está em uso por outro usuário ativo desta instalação."));
        }

        var resultadoPin = usuario.RedefinirPin(pinNovo);
        if (resultadoPin.Falha) return Result.Falhar<Usuario>(resultadoPin.Erro);

        await usuarios.SalvarAsync(usuario, ct).ConfigureAwait(false);
        return Result.Ok(usuario);
    }
}
