using SistemaX.Modules.Financeiro.Application.Quant;
using SistemaX.Modules.Financeiro.Domain.Comum;

namespace SistemaX.Modules.Financeiro.Tests.Quant;

public sealed class RadarDoSimplesNacionalTests
{
    [Fact]
    public void Encontra_a_primeira_faixa_do_anexo_i()
    {
        var faixa = RadarDoSimplesNacional.EncontrarFaixa(RadarDoSimplesNacional.AnexoI, 10_000_00);
        Assert.Equal(1, faixa.Numero);
    }

    [Fact]
    public void Encontra_a_faixa_correta_no_meio_da_tabela()
    {
        // faixa 3: 360.000,01 a 720.000,00
        var faixa = RadarDoSimplesNacional.EncontrarFaixa(RadarDoSimplesNacional.AnexoI, 500_000_00);
        Assert.Equal(3, faixa.Numero);
    }

    [Fact]
    public void Rbt12_acima_do_teto_maximo_cai_na_ultima_faixa()
    {
        var faixa = RadarDoSimplesNacional.EncontrarFaixa(RadarDoSimplesNacional.AnexoI, 999_999_999_00);
        Assert.Equal(6, faixa.Numero);
    }

    [Fact]
    public void Aliquota_efetiva_bate_o_exemplo_oficial_da_lc_123_faixa_2()
    {
        // RBT12 = R$ 300.000,00, faixa 2 (7,30%, deduz R$5.940,00)
        // efetiva = (300000*0.073 - 5940)/300000 = (21900-5940)/300000 = 15960/300000 = 0.0532
        var faixa = RadarDoSimplesNacional.AnexoI[1];
        var efetiva = RadarDoSimplesNacional.CalcularAliquotaEfetiva(300_000_00, faixa);
        Assert.Equal(0.0532, efetiva, precision: 4);
    }

    [Fact]
    public void Aliquota_efetiva_da_faixa_1_e_a_propria_aliquota_nominal_pois_nao_ha_deducao()
    {
        var faixa = RadarDoSimplesNacional.AnexoI[0];
        var efetiva = RadarDoSimplesNacional.CalcularAliquotaEfetiva(100_000_00, faixa);
        Assert.Equal(0.04, efetiva, precision: 4);
    }

    [Fact]
    public void Rbt12_zero_ou_negativo_tem_aliquota_efetiva_zero()
    {
        Assert.Equal(0, RadarDoSimplesNacional.CalcularAliquotaEfetiva(0, RadarDoSimplesNacional.AnexoI[0]));
        Assert.Equal(0, RadarDoSimplesNacional.CalcularAliquotaEfetiva(-100, RadarDoSimplesNacional.AnexoI[0]));
    }

    [Fact]
    public void Distancia_ao_proximo_degrau_e_o_teto_da_faixa_atual_menos_o_rbt12()
    {
        var resultado = RadarDoSimplesNacional.Calcular(170_000_00, RadarDoSimplesNacional.AnexoI, []);
        Assert.Equal(1, resultado.FaixaAtual);
        Assert.Equal(10_000_00, resultado.DistanciaAoProximoDegrauCentavos);
    }

    [Fact]
    public void Sem_receita_mensal_recente_nao_ha_projecao_de_cruzamento()
    {
        var resultado = RadarDoSimplesNacional.Calcular(170_000_00, RadarDoSimplesNacional.AnexoI, []);
        Assert.Null(resultado.MesesProjetadosAteOProximoDegrau);
    }

    [Fact]
    public void Crescimento_negativo_nao_projeta_cruzamento()
    {
        var resultado = RadarDoSimplesNacional.Calcular(170_000_00, RadarDoSimplesNacional.AnexoI, [50_000_00, 40_000_00, 30_000_00]);
        Assert.Null(resultado.MesesProjetadosAteOProximoDegrau);
    }

    [Fact]
    public void Crescimento_positivo_projeta_meses_ate_o_proximo_degrau()
    {
        // P1-1: distância = 10_000_00; crescimento médio mensal g = (2000+2000)/2 = 2000_00.
        // A fórmula ANTIGA (errada) dizia "distância / g = 5 meses" — mas o RBT12 é uma janela
        // móvel de N=3 meses aqui (o tamanho do array de meses fechados), então o incremento REAL
        // por mês é ~N·g, não g: no passo k=1, o mês que ENTRA é 14_000_00+2_000_00=16_000_00 e o
        // que SAI é o mais antigo do array (10_000_00) — incremento de 6_000_00 (=3×2_000_00), não
        // 2_000_00. Isso cruza o teto já no 2º mês, não no 5º.
        var resultado = RadarDoSimplesNacional.Calcular(
            170_000_00, RadarDoSimplesNacional.AnexoI, [10_000_00, 12_000_00, 14_000_00]);

        Assert.Equal(2, resultado.MesesProjetadosAteOProximoDegrau);
    }

