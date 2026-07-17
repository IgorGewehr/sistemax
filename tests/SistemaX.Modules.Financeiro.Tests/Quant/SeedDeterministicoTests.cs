using SistemaX.Modules.Financeiro.Application.Quant;

namespace SistemaX.Modules.Financeiro.Tests.Quant;

/// <summary>Prova da regra dura do ADR-0005 §3.4: "mesmo input, mesmo output, sempre".</summary>
public sealed class SeedDeterministicoTests
{
    [Fact]
    public void Mesmo_tenant_e_periodo_produz_sempre_a_mesma_seed()
    {
        var seed1 = SeedDeterministico.Gerar("tenant-1", "2026-07");
        var seed2 = SeedDeterministico.Gerar("tenant-1", "2026-07");
        Assert.Equal(seed1, seed2);
    }

    [Fact]
    public void Tenants_diferentes_produzem_seeds_diferentes()
    {
        var seedA = SeedDeterministico.Gerar("tenant-a", "2026-07");
        var seedB = SeedDeterministico.Gerar("tenant-b", "2026-07");
        Assert.NotEqual(seedA, seedB);
    }

    [Fact]
    public void Periodos_diferentes_do_mesmo_tenant_produzem_seeds_diferentes()
    {
        var seedJulho = SeedDeterministico.Gerar("tenant-1", "2026-07");
        var seedAgosto = SeedDeterministico.Gerar("tenant-1", "2026-08");
        Assert.NotEqual(seedJulho, seedAgosto);
    }

    [Fact]
    public void Seed_alimenta_random_deterministico_com_a_mesma_sequencia()
    {
        var seed = SeedDeterministico.Gerar("tenant-x", "2026-07");
        var sequencia1 = new Random(seed).Next(0, 1_000_000);
        var sequencia2 = new Random(seed).Next(0, 1_000_000);
        Assert.Equal(sequencia1, sequencia2);
    }
}
