using SistemaX.Modules.Financeiro.Application.Categorias;
using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Application.Quant;
using SistemaX.Modules.Financeiro.Application.ReadModels;
using SistemaX.Modules.Financeiro.Domain.Comum;
using SistemaX.Modules.Financeiro.Domain.ContasAPagarReceber;
using SistemaX.Modules.Financeiro.Infrastructure.InMemory;
using SistemaX.Modules.Financeiro.Tests.Fakes;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Tests.ReadModels;

/// <summary>
/// P0-4 (docs/financeiro/revisao-domain-fit-cnpj.md) — Radar do Simples multi-anexo: RBT12 soma
/// TODAS as correntes (inclusive assinatura), Fator R decide III×V, e o mix reparte o DAS por
/// anexo conforme a config (real ou padrão) do tenant.
/// </summary>
public sealed class RadarDoSimplesServiceTests
{
    private const string BusinessId = "biz-radar";

    private sealed record Ambiente(
        RadarDoSimplesService Servico,
        InMemoryFatoReceitaDiariaRepository FatoReceitaDiaria,
        InMemoryContaAPagarRepository ContasAPagar,
        InMemoryConfiguracaoRadarSimplesRepository ConfiguracaoRadar,
        FakeRelogio Relogio);

    private static Ambiente NovoAmbiente(DateTimeOffset hoje)
    {
        var fatoReceitaDiaria = new InMemoryFatoReceitaDiariaRepository();
        var contasAPagar = new InMemoryContaAPagarRepository();
        var configuracaoRadar = new InMemoryConfiguracaoRadarSimplesRepository();
        var relogio = new FakeRelogio(hoje);
        var servico = new RadarDoSimplesService(fatoReceitaDiaria, contasAPagar, configuracaoRadar, relogio);

        return new Ambiente(servico, fatoReceitaDiaria, contasAPagar, configuracaoRadar, relogio);
    }

    [Fact]
    public async Task Rbt12_soma_todas_as_correntes_incluindo_recorrente_de_assinatura()
    {
        var hoje = new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);
        var ambiente = NovoAmbiente(hoje);
        var hojeData = DateOnly.FromDateTime(hoje.UtcDateTime);

        await ambiente.FatoReceitaDiaria.AcumularAsync(BusinessId, hojeData, CorrenteDeReceita.Comercio, 10_000_00);
        await ambiente.FatoReceitaDiaria.AcumularAsync(BusinessId, hojeData, CorrenteDeReceita.Servico, 5_000_00);
        await ambiente.FatoReceitaDiaria.AcumularAsync(BusinessId, hojeData, CorrenteDeReceita.Recorrente, 2_000_00);

        var resultado = await ambiente.Servico.CalcularAsync(BusinessId, AnexoSimplesNacional.I);

