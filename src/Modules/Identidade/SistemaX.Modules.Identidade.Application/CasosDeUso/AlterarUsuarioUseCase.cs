using SistemaX.Modules.Abstractions.Autorizacao;
using SistemaX.Modules.Identidade.Application.Ports;
using SistemaX.Modules.Identidade.Domain.Usuarios;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Identidade.Application.CasosDeUso;

/// <summary>
/// Altera papel/status/PIN de um usuário já existente — <c>PATCH /usuarios/{id}</c>
/// (RequerPermissao Configuracoes:GerenciarUsuarios). Aplica a invariante "founder é intocável /
/// não rebaixar nem desativar o último founder ativo" (ADR-0003 §6) NO SERVIDOR — a mesma regra
/// que a UI de Configurações já esconde no botão, mas que aqui é reforçada de verdade: um cliente
/// que fale HTTP direto (não a SPA) também é barrado.
/// </summary>
public sealed class AlterarUsuarioUseCase(IUsuarioRepository usuarios)
{
    public async Task<Result<Usuario>> ExecutarAsync(
        string businessId,
        string usuarioId,
        Papel? novoPapel,
        bool? novoAtivo,
        string? novoPin,
        CancellationToken ct = default)
    {
        var usuario = await usuarios.ObterPorIdAsync(usuarioId, ct).ConfigureAwait(false);
        if (usuario is null || usuario.BusinessId != businessId)
        {
            return Result.Falhar<Usuario>(new Error(
                "usuario.nao_encontrado", $"Usuário '{usuarioId}' não encontrado."));
        }

        var mudaPapel = novoPapel is { } papel && papel != usuario.Papel;
        var mudaParaInativo = novoAtivo == false && usuario.Status == StatusUsuario.Ativo;

        if (usuario.EhIntocavel() && (mudaPapel || mudaParaInativo))
        {
            var ativos = await usuarios.ListarAsync(businessId, incluirInativos: false, ct).ConfigureAwait(false);
            var foundersAtivos = ativos.Count(u => u.Papel == Papel.Founder);

            if (foundersAtivos <= 1)
            {
                return Result.Falhar<Usuario>(new Error(
                    "usuario.founder_intocavel",
                    "Não é possível rebaixar ou desativar o último founder ativo da instalação."));
            }
        }

        if (!string.IsNullOrEmpty(novoPin))
        {
            var ativosParaDedupePin = await usuarios.ListarAsync(businessId, incluirInativos: false, ct).ConfigureAwait(false);
            if (ativosParaDedupePin.Any(u => u.Id != usuario.Id && u.VerificarPin(novoPin)))
            {
                return Result.Falhar<Usuario>(new Error(
                    "usuario.pin_duplicado", "Este PIN já está em uso por outro usuário ativo desta instalação."));
            }

            var resultadoPin = usuario.RedefinirPin(novoPin);
            if (resultadoPin.Falha) return Result.Falhar<Usuario>(resultadoPin.Erro);
        }

        if (mudaPapel)
        {
            usuario.TrocarPapel(novoPapel!.Value);
        }

        if (novoAtivo is { } ativo)
        {
            var alvo = ativo ? StatusUsuario.Ativo : StatusUsuario.Inativo;

            // Fsm<TStatus>.ValidarTransicao não trata origem==destino como no-op — só chama a
            // transição do agregado quando o status pedido é DE FATO diferente do atual (ex.:
            // PATCH manda ativo=true num usuário que já está ativo, só pra trocar o papel; nesse
            // caso não é uma transição, é apenas "sem mudança nesse campo").
            if (alvo != usuario.Status)
            {
                var resultadoStatus = ativo ? usuario.Ativar() : usuario.Desativar();
                if (resultadoStatus.Falha) return Result.Falhar<Usuario>(resultadoStatus.Erro);
            }
        }

        await usuarios.SalvarAsync(usuario, ct).ConfigureAwait(false);
        return Result.Ok(usuario);
    }
}
