using SistemaX.Modules.Fiscal.Domain.Comum;
using SistemaX.Modules.Fiscal.Domain.Operacoes;
using SistemaX.Modules.Fiscal.Domain.Regimes;

namespace SistemaX.Modules.Fiscal.Domain.Regras;

/// <summary>
/// Uma linha da matriz de decisão fiscal — a peça que resolve literalmente o requisito "CSOSN
/// configurável por regime/operação/UF", nunca literal de código (ADR-0002). Chave composta =
/// (<see cref="Regime"/>, <see cref="TipoOperacao"/>, <see cref="UfOrigem"/>,
/// <see cref="UfDestino"/> nullable = "qualquer", <see cref="IndicadorSt"/>). TenantId é
/// nullable: linha sem TenantId é DEFAULT DO SISTEMA (seed curada, editável em runtime pelo
/// suporte); linha COM TenantId sobrepõe o default só para aquele tenant (ex.: benefício fiscal
/// estadual específico daquela empresa) — ver docs/fiscal/arquitetura.md §2.3.
/// </summary>
public sealed record RegraFiscalPorOperacao(
    string? TenantId,
    RegimeTributario Regime,
    TipoOperacaoFiscal TipoOperacao,
    string UfOrigem,
    string? UfDestino,
    bool IndicadorSt,
    SituacaoTributariaIcms SituacaoIcms,
    Percentual? AliquotaInterna,
    Percentual? AliquotaInterestadual,
    Percentual? ReducaoBaseCalculo = null,
    Percentual? Mva = null,
    // FCP (Fundo de Combate à Pobreza) do UF DESTINO desta linha — só populado nas linhas usadas
    // para resolver a partilha DIFAL (chave com UfDestino concreto), nunca nas linhas "qualquer
    // destino" (docs/fiscal/arquitetura.md §2.2/§2.6/§3).
    Percentual? AliquotaFcp = null)
{
    /// <summary>Especificidade da linha — usada pelo resolvedor para desempatar quando mais de
    /// uma linha bate (tenant-específica vence default; UfDestino exata vence "qualquer").</summary>
    public int Especificidade =>
        (TenantId is not null ? 2 : 0) + (UfDestino is not null ? 1 : 0);

    /// <summary>Alíquota efetiva conforme a operação é interna ou interestadual — usada pelo
    /// motor de cálculo (docs/fiscal/arquitetura.md §3 passo 7).</summary>
    public Percentual? AliquotaPara(bool ehInterestadual) => ehInterestadual ? AliquotaInterestadual : AliquotaInterna;
}
