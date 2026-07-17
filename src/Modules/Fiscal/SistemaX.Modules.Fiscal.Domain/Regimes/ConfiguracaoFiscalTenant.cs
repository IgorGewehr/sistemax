using SistemaX.SharedKernel;

namespace SistemaX.Modules.Fiscal.Domain.Regimes;

/// <summary>
/// "Qual regime este tenant está" — o FATO estável (muda raramente, ver §1 de
/// docs/fiscal/arquitetura.md) que os handlers de evento de integração precisam para saber qual
/// <see cref="RegimeTributario"/>/UF usar ao abrir um <c>DocumentoFiscal</c> a partir de uma
/// venda. Uma linha por tenant — trocar de regime é uma edição desta linha (Settings→Fiscal),
/// nunca deploy.
/// </summary>
public sealed record ConfiguracaoFiscalTenant(
    string TenantId,
    RegimeTributario Regime,
    string UfOrigem,
    string SerieNfce,
    string SerieNfe)
{
    public static Result<ConfiguracaoFiscalTenant> Criar(string tenantId, RegimeTributario regime, string ufOrigem, string serieNfce = "1", string serieNfe = "1")
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            return Result.Falhar<ConfiguracaoFiscalTenant>(new Error("fiscal.configuracao_tenant.tenant_obrigatorio", "TenantId é obrigatório."));

        if (string.IsNullOrWhiteSpace(ufOrigem) || ufOrigem.Length != 2)
            return Result.Falhar<ConfiguracaoFiscalTenant>(new Error("fiscal.configuracao_tenant.uf_invalida", $"UF de origem '{ufOrigem}' inválida."));

        return Result.Ok(new ConfiguracaoFiscalTenant(tenantId, regime, ufOrigem.ToUpperInvariant(), serieNfce, serieNfe));
    }
}