        Assert.True(resultado.Sucesso);
        Assert.Equal(17_000_00, resultado.Valor.Rbt12Centavos); // 10.000 + 5.000 + 2.000 — as TRÊS correntes
    }

    [Fact]
    public async Task Fator_r_e_calculado_da_folha_de_doze_meses_sobre_o_rbt12()
    {
        var hoje = new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);
        var ambiente = NovoAmbiente(hoje);
        var hojeData = DateOnly.FromDateTime(hoje.UtcDateTime);

        // RBT12 = R$100.000,00 (Serviço); folha = R$30.000,00 -> Fator R = 30% (>= 28%).
        await ambiente.FatoReceitaDiaria.AcumularAsync(BusinessId, hojeData, CorrenteDeReceita.Servico, 100_000_00);

        var parcelas = ContaFinanceiraBase.ParcelaUnica(Money.DeReais(30_000), hoje);
        var folha = ContaAPagar.Criar(
            BusinessId, new SourceRef("payroll", "folha-1"), "Folha 2026-07", CategoriaFinanceiraPadrao.DespesaComPessoal,
            hoje, Money.DeReais(30_000), parcelas).Valor;
        await ambiente.ContasAPagar.SalvarAsync(folha);

        var resultado = await ambiente.Servico.CalcularAsync(BusinessId, AnexoSimplesNacional.I);

        Assert.True(resultado.Sucesso);
        Assert.Equal(0.30, resultado.Valor.FatorR, precision: 4);
    }

    [Fact]
    public async Task Mix_reparte_servico_no_anexo_iii_quando_fator_r_e_alto()
    {
        var hoje = new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);
        var ambiente = NovoAmbiente(hoje);
        var hojeData = DateOnly.FromDateTime(hoje.UtcDateTime);

        await ambiente.FatoReceitaDiaria.AcumularAsync(BusinessId, hojeData, CorrenteDeReceita.Servico, 100_000_00);

        var parcelas = ContaFinanceiraBase.ParcelaUnica(Money.DeReais(40_000), hoje); // Fator R = 40%
        var folha = ContaAPagar.Criar(
            BusinessId, new SourceRef("payroll", "folha-1"), "Folha", CategoriaFinanceiraPadrao.DespesaComPessoal,
            hoje, Money.DeReais(40_000), parcelas).Valor;
        await ambiente.ContasAPagar.SalvarAsync(folha);

        var resultado = await ambiente.Servico.CalcularAsync(BusinessId, AnexoSimplesNacional.I);

        Assert.True(resultado.Sucesso);
        var linha = Assert.Single(resultado.Valor.PorAnexo);
        Assert.Equal(AnexoSimplesNacional.III, linha.Anexo);
        Assert.Equal(100_000_00, linha.ReceitaMesCentavos);
        Assert.Equal(resultado.Valor.ImpostoTotalEstimadoCentavos, linha.ImpostoEstimadoCentavos);
    }

    [Fact]
    public async Task Mix_reparte_servico_no_anexo_v_quando_fator_r_e_baixo()
    {
        var hoje = new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);
        var ambiente = NovoAmbiente(hoje);
        var hojeData = DateOnly.FromDateTime(hoje.UtcDateTime);

        await ambiente.FatoReceitaDiaria.AcumularAsync(BusinessId, hojeData, CorrenteDeReceita.Servico, 100_000_00);

        var parcelas = ContaFinanceiraBase.ParcelaUnica(Money.DeReais(10_000), hoje); // Fator R = 10%
        var folha = ContaAPagar.Criar(
            BusinessId, new SourceRef("payroll", "folha-1"), "Folha", CategoriaFinanceiraPadrao.DespesaComPessoal,
            hoje, Money.DeReais(10_000), parcelas).Valor;
        await ambiente.ContasAPagar.SalvarAsync(folha);

        var resultado = await ambiente.Servico.CalcularAsync(BusinessId, AnexoSimplesNacional.I);

        Assert.True(resultado.Sucesso);
        var linha = Assert.Single(resultado.Valor.PorAnexo);
        Assert.Equal(AnexoSimplesNacional.V, linha.Anexo);
    }

    [Fact]
    public async Task Config_por_tenant_sobrescreve_o_mapeamento_padrao()
    {
        var hoje = new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);
        var ambiente = NovoAmbiente(hoje);
        var hojeData = DateOnly.FromDateTime(hoje.UtcDateTime);

        await ambiente.FatoReceitaDiaria.AcumularAsync(BusinessId, hojeData, CorrenteDeReceita.Recorrente, 50_000_00);

        // Tenant configura Recorrente como Anexo I fixo (ex.: SaaS puro, sem Fator R aplicável) —
        // diferente do padrão (PorFatorR) usado quando ninguém personaliza nada.
        await ambiente.ConfiguracaoRadar.SalvarAsync(BusinessId,
            [new MapeamentoCorrenteAnexo(CorrenteDeReceita.Recorrente, RegraDeEnquadramento.AnexoFixo, AnexoSimplesNacional.I)]);

        var resultado = await ambiente.Servico.CalcularAsync(BusinessId, AnexoSimplesNacional.I);

        Assert.True(resultado.Sucesso);
        var linha = Assert.Single(resultado.Valor.PorAnexo);
        Assert.Equal(AnexoSimplesNacional.I, linha.Anexo);
    }

    [Fact]
    public async Task Anexo_ii_ou_iv_ainda_nao_suportado_devolve_falha_documentada()
    {
        var hoje = new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);
        var ambiente = NovoAmbiente(hoje);

        var resultado = await ambiente.Servico.CalcularAsync(BusinessId, AnexoSimplesNacional.II);

        Assert.True(resultado.Falha);
        Assert.Equal("financeiro.radar_simples.anexo_nao_suportado", resultado.Erro.Codigo);
    }

    [Fact]
    public async Task Sem_anexo_pedido_usa_o_anexo_dominante_do_mix_real_nunca_hardcoda_anexo_i()
    {
        var hoje = new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);
        var ambiente = NovoAmbiente(hoje);
        var hojeData = DateOnly.FromDateTime(hoje.UtcDateTime);

        // Fator R baixo -> serviço no Anexo V; é a ÚNICA corrente com receita -> dominante = V,
        // não I (o hardcode antigo do Consultor).
        await ambiente.FatoReceitaDiaria.AcumularAsync(BusinessId, hojeData, CorrenteDeReceita.Servico, 100_000_00);
        var parcelas = ContaFinanceiraBase.ParcelaUnica(Money.DeReais(5_000), hoje);
        var folha = ContaAPagar.Criar(
            BusinessId, new SourceRef("payroll", "folha-1"), "Folha", CategoriaFinanceiraPadrao.DespesaComPessoal,
            hoje, Money.DeReais(5_000), parcelas).Valor;
        await ambiente.ContasAPagar.SalvarAsync(folha);

        var resultado = await ambiente.Servico.CalcularAsync(BusinessId, anexo: null);

        Assert.True(resultado.Sucesso);
        var faixaV = RadarDoSimplesNacional.AnexoV[resultado.Valor.FaixaAtual - 1];
        var esperado = RadarDoSimplesNacional.CalcularAliquotaEfetiva(resultado.Valor.Rbt12Centavos, faixaV);
        Assert.Equal(esperado, resultado.Valor.AliquotaEfetiva, precision: 6);
    }
}