    [Fact]
    public void Projecao_usa_o_incremento_real_da_janela_movel_nao_o_crescimento_mensal_simples()
    {
        // P1-1 (docs/financeiro/revisao-domain-fit-cnpj.md): sob crescimento linear sustentado
        // m_t = a + g·t, o incremento REAL do RBT12 (janela móvel de N=12 meses) é
        // m_{t+1} − m_{t−11} ≈ 12g — não g, como a fórmula antiga assumia. Histórico de 12 meses
        // fechados crescendo R$1.000,00/mês (g = 100_000 centavos); RBT12 a exatamente 12g do teto
        // da faixa 1 — ou seja, a UM incremento real de distância.
        const long g = 100_000;
        const long ultimoMesFechado = 5_000_000;
        var ultimosDozeMeses = Enumerable.Range(0, 12).Select(i => ultimoMesFechado - (11 - i) * g).ToList();

        var teto = RadarDoSimplesNacional.AnexoI[0].TetoRbt12Centavos; // 18_000_000
        var rbt12 = teto - 12 * g; // 16_800_000

        var resultado = RadarDoSimplesNacional.Calcular(rbt12, RadarDoSimplesNacional.AnexoI, ultimosDozeMeses);

        // Fórmula ANTIGA (errada): distância / g = (12g) / g = 12 meses — 12× mais devagar que o
        // real. Fórmula CORRETA: cruza já no mês seguinte (k=1), porque o incremento real da
        // janela é 12g, não g.
        Assert.Equal(1, resultado.MesesProjetadosAteOProximoDegrau);
    }

    // ─────────────────────────────────────────────────────────────────────────────────────────
    // P0-4 — Anexo III e V: tabelas oficiais (LC 123/2006 art. 18, redação LC 155/2016). Mesmo
    // RBT12 de R$300.000,00 do exemplo oficial do Anexo I (faixa 2) — conferível pela MESMA
    // fórmula oficial da alíquota efetiva.
    // ─────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Aliquota_efetiva_do_anexo_iii_faixa_2_bate_a_formula_oficial()
    {
        // RBT12 = R$300.000,00, faixa 2 (11,20%, deduz R$9.360,00)
        // efetiva = (300000*0.112 - 9360)/300000 = (33600-9360)/300000 = 24240/300000 = 0.0808
        var faixa = RadarDoSimplesNacional.AnexoIII[1];
        var efetiva = RadarDoSimplesNacional.CalcularAliquotaEfetiva(300_000_00, faixa);
        Assert.Equal(0.0808, efetiva, precision: 4);
    }

    [Fact]
    public void Aliquota_efetiva_do_anexo_v_faixa_2_bate_a_formula_oficial()
    {
        // RBT12 = R$300.000,00, faixa 2 (18,00%, deduz R$4.500,00)
        // efetiva = (300000*0.18 - 4500)/300000 = (54000-4500)/300000 = 49500/300000 = 0.165
        var faixa = RadarDoSimplesNacional.AnexoV[1];
        var efetiva = RadarDoSimplesNacional.CalcularAliquotaEfetiva(300_000_00, faixa);
        Assert.Equal(0.165, efetiva, precision: 4);
    }

    [Fact]
    public void Anexo_iii_e_v_tem_os_mesmos_tetos_de_rbt12_do_anexo_i()
    {
        for (var i = 0; i < RadarDoSimplesNacional.AnexoI.Count; i++)
        {
            Assert.Equal(RadarDoSimplesNacional.AnexoI[i].TetoRbt12Centavos, RadarDoSimplesNacional.AnexoIII[i].TetoRbt12Centavos);
            Assert.Equal(RadarDoSimplesNacional.AnexoI[i].TetoRbt12Centavos, RadarDoSimplesNacional.AnexoV[i].TetoRbt12Centavos);
        }
    }

    [Fact]
    public void ObterTabela_devolve_a_tabela_do_anexo_pedido_e_recusa_anexo_sem_tabela()
    {
        Assert.Same(RadarDoSimplesNacional.AnexoI, RadarDoSimplesNacional.ObterTabela(AnexoSimplesNacional.I));
        Assert.Same(RadarDoSimplesNacional.AnexoIII, RadarDoSimplesNacional.ObterTabela(AnexoSimplesNacional.III));
        Assert.Same(RadarDoSimplesNacional.AnexoV, RadarDoSimplesNacional.ObterTabela(AnexoSimplesNacional.V));
        Assert.Throws<NotSupportedException>(() => RadarDoSimplesNacional.ObterTabela(AnexoSimplesNacional.II));
    }

    // ─────────────────────────────────────────────────────────────────────────────────────────
    // P0-4 — Fator R: LC 123/2006 art. 18 §5º-J — folha 12m ÷ RBT12; ≥28% Anexo III, <28% Anexo V.
    // ─────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Fator_r_e_a_razao_folha_doze_meses_sobre_rbt12()
    {
        Assert.Equal(0.30, FatorR.Calcular(300_000_00, 1_000_000_00), precision: 4);
    }

