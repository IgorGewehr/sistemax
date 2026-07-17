using SistemaX.Modules.Fiscal.Domain.Comum;
using SistemaX.Modules.Fiscal.Domain.Regimes;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Fiscal.Domain.Ncm;

/// <summary>
/// Tributação DEFAULT de um NCM sob um regime — chave (TenantId, Regime, Ncm). Populada em massa
/// (preenchimento de NCM do Estoque, docs/fiscal/arquitetura.md §4) e editável linha a linha.
/// TenantId aqui (diferente de <c>RegraFiscalPorOperacao</c>) NÃO é opcional: perfil fiscal de NCM
/// é sempre do tenant (o contador da empresa decide se aquele NCM tem ICMS-ST, qual CEST, qual
/// IPI — ainda que o sistema possa sugerir valores iniciais de uma tabela de referência).
///
/// Nota deliberada: NÃO guarda o CSOSN/CST de ICMS — o DEFAULT vem de
/// <c>RegraFiscalPorOperacao</c>, porque a situação tributária de ICMS depende também da
/// OPERAÇÃO (venda normal vs devolução vs transferência), não só do NCM.
/// </summary>
public sealed record PerfilFiscalNCM(
    string TenantId,
    RegimeTributario Regime,
    string Ncm,
    OrigemMercadoria Origem,
    bool ExigeIcmsSt,
    string? Cest,
    Percentual? AliquotaIpi,
    string CstOuCsosnPisCofins,
    Percentual? AliquotaPis,
    Percentual? AliquotaCofins,
    DateTimeOffset AtualizadoEm)
{
    public static Result<PerfilFiscalNCM> Criar(
        string tenantId, RegimeTributario regime, string ncm, OrigemMercadoria origem, bool exigeIcmsSt, string? cest,
        Percentual? aliquotaIpi, string cstOuCsosnPisCofins, Percentual? aliquotaPis, Percentual? aliquotaCofins)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            return Result.Falhar<PerfilFiscalNCM>(new Error("fiscal.ncm.tenant_obrigatorio", "TenantId é obrigatório para um perfil fiscal de NCM."));

        if (!NcmValido(ncm))
            return Result.Falhar<PerfilFiscalNCM>(new Error("fiscal.ncm.formato_invalido", $"NCM '{ncm}' não tem 8 dígitos."));

        if (exigeIcmsSt && string.IsNullOrWhiteSpace(cest))
            return Result.Falhar<PerfilFiscalNCM>(new Error("fiscal.ncm.cest_obrigatorio", "NCM com ICMS-ST exige CEST."));

        return Result.Ok(new PerfilFiscalNCM(tenantId, regime, ncm, origem, exigeIcmsSt, cest, aliquotaIpi,
            cstOuCsosnPisCofins, aliquotaPis, aliquotaCofins, DateTimeOffset.UtcNow));
    }

    private static bool NcmValido(string ncm) => !string.IsNullOrEmpty(ncm) && ncm.Length == 8 && ncm.All(char.IsDigit);
}
