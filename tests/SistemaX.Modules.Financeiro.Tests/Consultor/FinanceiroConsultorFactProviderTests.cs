using System.Globalization;
using SistemaX.Modules.Abstractions.Consultor;
using SistemaX.Modules.Financeiro.Application.Consultor;
using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Application.Quant;
using SistemaX.Modules.Financeiro.Application.ReadModels;
using SistemaX.Modules.Financeiro.Domain.Caixa;
using SistemaX.Modules.Financeiro.Domain.Comum;
using SistemaX.Modules.Financeiro.Domain.ContasAPagarReceber;
using SistemaX.Modules.Financeiro.Domain.Recorrencia;
using SistemaX.Modules.Financeiro.Infrastructure.InMemory;
using SistemaX.Modules.Financeiro.Tests.Fakes;
using SistemaX.SharedKernel;
using RecorrenciaAgg = SistemaX.Modules.Financeiro.Domain.Recorrencia.Recorrencia;

namespace SistemaX.Modules.Financeiro.Tests.Consultor;

/// <summary>
/// Fase 2 do plano de inteligência do Financeiro (docs/financeiro/inteligencia-arquitetura.md
/// §3.5/ADR-0005) — o Super Consultor DETERMINÍSTICO: <see cref="FinanceiroConsultorFactProvider"/>
/// não calcula nada de novo, só reaproveita os read-models da F1 já testados
/// (<c>Quant/*Tests.cs</c>) e formata. Estes testes travam DUAS coisas por regra: (1) com dado
/// conhecido, o fato/score/frase batem EXATAMENTE com o esperado (nada de LLM, é matemática pura);
/// (2) sem dado nenhum, a regra devolve mensagem NEUTRA — nunca lança, nunca formata um número sem
/// sentido (ex.: <c>long.MaxValue</c> como moeda).
/// </summary>
public class FinanceiroConsultorFactProviderTests
{
    private const string BusinessId = "business-consultor-teste";
    private static readonly CultureInfo CulturaPtBr = CultureInfo.GetCultureInfo("pt-BR");

    private sealed record Ambiente(
        FinanceiroConsultorFactProvider Provider,
        InMemoryFatoCaixaDiarioRepository FatoCaixaDiario,
        InMemoryContaAPagarRepository ContasAPagar,
        InMemoryContaAReceberRepository ContasAReceber,
        InMemoryMovimentoFinanceiroRepository Movimentos,
        InMemoryRecorrenciaRepository Recorrencias,
        InMemoryFatoCustoDiarioRepository FatoCustoDiario,
        InMemoryFatoReceitaDiariaRepository FatoReceitaDiaria,
        FakeRelogio Relogio);

    private static Ambiente NovoAmbiente(DateTimeOffset agora)
    {
        var relogio = new FakeRelogio(agora);
        var fatoCaixaDiario = new InMemoryFatoCaixaDiarioRepository();
        var contasAPagar = new InMemoryContaAPagarRepository();
        var contasAReceber = new InMemoryContaAReceberRepository();
        var movimentos = new InMemoryMovimentoFinanceiroRepository();
        var recorrencias = new InMemoryRecorrenciaRepository();
        var fatoCustoDiario = new InMemoryFatoCustoDiarioRepository();
        var fatoReceitaDiaria = new InMemoryFatoReceitaDiariaRepository();
        var fatoRecebiveis = new InMemoryFatoRecebiveisRepository();

        var previsaoDeCaixa = new PrevisaoDeCaixaService(fatoCaixaDiario, contasAReceber, contasAPagar, movimentos, new InMemoryFormaDePagamentoRepository(), relogio);
        var dreGerencial = new DreGerencialService(contasAReceber, contasAPagar, fatoCustoDiario, fatoRecebiveis);
        var pontoDeEquilibrio = new PontoDeEquilibrioService(recorrencias, fatoReceitaDiaria, dreGerencial, relogio);
        var inadimplencia = new InadimplenciaService(contasAReceber, relogio);
        var radarDoSimples = new RadarDoSimplesService(fatoReceitaDiaria, contasAPagar, new InMemoryConfiguracaoRadarSimplesRepository(), relogio);

        var provider = new FinanceiroConsultorFactProvider(
            previsaoDeCaixa, pontoDeEquilibrio, inadimplencia, radarDoSimples,
            contasAPagar, contasAReceber, movimentos, relogio);

        return new Ambiente(
            provider, fatoCaixaDiario, contasAPagar, contasAReceber, movimentos,
            recorrencias, fatoCustoDiario, fatoReceitaDiaria, relogio);
    }

