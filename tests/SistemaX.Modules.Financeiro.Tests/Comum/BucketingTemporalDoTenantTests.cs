using SistemaX.Modules.Financeiro.Application.Comum;

namespace SistemaX.Modules.Financeiro.Tests.Comum;

/// <summary>
/// F0 do plano de inteligência do Financeiro (docs/financeiro/inteligencia-arquitetura.md/
/// ADR-0005) — regressão do bug documentado: "uma venda das 22h (horário local) cai no dia
/// seguinte quando bucketada em UTC-3".
/// </summary>
public sealed class BucketingTemporalDoTenantTests
{
    [Fact]
    public void DiaLocal_venda_as_22h_locais_fica_no_mesmo_dia_local_mesmo_cruzando_o_dia_em_utc()
    {
        // 22h de 15/07 em America/Sao_Paulo (UTC-3) = 01h de 16/07 em UTC.
        var instante = new DateTimeOffset(2026, 7, 15, 22, 0, 0, TimeSpan.FromHours(-3));

        Assert.Equal(new DateOnly(2026, 7, 15), BucketingTemporalDoTenant.DiaLocal(instante));
        Assert.Equal(new DateOnly(2026, 7, 16), DateOnly.FromDateTime(instante.UtcDateTime)); // o bug que este utilitário corrige
    }

    [Fact]
    public void DiaLocal_meio_dia_local_nao_cruza_fronteira_nenhuma()
    {
        var instante = new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.FromHours(-3));
        Assert.Equal(new DateOnly(2026, 7, 15), BucketingTemporalDoTenant.DiaLocal(instante));
    }
}
