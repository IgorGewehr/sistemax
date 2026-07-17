using SistemaX.Modules.Fiscal.Application.Ports;

namespace SistemaX.Modules.Fiscal.Infrastructure.Sefaz;

/// <summary>
/// Config do gateway de emissão externo (<c>emissao.tensorroot.com</c> ou compatível) — o MESMO
/// serviço que <c>saas-erp/lib/services/sefaz-gateway.ts</c> já fala em produção
/// (docs/fiscal/emissao-mapping.md §2). Populada via env vars <c>SEFAZ_API_URL</c>/
/// <c>SEFAZ_API_KEY</c>/<c>SEFAZ_AMBIENTE</c> (mesmo trio de nomes do saas-erp) — a API key
/// pertence ao <c>.env</c> do saas-erp/infra, NUNCA commitada aqui.
/// </summary>
public sealed class SefazGatewayOptions
{
    public string BaseUrl { get; set; } = "https://emissao.tensorroot.com";

    /// <summary>Bearer token — nunca logado, nunca serializado em mensagem de erro.</summary>
    public string ApiKey { get; set; } = "";

    /// <summary>"mock" | "homologacao" | "producao". Default é homologação por segurança — uma
    /// instalação sem <c>SEFAZ_AMBIENTE</c> explícito nunca emite em produção por acidente (mesmo
    /// racional do saas-erp: "sem esse campo, toda emissão cai em homologação por default de
    /// segurança").</summary>
    public string Ambiente { get; set; } = "homologacao";

    public int TimeoutSeconds { get; set; } = 60;

    public int MaxRetries { get; set; } = 3;

    /// <summary>Modo MOCK — nunca faz I/O de rede; devolve chaveAcesso/protocolo fake. Para
    /// dev/CI sem certificado digital (mesmo papel de <c>isMockMode()</c> no sefaz-gateway.ts).</summary>
    public bool ModoMock => string.Equals(Ambiente, "mock", StringComparison.OrdinalIgnoreCase);

    /// <summary>Desfecho forçado quando <see cref="ModoMock"/> está ativo — "autorizado" (default,
    /// mesmo comportamento de sempre), "rejeitado" ou "denegado". Existe só para QA/testes
    /// exercitarem os três desfechos de <see cref="ResultadoTransmissaoSefaz"/> em modo mock, sem
    /// I/O real (o mock em si nunca decide sozinho — ele só devolve o que foi configurado; a
    /// decisão de negócio continua vindo de fora, nunca inventada aqui). Nunca lido fora de
    /// <see cref="ModoMock"/>.</summary>
    public string MockDesfecho { get; set; } = "autorizado";

    /// <summary>Motivo devolvido quando <see cref="MockDesfecho"/> é "rejeitado"/"denegado" —
    /// nunca usado quando o desfecho é "autorizado".</summary>
    public string MockMotivoDesfecho { get; set; } = "Desfecho simulado em modo mock (SEFAZ_MOCK_DESFECHO).";

    public string AmbienteSefaz => string.Equals(Ambiente, "producao", StringComparison.OrdinalIgnoreCase)
        ? "producao"
        : "homologacao";
}
