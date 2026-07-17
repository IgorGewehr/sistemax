using SistemaX.Modules.Identidade.Application.Ports;
using SistemaX.Modules.Identidade.Domain.Usuarios;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Identidade.Application.CasosDeUso;

/// <summary>
/// Coração do ADR-0003 §2 — login CONTINUA sendo só PIN (nenhum seletor de pessoa). O PIN deixa
/// de ser comparado contra um único <c>config.json</c> e passa a ser verificado contra TODOS os
/// usuários ATIVOS da instalação; o primeiro que bater é quem logou.
///
/// Candidatos ordenados por <c>UltimoAcessoEm</c> decrescente (quem loga mais, loga de novo —
/// otimização pequena do caso médio, não é o que garante corretude: PIN é estruturalmente único
/// entre ativos, ver <c>CriarUsuarioUseCase</c>/<c>AlterarUsuarioUseCase</c>).
///
/// Falha genérica "PIN incorreto" — nunca revela se o PIN quase bateu em alguém (mesmo padrão de
/// não-enumeração do login de hoje).
/// </summary>
public sealed class AutenticarPorPinUseCase(IUsuarioRepository usuarios)
{
    public async Task<Result<Usuario>> ExecutarAsync(string businessId, string pin, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(pin))
        {
            return Result.Falhar<Usuario>(new Error("auth.pin_invalido", "PIN incorreto."));
        }

        var candidatos = await usuarios.ListarAsync(businessId, incluirInativos: false, ct).ConfigureAwait(false);

        foreach (var candidato in candidatos.OrderByDescending(u => u.UltimoAcessoEm ?? DateTimeOffset.MinValue))
        {
            if (!candidato.VerificarPin(pin))
            {
                continue;
            }

            candidato.RegistrarAcesso(DateTimeOffset.UtcNow);
            await usuarios.SalvarAsync(candidato, ct).ConfigureAwait(false);
            return Result.Ok(candidato);
        }

        return Result.Falhar<Usuario>(new Error("auth.pin_invalido", "PIN incorreto."));
    }
}