    [Fact]
    public void Fator_r_com_rbt12_zero_ou_negativo_e_zero()
    {
        Assert.Equal(0, FatorR.Calcular(100_000_00, 0));
        Assert.Equal(0, FatorR.Calcular(100_000_00, -1));
    }

    [Theory]
    [InlineData(0.28, AnexoSimplesNacional.III)]
    [InlineData(0.35, AnexoSimplesNacional.III)]
    [InlineData(1.0, AnexoSimplesNacional.III)]
    public void Fator_r_maior_ou_igual_a_28_por_cento_usa_anexo_iii(double fatorR, AnexoSimplesNacional esperado)
        => Assert.Equal(esperado, FatorR.AnexoDeServicoPorFatorR(fatorR));

    [Theory]
    [InlineData(0.2799)]
    [InlineData(0.10)]
    [InlineData(0)]
    public void Fator_r_menor_que_28_por_cento_usa_anexo_v(double fatorR)
        => Assert.Equal(AnexoSimplesNacional.V, FatorR.AnexoDeServicoPorFatorR(fatorR));

    // ─────────────────────────────────────────────────────────────────────────────────────────
    // P0-4 — CalcularMix: repartição do RBT12/receita do mês por corrente→anexo e soma do DAS.
    // ─────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void CalcularMix_reparte_comercio_servico_e_recorrente_e_soma_o_imposto_total_corretamente()
    {
        // RBT12 = R$300.000,00 — mesmo cenário oficial usado nos testes de alíquota efetiva acima
        // (faixa 2 em qualquer anexo, tetos idênticos): efetiva_I=5,32%, efetiva_III(faixa2)=8,08%.
        const long rbt12 = 300_000_00;
        const double fatorR = 0.30; // ≥28% -> serviço/recorrente caem no Anexo III

        var receitaPorCorrente = new Dictionary<CorrenteDeReceita, long>
        {
            [CorrenteDeReceita.Comercio] = 1_000_00,   // -> Anexo I
            [CorrenteDeReceita.Servico] = 2_000_00,    // -> Anexo III (Fator R >= 28%)
            [CorrenteDeReceita.Recorrente] = 500_00,   // -> Anexo III (mesmo Fator R da empresa)
        };

        var mix = RadarDoSimplesNacional.CalcularMix(
            rbt12, [], fatorR, receitaPorCorrente, MapeamentoCorrenteAnexoPadrao.Obter());

        Assert.Equal(2, mix.FaixaAtual);
        Assert.Equal(2, mix.PorAnexo.Count); // Anexo I (comércio) + Anexo III (serviço+recorrente somados)

        var anexoI = mix.PorAnexo.Single(p => p.Anexo == AnexoSimplesNacional.I);
        Assert.Equal(1_000_00, anexoI.ReceitaMesCentavos);
        Assert.Equal(5_320, anexoI.ImpostoEstimadoCentavos); // 100.000 * 0,0532

        var anexoIii = mix.PorAnexo.Single(p => p.Anexo == AnexoSimplesNacional.III);
        Assert.Equal(2_500_00, anexoIii.ReceitaMesCentavos); // 2.000+500 somados no mesmo anexo
        Assert.Equal(20_200, anexoIii.ImpostoEstimadoCentavos); // 250.000 * 0,0808

        Assert.Equal(5_320 + 20_200, mix.ImpostoTotalEstimadoCentavos);
    }

    [Fact]
    public void CalcularMix_com_fator_r_baixo_reparte_servico_no_anexo_v()
    {
        const long rbt12 = 300_000_00;
        const double fatorR = 0.10; // <28% -> serviço cai no Anexo V

        var receitaPorCorrente = new Dictionary<CorrenteDeReceita, long> { [CorrenteDeReceita.Servico] = 1_000_00 };

        var mix = RadarDoSimplesNacional.CalcularMix(rbt12, [], fatorR, receitaPorCorrente, MapeamentoCorrenteAnexoPadrao.Obter());

        var anexoV = Assert.Single(mix.PorAnexo);
        Assert.Equal(AnexoSimplesNacional.V, anexoV.Anexo);
        Assert.Equal(16_500, anexoV.ImpostoEstimadoCentavos); // 100.000 * 0,165
    }

    [Fact]
    public void CalcularMix_sem_receita_nenhuma_devolve_lista_vazia_e_imposto_zero()
    {
        var mix = RadarDoSimplesNacional.CalcularMix(0, [], 0, new Dictionary<CorrenteDeReceita, long>(), MapeamentoCorrenteAnexoPadrao.Obter());

        Assert.Empty(mix.PorAnexo);
        Assert.Equal(0, mix.ImpostoTotalEstimadoCentavos);
        Assert.Equal(1, mix.FaixaAtual);
    }

    [Fact]
    public void MapeamentoCorrenteAnexo_com_regra_anexofixo_sem_anexo_definido_lanca()
    {
        var mapeamento = new MapeamentoCorrenteAnexo(CorrenteDeReceita.Comercio, RegraDeEnquadramento.AnexoFixo);
        Assert.Throws<InvalidOperationException>(() => mapeamento.Resolver(0));
    }
}
