using Microsoft.Extensions.Logging;

namespace SistemaX.Infrastructure.Local.Recovery;

/// <summary>
/// Executa todos os <see cref="ICrashRecoveryHook"/> registrados, em sequência, no boot. Um hook
/// que lança exceção é logado e PULADO — nunca impede os demais hooks de rodar, nem impede o
/// terminal de terminar de subir (o PDV precisa continuar operando mesmo se um módulo específico
/// falhar ao verificar seu próprio estado de crash-recovery).
/// </summary>
public sealed class CrashRecoveryRunner(
    IEnumerable<ICrashRecoveryHook> hooks,
    ILocalSqliteConnectionFactory connectionFactory,
    ILogger<CrashRecoveryRunner> logger)
{
    public async Task RunAllAsync(CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(ct).ConfigureAwait(false);

        foreach (var hook in hooks)
        {
            try
            {
                logger.LogInformation("Executando hook de crash-recovery: {Hook}.", hook.Nome);
                await hook.ExecutarAsync(connection, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Hook de crash-recovery '{Hook}' falhou — ignorando e seguindo boot (fail-open).", hook.Nome);
            }
        }
    }
}