    private static PeriodoRef Periodo(DateTimeOffset hoje) => new(BusinessId, DateOnly.FromDateTime(hoje.UtcDateTime));

    // ─────────────────────────────────────────────────────────────────────────────────────────
    // CASO DE BORDA — sem dado nenhum: nunca crasha, tudo neutro.
    // ─────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ColetarAsync_SemNenhumDado_DevolveSoFatosNeutros_NuncaCrasha()
    {
        var hoje = new DateTimeOffset(2026, 3, 15, 12, 0, 0, TimeSpan.Zero);
        var ambiente = NovoAmbiente(hoje);

        var fatos = await ambiente.Provider.ColetarAsync(Periodo(hoje));

        // Os 4 read-models sempre produzem fato (mesmo neutro); radar entra também (Anexo I
        // sempre "Sucesso"); o sinal cross-recurso NÃO entra — não há conta a pagar nenhuma.
        Assert.Equal(5, fatos.Count);
        Assert.DoesNotContain(fatos, f => f.RuleId == "fin.conta-grande-antes-de-receber");

        var runway = fatos.Single(f => f.RuleId == "fin.runway");
        Assert.Equal(0, runway.Score);
        Assert.Equal("Seu caixa não está queimando no ritmo atual — nenhum sinal de risco de ficar sem dinheiro.", runway.TemplateFallback);

        var previsao = fatos.Single(f => f.RuleId == "fin.previsao-caixa");
        Assert.Equal(0, previsao.Score);
        Assert.Contains("não fica negativo", previsao.TemplateFallback);

        var breakeven = fatos.Single(f => f.RuleId == "fin.breakeven");
        Assert.Equal(0, breakeven.Score);
        Assert.Equal("Ainda não há vendas suficientes com margem calculada para estimar o ponto de equilíbrio deste mês.", breakeven.TemplateFallback);

        var inad = fatos.Single(f => f.RuleId == "fin.inadimplencia");
        Assert.Equal(0, inad.Score);
        Assert.Equal("Você não tem contas a receber em aberto no momento.", inad.TemplateFallback);

        var radar = fatos.Single(f => f.RuleId == "fin.radar-simples");
        Assert.Equal("1", radar.Facts["faixaAtual"]);

        // Todo fato tem drill de leitura pra própria tela (Lei 2 — navegação read-only).
        Assert.All(fatos, f => Assert.NotNull(f.Drill));
    }

