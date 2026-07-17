using System.Globalization;
using SistemaX.Modules.Abstractions.Consultor;
using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Application.ReadModels;
using SistemaX.Modules.Financeiro.Domain.Comum;
using SistemaX.Modules.Financeiro.Domain.ContasAPagarReceber;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Application.Consultor;

/// <summary>
/// Implementação do Financeiro para <see cref="IConsultorFactProvider"/> — Fase 2 do plano de
/// inteligência do Financeiro (docs/financeiro/inteligencia-arquitetura.md §3.5/ADR-0005): "cada
/// módulo registra o seu via DI (R5)". Este provider NÃO calcula nada — só reaproveita os
/// read-models quant da F1 (<see cref="PrevisaoDeCaixaService"/>, <see cref="PontoDeEquilibrioService"/>,
/// <see cref="InadimplenciaService"/>, <see cref="RadarDoSimplesService"/>) e formata os números já
/// prontos em <see cref="ConsultorFato.Facts"/> — o LLM (ainda não ligado nesta rodada, ver
/// <c>NarradorTemplate</c>) nunca vê um valor cru.
///
/// Além dos 4 read-models, coleta um SINAL ÓBVIO cross-recurso dentro do próprio módulo (não
/// cross-MÓDULO — não fere R5/Lei 2): "a maior conta a pagar que vence em breve é maior que o
/// caixa disponível e vence antes do maior recebimento esperado" (<see cref="ColetarSinalContaGrandeAntesDeReceberAsync"/>).
///
/// Cada regra abaixo é determinística e sempre produz um <see cref="ConsultorFato"/> (nunca
/// lança), mesmo com dado zerado/ausente — a mensagem nesse caso é neutra (ex.: "sem contas a
/// receber em aberto"), nunca um crash ou um número sem sentido (ex.: <c>long.MaxValue</c>
/// formatado como moeda). É o requisito "casos de borda → mensagem neutra, não crash" da tarefa.
/// </summary>
public sealed class FinanceiroConsultorFactProvider(
    PrevisaoDeCaixaService previsaoDeCaixa,
    PontoDeEquilibrioService pontoDeEquilibrio,
    InadimplenciaService inadimplencia,
    RadarDoSimplesService radarDoSimples,
    IContaAPagarRepository contasAPagar,
    IContaAReceberRepository contasAReceber,
    IMovimentoFinanceiroRepository movimentos,
    IRelogio relogio) : IConsultorFactProvider
{
    private const string Modulo = "financeiro";

    /// <summary>Horizonte-padrão de 30 dias — usado tanto pela projeção de caixa (#1/#2) quanto
    /// pelo sinal "conta grande vence antes de receber X" (mesma janela de curto prazo em que uma
    /// PME de fato sente o aperto de caixa).</summary>
    private const int HorizonteDiasPadrao = 30;

    private static readonly CultureInfo CulturaPtBr = CultureInfo.GetCultureInfo("pt-BR");

    public async Task<IReadOnlyList<ConsultorFato>> ColetarAsync(PeriodoRef periodo, CancellationToken ct = default)
    {
        var businessId = periodo.BusinessId;

        var previsao = await previsaoDeCaixa.CalcularAsync(businessId, HorizonteDiasPadrao, ct).ConfigureAwait(false);
        var breakeven = await pontoDeEquilibrio.CalcularAsync(businessId, ct).ConfigureAwait(false);
        var inad = await inadimplencia.CalcularAsync(businessId, ct).ConfigureAwait(false);
        // P0-4 (docs/financeiro/revisao-domain-fit-cnpj.md): NÃO passa mais Anexo I hardcoded — o
        // Radar resolve o mix real do tenant (config real de corrente→anexo + Fator R) e o
        // Consultor lê a quebra multi-anexo em ColetarRadarDoSimples.
        var radar = await radarDoSimples.CalcularAsync(businessId, anexo: null, ct).ConfigureAwait(false);

        var fatos = new List<ConsultorFato>
        {
            ColetarRunway(previsao),
            ColetarPrevisaoDeCaixa(previsao),
            ColetarBreakeven(breakeven),
            ColetarInadimplencia(inad),
        };

        if (radar.Sucesso)
        {
            fatos.Add(ColetarRadarDoSimples(radar.Valor));
        }

        var sinal = await ColetarSinalContaGrandeAntesDeReceberAsync(businessId, ct).ConfigureAwait(false);
        if (sinal is not null)
        {
            fatos.Add(sinal);
        }

        return fatos;
    }

    /// <summary>Catálogo #2 (runway) — "quantos dias eu aguento se parar de vender?". Prioriza o
    /// runway REALISTA (já incorpora recebíveis/pagáveis agendados); cai para o BRUTO só quando o
    /// realista não existe (nenhum dia P50 fica negativo no horizonte simulado). Sem burn
    /// detectável em nenhum dos dois, é uma boa notícia — mensagem neutra, score baixo.</summary>
    private static ConsultorFato ColetarRunway(PrevisaoDeCaixaResultado previsao)
    {
        const string ruleId = "fin.runway";
        const string tela = "visao-geral";

        var (dias, origem) = previsao.DiasRunwayRealista is { } realista
            ? (realista, "realista")
            : previsao.DiasRunwayBruto is { } bruto
                ? (bruto, "bruto (sem considerar recebíveis futuros)")
                : ((int?)null, string.Empty);

        if (dias is null)
        {
            return new ConsultorFato(
                Modulo, ruleId, tela, Score: 0,
                Facts: new Dictionary<string, string> { ["runway"] = "sem queima de caixa detectada" },
                TemplateFallback: "Seu caixa não está queimando no ritmo atual — nenhum sinal de risco de ficar sem dinheiro.",
                Drill: new DrillTarget(tela));
        }

        var score = Clamp0a10000((long)Math.Round(10_000.0 / (dias.Value + 1)));
        var diasTexto = dias.Value.ToString(CultureInfo.InvariantCulture);
        var frase = dias.Value <= 0
            ? "Seu caixa já está negativo (ou fica hoje mesmo) no cenário atual — ação imediata é recomendada."
            : $"No ritmo atual, seu caixa aguenta cerca de {diasTexto} dias sem novas vendas (runway {origem}).";

        return new ConsultorFato(
            Modulo, ruleId, tela, score,
            Facts: new Dictionary<string, string>
            {
                ["runwayDias"] = diasTexto,
                ["runwayOrigem"] = origem,
            },
            TemplateFallback: frase,
            Drill: new DrillTarget(tela));
    }

    /// <summary>Catálogo #1 (bandas P5/P50/P95) — "quando fico sem caixa? com que certeza?". Score
    /// escala diretamente com a probabilidade (0–100% → 0–10.000) — quanto mais provável o caixa
    /// negativo, mais urgente o card.</summary>
    private static ConsultorFato ColetarPrevisaoDeCaixa(PrevisaoDeCaixaResultado previsao)
    {
        const string ruleId = "fin.previsao-caixa";
        const string tela = "fluxo-caixa";

        var probPct = previsao.ProbabilidadeSaldoNegativoEm30Dias.ToString("P0", CulturaPtBr);
        var score = Clamp0a10000((long)Math.Round(previsao.ProbabilidadeSaldoNegativoEm30Dias * 10_000));

        var frase = previsao.PrimeiroDiaP50Negativo is { } dia
            ? $"Há {probPct} de chance do seu caixa ficar negativo até {dia.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}."
            : $"No cenário mais provável, seu caixa não fica negativo nos próximos {HorizonteDiasPadrao} dias (chance estimada de {probPct}).";

        return new ConsultorFato(
            Modulo, ruleId, tela, score,
            Facts: new Dictionary<string, string>
            {
                ["probabilidadeSaldoNegativo30d"] = probPct,
                ["primeiroDiaNegativoProvavel"] = previsao.PrimeiroDiaP50Negativo?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "nenhum no horizonte",
            },
            TemplateFallback: frase,
            Drill: new DrillTarget(tela));
    }

    /// <summary>Catálogo #7 (ponto de equilíbrio vivo) — "quanto preciso vender por dia?". Sem
    /// margem de contribuição medida ainda (nenhuma venda com CMV real na janela), o motor devolve
    /// <c>long.MaxValue</c> para "receita necessária" (ver <see cref="BreakevenMensal"/>) — formatar
    /// isso como moeda seria um número sem sentido pro leigo, então esse caso vira mensagem neutra
    /// ANTES de tocar em <see cref="Money"/>.</summary>
    private static ConsultorFato ColetarBreakeven(PontoDeEquilibrioResultado breakeven)
    {
        const string ruleId = "fin.breakeven";
        const string tela = "recorrentes";

        if (breakeven.MargemContribuicaoPercentual <= 0)
        {
            return new ConsultorFato(
                Modulo, ruleId, tela, Score: 0,
                Facts: new Dictionary<string, string> { ["margemContribuicao"] = "sem dado suficiente" },
                TemplateFallback: "Ainda não há vendas suficientes com margem calculada para estimar o ponto de equilíbrio deste mês.",
                Drill: new DrillTarget(tela));
        }

        var custosFixos = new Money(breakeven.CustosFixosMensaisCentavos).Formatado();
        var receitaDiaria = new Money(breakeven.ReceitaNecessariaDiariaCentavos).Formatado();
        var mcPct = breakeven.MargemContribuicaoPercentual.ToString("P1", CulturaPtBr);

        if (breakeven.JaAtingiuNoMes && breakeven.DiaDoEquilibrio is { } diaAtingido)
        {
            return new ConsultorFato(
                Modulo, ruleId, tela, Score: 100,
                Facts: new Dictionary<string, string>
                {
                    ["diaDoEquilibrio"] = diaAtingido.ToString(CultureInfo.InvariantCulture),
                    ["custosFixosMensais"] = custosFixos,
                },
                TemplateFallback: $"Você já bateu o ponto de equilíbrio este mês no dia {diaAtingido} — a partir daqui, o que entra é lucro (custos fixos de {custosFixos}).",
                Drill: new DrillTarget(tela));
        }

        if (breakeven.DiaDoEquilibrio is { } diaProjetado)
        {
            var score = Clamp0a10000(diaProjetado * 30);
            return new ConsultorFato(
                Modulo, ruleId, tela, score,
                Facts: new Dictionary<string, string>
                {
                    ["diaDoEquilibrio"] = diaProjetado.ToString(CultureInfo.InvariantCulture),
                    ["receitaNecessariaDiaria"] = receitaDiaria,
                    ["margemContribuicao"] = mcPct,
                },
                TemplateFallback: $"No ritmo atual, você deve bater o ponto de equilíbrio por volta do dia {diaProjetado} — precisa vender {receitaDiaria}/dia (margem de contribuição de {mcPct}).",
                Drill: new DrillTarget(tela));
        }

        return new ConsultorFato(
            Modulo, ruleId, tela, Score: 8_000,
            Facts: new Dictionary<string, string>
            {
                ["custosFixosMensais"] = custosFixos,
                ["receitaNecessariaDiaria"] = receitaDiaria,
                ["margemContribuicao"] = mcPct,
            },
            TemplateFallback: $"No ritmo atual do mês, suas vendas não são suficientes para cobrir os custos fixos de {custosFixos} — faltam vender {receitaDiaria}/dia a mais para bater o ponto de equilíbrio.",
            Drill: new DrillTarget(tela));
    }

    /// <summary>Catálogo #3 (score de inadimplência) — "esse 'a receber' vale quanto de verdade?".
    /// Score escala com o valor em risco (provisão esperada), não com a contagem de parcelas — uma
    /// única parcela grande e atrasada pesa mais que dez pequenas em dia.</summary>
    private static ConsultorFato ColetarInadimplencia(InadimplenciaResultado inad)
    {
        const string ruleId = "fin.inadimplencia";
        const string tela = "entradas-saidas";

        if (inad.ValorTotalEmAbertoCentavos == 0)
        {
            return new ConsultorFato(
                Modulo, ruleId, tela, Score: 0,
                Facts: new Dictionary<string, string> { ["valorEmAberto"] = new Money(0).Formatado() },
                TemplateFallback: "Você não tem contas a receber em aberto no momento.",
                Drill: new DrillTarget(tela));
        }

        var valorTotal = new Money(inad.ValorTotalEmAbertoCentavos).Formatado();
        var provisao = new Money(inad.ProvisaoEsperadaCentavos).Formatado();
        var liquido = new Money(inad.ValorLiquidoEsperadoCentavos).Formatado();
        var score = Clamp0a10000(inad.ProvisaoEsperadaCentavos / 1_000);

        return new ConsultorFato(
            Modulo, ruleId, tela, score,
            Facts: new Dictionary<string, string>
            {
                ["valorEmAberto"] = valorTotal,
                ["provisaoEsperada"] = provisao,
                ["valorLiquidoEsperado"] = liquido,
            },
            TemplateFallback: $"Da sua carteira de {valorTotal} a receber, a expectativa é perder cerca de {provisao} por atraso — valor líquido esperado de {liquido}.",
            Drill: new DrillTarget(tela));
    }

    /// <summary>Catálogo #4 (Radar do Simples Nacional) — "vou pular de faixa? vale vender mais?".
    /// Sem tendência de crescimento clara nos últimos meses fechados, o motor não projeta um mês de
    /// cruzamento (<c>MesesProjetadosAteOProximoDegrau</c> nulo) — mensagem ainda informativa (mostra
    /// a alíquota efetiva de hoje), mas de baixa urgência.
    ///
    /// P0-4 (docs/financeiro/revisao-domain-fit-cnpj.md) — quando o mix do tenant tem MAIS DE UM
    /// anexo (ex.: comércio no Anexo I + serviço no III/V), o fato expõe a quebra e o DAS total
    /// estimado: nunca reduz um CNPJ misto a "uma alíquota só" (a versão antiga, hardcoded no
    /// Anexo I, fazia exatamente isso).</summary>
    private static ConsultorFato ColetarRadarDoSimples(RadarDoSimplesResultado radar)
    {
        const string ruleId = "fin.radar-simples";
        const string tela = "relatorios";

        var aliquota = radar.AliquotaEfetiva.ToString("P1", CulturaPtBr);
        var facts = new Dictionary<string, string>
        {
            ["aliquotaEfetiva"] = aliquota,
            ["faixaAtual"] = radar.FaixaAtual.ToString(CultureInfo.InvariantCulture),
        };

        string? quebraPorAnexo = null;
        if (radar.PorAnexo.Count > 0)
        {
            facts["impostoTotalEstimado"] = new Money(radar.ImpostoTotalEstimadoCentavos).Formatado();
        }
        if (radar.PorAnexo.Count > 1)
        {
            quebraPorAnexo = string.Join("; ", radar.PorAnexo.Select(p =>
                $"Anexo {p.Anexo} {p.AliquotaEfetiva.ToString("P1", CulturaPtBr)} sobre {new Money(p.ReceitaMesCentavos).Formatado()}"));
            facts["quebraPorAnexo"] = quebraPorAnexo;
        }

        string mensagemBase;
        int score;
        if (radar.MesesProjetadosAteOProximoDegrau is { } meses)
        {
            score = Clamp0a10000(10_000 - meses * 800L);
            facts["mesesAteProximaFaixa"] = meses.ToString(CultureInfo.InvariantCulture);
            mensagemBase = $"No ritmo atual de crescimento, sua receita deve cruzar a próxima faixa do Simples Nacional em cerca de {meses} meses — sua alíquota efetiva hoje é {aliquota}.";
        }
        else
        {
            score = 50;
            mensagemBase = $"Sua alíquota efetiva do Simples Nacional hoje é {aliquota} (faixa {radar.FaixaAtual}), sem tendência clara de aproximação da próxima faixa nos últimos meses.";
        }

        var mensagem = quebraPorAnexo is null
            ? mensagemBase
            : $"{mensagemBase} DAS estimado do mês: {new Money(radar.ImpostoTotalEstimadoCentavos).Formatado()} ({quebraPorAnexo}).";

        return new ConsultorFato(Modulo, ruleId, tela, score, Facts: facts, TemplateFallback: mensagem, Drill: new DrillTarget(tela));
    }

    /// <summary>
    /// SINAL ÓBVIO citado na tarefa: "conta grande vence antes de receber X". Compara, dentro dos
    /// próximos <see cref="HorizonteDiasPadrao"/> dias, a MAIOR parcela em aberto de
    /// <see cref="ContaAPagar"/> contra a MAIOR parcela em aberto de <see cref="ContaAReceber"/>.
    /// Só emite fato quando as três condições batem (senão o sinal não é "óbvio", é ruído):
    /// 1) existe uma conta a pagar grande no horizonte;
    /// 2) o saldo em caixa de HOJE não cobre essa conta sozinho;
    /// 3) o maior recebimento esperado (se existir) vence DEPOIS dessa conta.
    /// Retorna <c>null</c> (não crasha, não emite fato de "tudo bem" para isto) quando a condição
    /// não se sustenta — é um sinal opcional, não uma métrica de dashboard sempre presente.
    /// </summary>
    private async Task<ConsultorFato?> ColetarSinalContaGrandeAntesDeReceberAsync(string businessId, CancellationToken ct)
    {
        var agora = relogio.Agora();
        var horizonte = agora.AddDays(HorizonteDiasPadrao);

        var contasPagarAbertas = await contasAPagar.ListarAbertasAteAsync(businessId, horizonte, ct).ConfigureAwait(false);
        var contasReceberAbertas = await contasAReceber.ListarAbertasAteAsync(businessId, horizonte, ct).ConfigureAwait(false);
        var saldoAtual = await movimentos.CalcularSaldoAsync(businessId, null, agora, ct).ConfigureAwait(false);

        var maiorAPagar = MaiorParcelaAbertaNoHorizonte(contasPagarAbertas, agora, horizonte);
        if (maiorAPagar is null || maiorAPagar.ValorRestanteCentavos <= saldoAtual.Centavos)
        {
            return null;
        }

        var maiorAReceber = MaiorParcelaAbertaNoHorizonte(contasReceberAbertas, agora, horizonte);
        if (maiorAReceber is not null && maiorAReceber.Vencimento <= maiorAPagar.Vencimento)
        {
            return null; // o recebimento cobre (ou chega antes/junto), não é um alerta
        }

        const string ruleId = "fin.conta-grande-antes-de-receber";
        const string tela = "bancario";

        var valorAPagar = new Money(maiorAPagar.ValorRestanteCentavos).Formatado();
        var vencAPagar = maiorAPagar.Vencimento.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        var saldo = saldoAtual.Formatado();
        var score = Clamp0a10000(maiorAPagar.ValorRestanteCentavos / 1_000);

        var facts = new Dictionary<string, string>
        {
            ["contaAPagarDescricao"] = maiorAPagar.Descricao,
            ["contaAPagarValor"] = valorAPagar,
            ["contaAPagarVencimento"] = vencAPagar,
            ["saldoAtual"] = saldo,
        };

        string frase;
        if (maiorAReceber is null)
        {
            frase = $"Atenção: a conta \"{maiorAPagar.Descricao}\" de {valorAPagar} vence em {vencAPagar} e seu saldo atual ({saldo}) não cobre esse valor — não há nenhum recebimento grande previsto antes disso.";
        }
        else
        {
            var valorAReceber = new Money(maiorAReceber.ValorRestanteCentavos).Formatado();
            var vencAReceber = maiorAReceber.Vencimento.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
            facts["contaAReceberDescricao"] = maiorAReceber.Descricao;
            facts["contaAReceberValor"] = valorAReceber;
            facts["contaAReceberVencimento"] = vencAReceber;
            frase = $"Atenção: a conta \"{maiorAPagar.Descricao}\" de {valorAPagar} vence em {vencAPagar}, antes do recebimento de \"{maiorAReceber.Descricao}\" ({valorAReceber}) previsto para {vencAReceber} — seu saldo atual ({saldo}) não cobre a conta sozinho.";
        }

        return new ConsultorFato(Modulo, ruleId, tela, score, facts, frase, new DrillTarget(tela));
    }

    private sealed record ParcelaCandidata(string Descricao, DateTimeOffset Vencimento, long ValorRestanteCentavos);

    private static ParcelaCandidata? MaiorParcelaAbertaNoHorizonte(
        IReadOnlyList<ContaFinanceiraBase> contas, DateTimeOffset inicio, DateTimeOffset fim)
        => contas
            .SelectMany(conta => conta.Parcelas
                .Where(p => p.Status is StatusFinanceiro.Aberto or StatusFinanceiro.Parcial or StatusFinanceiro.Atrasado
                            && p.Vencimento >= inicio && p.Vencimento <= fim)
                .Select(p => new ParcelaCandidata(conta.Descricao, p.Vencimento, (p.Valor - p.ValorPago).Centavos)))
            .OrderByDescending(p => p.ValorRestanteCentavos)
            .ThenBy(p => p.Vencimento)
            .FirstOrDefault();

    private static int Clamp0a10000(long valor) => (int)Math.Clamp(valor, 0, 10_000);
}
