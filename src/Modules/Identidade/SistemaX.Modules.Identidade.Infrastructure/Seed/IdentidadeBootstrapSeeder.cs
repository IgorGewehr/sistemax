using Microsoft.Extensions.DependencyInjection;
using SistemaX.Modules.Abstractions.Autorizacao;
using SistemaX.Modules.Identidade.Application.CasosDeUso;
using SistemaX.Modules.Identidade.Application.Ports;

namespace SistemaX.Modules.Identidade.Infrastructure.Seed;

/// <summary>
/// Semente de bootstrap (ADR-0003 §3) — IDEMPOTENTE, roda em TODO boot (dev e produção, ao
/// contrário do <c>DemoSeeder</c> do Host, que só existe pra popular dado de demonstração):
/// se a instalação ainda não tem NENHUM usuário, cria um único <c>Founder</c> ("Administrador",
/// PIN "1234") — é o que garante que o login funciona out-of-the-box (o teclado de PIN nunca fica
/// "sem ninguém pra logar") e que a instalação nunca fica sem founder desde o primeiro boot.
///
/// PIN "1234" é o mesmo default de dev já usado por <c>HostConfigLoader</c> — não é um segredo
/// (é impresso/conhecido publicamente neste código); trocar o PIN do founder é a primeira coisa
/// que uma instalação real deveria fazer. O founder nasce com <c>Usuario.PinProvisorio = true</c>
/// — <c>POST /api/auth/login</c> devolve <c>deveTrocarPin</c> a partir disso, é o gatilho que o
/// wizard de 1º-boot da UI usa pra forçar a troca antes de liberar o resto do app; a troca em si
/// (<c>PATCH /usuarios/{id}</c> ou <c>POST /api/auth/trocar-pin</c>) zera a flag.
/// </summary>
public static class IdentidadeBootstrapSeeder
{
    private const string PinFounderPadrao = "1234";

    public static async Task SemearFounderAsync(IServiceProvider provider, string businessId, CancellationToken ct = default)
    {
        await using var scope = provider.CreateAsyncScope();
        var sp = scope.ServiceProvider;

        var repo = sp.GetRequiredService<IUsuarioRepository>();
        var existentes = await repo.ListarAsync(businessId, incluirInativos: true, ct).ConfigureAwait(false);
        if (existentes.Count > 0)
        {
            return;
        }

        var criar = sp.GetRequiredService<CriarUsuarioUseCase>();
        await criar.ExecutarAsync(
            businessId,
            nome: "Administrador",
            email: "admin@sistemax.local",
            pin: PinFounderPadrao,
            papel: Papel.Founder,
            ct: ct,
            pinProvisorio: true).ConfigureAwait(false);
    }
}
