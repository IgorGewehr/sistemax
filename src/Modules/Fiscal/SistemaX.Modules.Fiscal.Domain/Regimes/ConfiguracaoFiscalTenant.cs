using SistemaX.SharedKernel;

namespace SistemaX.Modules.Fiscal.Domain.Regimes;

/// <summary>
/// "Qual regime este tenant está" — o FATO estável (muda raramente, ver §1 de
/// docs/fiscal/arquitetura.md) que os handlers de evento de integração precisam para saber qual
/// <see cref="RegimeTributario"/>/UF usar ao abrir um <c>DocumentoFiscal</c> a partir de uma
/// venda. Uma linha por tenant — trocar de regime é uma edição desta linha (Settings→Fiscal),
/// nunca deploy.
///
/// <see cref="CscId"/>/<see cref="CscToken"/> fecham o gap #2 de docs/fiscal/emissao-mapping.md §11
/// ("CSC (NFC-e) continua em aberto"): o Código de Segurança do Contribuinte é exigido pela SEFAZ
/// para compor o QR Code/hash de toda NFC-e (nunca NF-e) — sem ele a emissão de NFC-e é
/// impossível, mesmo com certificado digital válido. Nullable porque um tenant que só emite NF-e
/// nunca precisa cadastrar CSC; esta classe NÃO valida presença aqui (não é fato tributário do
/// regime, é credencial operacional de NFC-e) — quem exigirá o campo é o mapper de NFC-e no
/// momento de montar o payload (gap continua rastreado até essa validação existir).
/// </summary>
public sealed record ConfiguracaoFiscalTenant(
    string TenantId,
    RegimeTributario Regime,
    string UfOrigem,
    string SerieNfce,
    string SerieNfe,
    string? CscId = null,
    string? CscToken = null)
{
    public static Result<ConfiguracaoFiscalTenant> Criar(
        string tenantId, RegimeTributario regime, string ufOrigem, string serieNfce = "1", string serieNfe = "1",
        string? cscId = null, string? cscToken = null)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            return Result.Falhar<ConfiguracaoFiscalTenant>(new Error("fiscal.configuracao_tenant.tenant_obrigatorio", "TenantId é obrigatório."));

        if (string.IsNullOrWhiteSpace(ufOrigem) || ufOrigem.Length != 2)
            return Result.Falhar<ConfiguracaoFiscalTenant>(new Error("fiscal.configuracao_tenant.uf_invalida", $"UF de origem '{ufOrigem}' inválida."));

        return Result.Ok(new ConfiguracaoFiscalTenant(
            tenantId, regime, ufOrigem.ToUpperInvariant(), serieNfce, serieNfe,
            string.IsNullOrWhiteSpace(cscId) ? null : cscId,
            string.IsNullOrWhiteSpace(cscToken) ? null : cscToken));
    }
}