    // ─────────────────────────────────────────────────────────────────────────────────────────
    // #2/#1 — Runway + previsão de caixa: histórico de queima CONSTANTE não tem variância, então
    // o bootstrap em blocos (que resample o mesmo valor repetido) produz uma trajetória 100%
    // determinística — dá pra calcular a mão, sem depender de tolerância estatística.
    // ─────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ColetarAsync_ComQueimaDeCaixaConstante_CalculaRunwayEProbabilidadeExatos()
    {
        var hoje = new DateTimeOffset(2026, 3, 15, 12, 0, 0, TimeSpan.Zero);
        var ambiente = NovoAmbiente(hoje);

        // 91 dias de histórico (hoje-90..hoje), TODOS com saída de R$1.000,00/dia — burn EWMA e
        // trajetória de bootstrap ficam exatamente iguais a essa constante (sem ruído nenhum).
        var hojeData = DateOnly.FromDateTime(hoje.UtcDateTime);
        for (var dia = hojeData.AddDays(-90); dia <= hojeData; dia = dia.AddDays(1))
        {
            await ambiente.FatoCaixaDiario.AcumularSaidaAsync(BusinessId, dia, 100_000);
        }

        // Saldo atual de R$5.000,00 — a R$1.000,00/dia, o saldo cruza negativo no dia 6
        // (dia1=4000, dia2=3000, dia3=2000, dia4=1000, dia5=0, dia6=-1000).
        var movimento = MovimentoFinanceiro.Registrar(
            BusinessId, "caixa-1", "pix", "parcela-saldo", "conta-saldo",
            TipoMovimentoFinanceiro.Entrada, Money.DeReais(5_000), hoje, new SourceRef("teste", "saldo-inicial"));
        await ambiente.Movimentos.SalvarAsync(movimento.Valor);

        var fatos = await ambiente.Provider.ColetarAsync(Periodo(hoje));

        var runway = fatos.Single(f => f.RuleId == "fin.runway");
        Assert.Equal("6", runway.Facts["runwayDias"]);
        Assert.Equal("realista", runway.Facts["runwayOrigem"]);
        Assert.Equal("No ritmo atual, seu caixa aguenta cerca de 6 dias sem novas vendas (runway realista).", runway.TemplateFallback);
        Assert.Equal((int)Math.Round(10_000.0 / 7), runway.Score);

        var previsao = fatos.Single(f => f.RuleId == "fin.previsao-caixa");
        var probEsperada = 1.0.ToString("P0", CulturaPtBr);
        Assert.Equal(probEsperada, previsao.Facts["probabilidadeSaldoNegativo30d"]);
        Assert.Equal(hojeData.AddDays(6).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture), previsao.Facts["primeiroDiaNegativoProvavel"]);
        Assert.Equal(10_000, previsao.Score);
        Assert.Equal(
            $"Há {probEsperada} de chance do seu caixa ficar negativo até {hojeData.AddDays(6):dd/MM/yyyy}.",
            previsao.TemplateFallback);
    }

    // ─────────────────────────────────────────────────────────────────────────────────────────
    // #7 — Ponto de equilíbrio: bate exatamente no 3º dia com dados (MC 50%, custos fixos batem
    // com a margem de contribuição acumulada até o dia 3).
    // ─────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ColetarAsync_ComCustosFixosEMargemConhecidos_CalculaBreakevenNoDiaCerto()
    {
        var hoje = new DateTimeOffset(2026, 3, 5, 12, 0, 0, TimeSpan.Zero);
        var ambiente = NovoAmbiente(hoje);

        // Custos fixos: uma recorrência mensal a pagar de R$3.000,00.
        var recorrencia = RecorrenciaAgg.Criar(
            BusinessId, "Aluguel", TipoContaRecorrente.APagar, Money.DeReais(3_000),
            "aluguel", FrequenciaRecorrencia.Mensal, hoje.AddMonths(-6)).Valor;
        await ambiente.Recorrencias.SalvarAsync(recorrencia);

        // Margem de contribuição 50% (P1-2 — blended por mix, aqui com uma corrente só): uma venda
        // de R$2.000,00 (ContaAReceber, corrente Comercio) com CMV real de R$1.000,00 já foldado em
        // fato_custo_diario — o mesmo insumo que DreGerencialService.PorCorrente usa.
        var hojeData = DateOnly.FromDateTime(hoje.UtcDateTime);
        var vendaMc = ContaAReceber.Criar(
            BusinessId, new SourceRef("teste", "venda-mc"), "Venda MC", "servicos",
            hoje, Money.DeReais(2_000), ContaFinanceiraBase.ParcelaUnica(Money.DeReais(2_000), hoje),
            corrente: CorrenteDeReceita.Comercio).Valor;
        await ambiente.ContasAReceber.SalvarAsync(vendaMc);
        await ambiente.FatoCustoDiario.AcumularAsync(BusinessId, hojeData, CorrenteDeReceita.Comercio, 100_000);

        // Receita diária do mês: R$2.000,00/dia nos dias 1, 2 e 3 — MC acumulada bate os
        // R$3.000,00 de custo fixo EXATAMENTE no dia 3 (1000 + 1000 + 1000 = 3000).
        var inicioDoMes = new DateOnly(hoje.Year, hoje.Month, 1);
        for (var d = 0; d < 3; d++)
        {
            await ambiente.FatoReceitaDiaria.AcumularAsync(BusinessId, inicioDoMes.AddDays(d), CorrenteDeReceita.Comercio, 200_000);
        }

        var fatos = await ambiente.Provider.ColetarAsync(Periodo(hoje));
        var breakeven = fatos.Single(f => f.RuleId == "fin.breakeven");

        Assert.Equal("3", breakeven.Facts["diaDoEquilibrio"]);
        Assert.Equal(new Money(300_000).Formatado(), breakeven.Facts["custosFixosMensais"]);
        Assert.Equal(100, breakeven.Score); // já atingido no mês -> score baixo, informativo
        Assert.Equal(
            $"Você já bateu o ponto de equilíbrio este mês no dia 3 — a partir daqui, o que entra é lucro (custos fixos de {new Money(300_000).Formatado()}).",
            breakeven.TemplateFallback);
    }

    // ─────────────────────────────────────────────────────────────────────────────────────────
    // #3 — Inadimplência: uma parcela vencida há 45 dias (faixa 31-60, taxa padrão 10%).
    // ─────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ColetarAsync_ComParcelaAtrasada45Dias_CalculaProvisaoDaFaixaCorreta()
    {
        var hoje = new DateTimeOffset(2026, 3, 15, 12, 0, 0, TimeSpan.Zero);
        var ambiente = NovoAmbiente(hoje);

        var vencimento = hoje.AddDays(-45);
        var parcelas = ContaFinanceiraBase.ParcelaUnica(Money.DeReais(1_000), vencimento);
        var conta = ContaAReceber.Criar(
            BusinessId, new SourceRef("teste", "conta-atrasada"), "Serviço prestado", "servicos", vencimento, Money.DeReais(1_000), parcelas).Valor;
        await ambiente.ContasAReceber.SalvarAsync(conta);

        var fatos = await ambiente.Provider.ColetarAsync(Periodo(hoje));
        var inad = fatos.Single(f => f.RuleId == "fin.inadimplencia");

        // Faixa De31a60Dias -> taxa padrão 10% -> provisão = R$100,00; líquido = R$900,00.
        Assert.Equal(new Money(100_000).Formatado(), inad.Facts["valorEmAberto"]);
        Assert.Equal(new Money(10_000).Formatado(), inad.Facts["provisaoEsperada"]);
        Assert.Equal(new Money(90_000).Formatado(), inad.Facts["valorLiquidoEsperado"]);
        Assert.Equal(10, inad.Score);
    }

    // ─────────────────────────────────────────────────────────────────────────────────────────
    // #4 — Radar do Simples: RBT12 de R$500.000,00 cai na faixa 3 do Anexo I; sem tendência de
    // crescimento nos últimos meses fechados (só há dado no mês corrente).
    // ─────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ColetarAsync_ComRbt12NaFaixa3_CalculaAliquotaEfetivaCorreta()
    {
        var hoje = new DateTimeOffset(2026, 3, 15, 12, 0, 0, TimeSpan.Zero);
        var ambiente = NovoAmbiente(hoje);

        // P2-1: RBT12 são os 12 meses FECHADOS anteriores ao mês corrente — a receita precisa estar
        // num mês já fechado (fevereiro/2026), não no mês corrente (março, ainda em curso).
        var mesFechado = DateOnly.FromDateTime(hoje.AddMonths(-1).UtcDateTime);
        await ambiente.FatoReceitaDiaria.AcumularAsync(BusinessId, mesFechado, CorrenteDeReceita.Comercio, 50_000_000); // R$500.000,00

        var fatos = await ambiente.Provider.ColetarAsync(Periodo(hoje));
        var radar = fatos.Single(f => f.RuleId == "fin.radar-simples");

        // Mesma fórmula fechada do motor puro (LC 123/2006 art. 18 §1º-A), usada aqui só para
        // computar o valor esperado — a lógica testada é a do FACT PROVIDER (que Facts/Score ele
        // deriva desse resultado), não a fórmula em si (já coberta por RadarDoSimplesNacionalTests).
        var esperado = RadarDoSimplesNacional.Calcular(50_000_000, RadarDoSimplesNacional.AnexoI, []);
        Assert.Equal(3, esperado.FaixaAtual);
        Assert.Null(esperado.MesesProjetadosAteOProximoDegrau);

        Assert.Equal("3", radar.Facts["faixaAtual"]);
        Assert.Equal(esperado.AliquotaEfetiva.ToString("P1", CulturaPtBr), radar.Facts["aliquotaEfetiva"]);
        Assert.DoesNotContain("mesesAteProximaFaixa", radar.Facts.Keys);
        Assert.Equal(50, radar.Score);
    }

    // ─────────────────────────────────────────────────────────────────────────────────────────
    // Sinal óbvio cross-recurso: "conta grande vence antes de receber X".
    // ─────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ColetarAsync_ComContaAPagarGrandeAntesDoRecebimento_EmiteSinalDeAlerta()
    {
        var hoje = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);
        var ambiente = NovoAmbiente(hoje);

        // Saldo atual pequeno: R$500,00 — não cobre a conta a pagar abaixo.
        var movimentoSaldo = MovimentoFinanceiro.Registrar(
            BusinessId, "caixa-1", "pix", "parcela-saldo", "conta-saldo",
            TipoMovimentoFinanceiro.Entrada, Money.DeReais(500), hoje, new SourceRef("teste", "saldo"));
        await ambiente.Movimentos.SalvarAsync(movimentoSaldo.Valor);

        // Conta a pagar grande (R$10.000,00), vence em 5 dias.
        var vencimentoPagar = hoje.AddDays(5);
        var parcelasPagar = ContaFinanceiraBase.ParcelaUnica(Money.DeReais(10_000), vencimentoPagar);
        var contaPagar = ContaAPagar.Criar(
            BusinessId, new SourceRef("teste", "aluguel-grande"), "Aluguel atrasado", "aluguel", hoje, Money.DeReais(10_000), parcelasPagar).Valor;
        await ambiente.ContasAPagar.SalvarAsync(contaPagar);

        // Recebimento esperado, mas só DEPOIS da conta a pagar (dia 10).
        var vencimentoReceber = hoje.AddDays(10);
        var parcelasReceber = ContaFinanceiraBase.ParcelaUnica(Money.DeReais(8_000), vencimentoReceber);
        var contaReceber = ContaAReceber.Criar(
            BusinessId, new SourceRef("teste", "recebimento-grande"), "Cliente XYZ", "servicos", hoje, Money.DeReais(8_000), parcelasReceber).Valor;
        await ambiente.ContasAReceber.SalvarAsync(contaReceber);

        var fatos = await ambiente.Provider.ColetarAsync(Periodo(hoje));
        var sinal = fatos.SingleOrDefault(f => f.RuleId == "fin.conta-grande-antes-de-receber");

        // P2-3: a matemática vem de SinalContaGrandeAntesDoRecebimento.Detectar — caixa PROJETADO
        // até o vencimento da maior conta a pagar (saldo + entradas antes − saídas antes, exceto
        // ela mesma). O recebimento de R$8.000,00 vence DEPOIS (dia 10 > dia 5) — não entra na
        // projeção até o dia 5: caixa projetado = só o saldo (R$500,00), falta R$9.500,00.
        Assert.NotNull(sinal);
        Assert.Equal("bancario", sinal!.Tela);
        Assert.Equal("Aluguel atrasado", sinal.Facts["contaAPagarDescricao"]);
        Assert.Equal(new Money(1_000_000).Formatado(), sinal.Facts["contaAPagarValor"]);
        Assert.Equal(new Money(500_00).Formatado(), sinal.Facts["caixaProjetadoAteLa"]);
        Assert.Equal(new Money(950_000).Formatado(), sinal.Facts["falta"]);
        Assert.Contains("Aluguel atrasado", sinal.TemplateFallback);
    }

    [Fact]
    public async Task ColetarAsync_ComSaldoSuficiente_NaoEmiteSinalDeAlerta()
    {
        var hoje = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);
        var ambiente = NovoAmbiente(hoje);

        // Saldo cobre a conta folgadamente.
        var movimentoSaldo = MovimentoFinanceiro.Registrar(
            BusinessId, "caixa-1", "pix", "parcela-saldo", "conta-saldo",
            TipoMovimentoFinanceiro.Entrada, Money.DeReais(50_000), hoje, new SourceRef("teste", "saldo"));
        await ambiente.Movimentos.SalvarAsync(movimentoSaldo.Valor);

        var vencimentoPagar = hoje.AddDays(5);
        var parcelasPagar = ContaFinanceiraBase.ParcelaUnica(Money.DeReais(10_000), vencimentoPagar);
        var contaPagar = ContaAPagar.Criar(
            BusinessId, new SourceRef("teste", "aluguel"), "Aluguel", "aluguel", hoje, Money.DeReais(10_000), parcelasPagar).Valor;
        await ambiente.ContasAPagar.SalvarAsync(contaPagar);

        var fatos = await ambiente.Provider.ColetarAsync(Periodo(hoje));

        Assert.DoesNotContain(fatos, f => f.RuleId == "fin.conta-grande-antes-de-receber");
    }

    [Fact]
    public async Task ColetarAsync_ComRecebimentoAntesDaContaAPagar_NaoEmiteSinalDeAlerta()
    {
        var hoje = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);
        var ambiente = NovoAmbiente(hoje);

        var movimentoSaldo = MovimentoFinanceiro.Registrar(
            BusinessId, "caixa-1", "pix", "parcela-saldo", "conta-saldo",
            TipoMovimentoFinanceiro.Entrada, Money.DeReais(500), hoje, new SourceRef("teste", "saldo"));
        await ambiente.Movimentos.SalvarAsync(movimentoSaldo.Valor);

        // Conta a pagar vence dia 10; recebimento chega ANTES (dia 5) E, somado ao saldo, COBRE a
        // conta — a projeção acumulada (SinalContaGrandeAntesDoRecebimento.Detectar, P2-3) é
        // saldo(500) + recebimento(10.000) = 10.500 >= 10.000 -> falta <= 0, não é sinal de alerta.
        var vencimentoPagar = hoje.AddDays(10);
        var parcelasPagar = ContaFinanceiraBase.ParcelaUnica(Money.DeReais(10_000), vencimentoPagar);
        var contaPagar = ContaAPagar.Criar(
            BusinessId, new SourceRef("teste", "aluguel"), "Aluguel", "aluguel", hoje, Money.DeReais(10_000), parcelasPagar).Valor;
        await ambiente.ContasAPagar.SalvarAsync(contaPagar);

        var vencimentoReceber = hoje.AddDays(5);
        var parcelasReceber = ContaFinanceiraBase.ParcelaUnica(Money.DeReais(10_000), vencimentoReceber);
        var contaReceber = ContaAReceber.Criar(
            BusinessId, new SourceRef("teste", "recebimento"), "Cliente ABC", "servicos", hoje, Money.DeReais(10_000), parcelasReceber).Valor;
        await ambiente.ContasAReceber.SalvarAsync(contaReceber);

        var fatos = await ambiente.Provider.ColetarAsync(Periodo(hoje));

        Assert.DoesNotContain(fatos, f => f.RuleId == "fin.conta-grande-antes-de-receber");
    }
}
