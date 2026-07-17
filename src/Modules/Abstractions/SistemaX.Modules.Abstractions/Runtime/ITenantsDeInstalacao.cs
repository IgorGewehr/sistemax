namespace SistemaX.Modules.Abstractions.Runtime;

/// <summary>
/// Resolve o(s) tenant(s) que um job de BACKGROUND (cron sem <c>HttpContext</c> — ex.: avaliação
/// de parcelas vencidas, retransmissão fiscal) deve processar a cada rodada. Endpoints HTTP
/// resolvem o tenant da sessão (<c>SessaoHttpContextExtensions.ObterBusinessId</c>, R1); um
/// <see cref="Microsoft.Extensions.Hosting.BackgroundService"/> não tem sessão nenhuma — precisa
/// de uma fonte alternativa, que é este contrato.
///
/// HOJE (Host.Desktop): cada processo é UM tenant fixo — o mesmo <c>hostConfig.BusinessId</c> que
/// a sessão HTTP resolveria em runtime — ver <see cref="TenantsDeInstalacaoFixo"/>, registrado no
/// composition root (<c>SistemaXHost</c>). Uma Nuvem multi-tenant (fora do escopo desta fatia)
/// troca a implementação por uma que enumera todos os tenants ativos numa tabela — nenhum
/// consumidor deste contrato muda.
/// </summary>
public interface ITenantsDeInstalacao
{
    /// <summary>Tenants ativos que a rodada atual do job deve processar.</summary>
    Task<IReadOnlyList<string>> ObterBusinessIdsAsync(CancellationToken ct = default);
}

/// <summary>Instalação desktop = exatamente um tenant, fixo pra vida inteira do processo.</summary>
public sealed class TenantsDeInstalacaoFixo(string businessId) : ITenantsDeInstalacao
{
    private readonly IReadOnlyList<string> _businessIds = [businessId];

    public Task<IReadOnlyList<string>> ObterBusinessIdsAsync(CancellationToken ct = default)
        => Task.FromResult(_businessIds);
}
