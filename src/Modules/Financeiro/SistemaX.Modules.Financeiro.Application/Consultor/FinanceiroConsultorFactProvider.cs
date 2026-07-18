using System.Globalization;
using SistemaX.Modules.Abstractions.Consultor;
using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Application.Projetos;
using SistemaX.Modules.Financeiro.Application.Quant;
using SistemaX.Modules.Financeiro.Application.ReadModels;
using SistemaX.Modules.Financeiro.Application.Tempo;
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
    IConfiguracaoFinanceiraTenantRepository configuracoes,
    IProjetoRepository projetos,
    PainelDoProjetoService painelDoProjeto,
    ResumoDeTempoService resumoDeTempo,
    RoiDoNegocioService roiDoNegocio,
    DreGerencialService dreGerencial,
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
        var breakeven = await pontoDeEquilibrio.CalcularAsync(businessId, ct: ct).ConfigureAwait(false);
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

        // Análise por Projeto (P5, docs/financeiro/design-analise-por-projeto.md §10/§11) — fatos
        // NOVOS fail-quiet: só emite quando o toggle está ligado E há dado real (Lei 2 — "só
        // narra/observa", nunca inventa). Toggle desligado (o caso comum, sem análise por projeto
        // configurada) ⇒ nenhum fato emitido, exatamente como antes desta fatia.
        fatos.AddRange(await ColetarFatosDeProjetoAsync(businessId, ct).ConfigureAwait(false));

        // Imobilizado + Painel de ROI (docs/financeiro/design-imobilizado-roi.md §11 — "Fatos
        // novos, fail-quiet com toggle off"): reusa SÓ RoiDoNegocioService/DreGerencialService, já
        // testados na F1; opt-in ESTRITO — sem ImobilizadoRoiAtivo, RoiDoNegocioService devolve
        // Falha (FinanceiroOptInGuard) e nenhum fato nasce, mesmo padrão fail-quiet do resto do
        // provider (Lei 2: só narra o que já existe, nunca invade sem opt-in explícito).
        fatos.AddRange(await ColetarFatosDeRoiAsync(businessId, ct).ConfigureAwait(false));

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
    /// SINAL ÓBVIO citado na tarefa: "conta grande vence antes de receber X". P2-3
    /// (docs/financeiro/revisao-domain-fit-cnpj.md) — a matemática mora inteira em
    /// <see cref="SinalContaGrandeAntesDoRecebimento.Detectar"/> (projeção ACUMULADA de caixa até o
    /// vencimento da maior conta a pagar, não só a comparação contra a maior parcela a receber que
    /// a versão inline antiga fazia); este método só ADAPTA os ports do Financeiro (parcelas em
    /// aberto + saldo) para o formato de insumo do Quant e traduz o <c>Resultado</c> em
    /// <see cref="ConsultorFato"/>. Retorna <c>null</c> (não crasha, não emite fato de "tudo bem"
    /// para isto) quando a condição não se sustenta — é um sinal opcional, não uma métrica de
    /// dashboard sempre presente.
    /// </summary>
    private async Task<ConsultorFato?> ColetarSinalContaGrandeAntesDeReceberAsync(string businessId, CancellationToken ct)
    {
        var agora = relogio.Agora();
        var horizonte = agora.AddDays(HorizonteDiasPadrao);

        var contasPagarAbertas = await contasAPagar.ListarAbertasAteAsync(businessId, horizonte, ct).ConfigureAwait(false);
        var contasReceberAbertas = await contasAReceber.ListarAbertasAteAsync(businessId, horizonte, ct).ConfigureAwait(false);
        var saldoAtual = await movimentos.CalcularSaldoAsync(businessId, null, agora, ct).ConfigureAwait(false);

        var descricaoPorParcelaId = new Dictionary<string, string>();
        var parcelasAPagar = ParaParcelasAbertas(contasPagarAbertas, agora, descricaoPorParcelaId);
        var parcelasAReceber = ParaParcelasAbertas(contasReceberAbertas, agora, descricaoPorParcelaId);

        var sinal = SinalContaGrandeAntesDoRecebimento.Detectar(saldoAtual.Centavos, parcelasAPagar, parcelasAReceber, HorizonteDiasPadrao);
        if (sinal is null) return null;

        const string ruleId = "fin.conta-grande-antes-de-receber";
        const string tela = "bancario";

        var descricaoConta = descricaoPorParcelaId.GetValueOrDefault(sinal.ParcelaId, "Conta a pagar");
        var vencimento = agora.AddDays(sinal.DiasParaVencer).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        var valorConta = new Money(sinal.ValorDaContaCentavos).Formatado();
        var caixaProjetado = new Money(sinal.CaixaProjetadoAntesCentavos).Formatado();
        var falta = new Money(sinal.FaltaCentavos).Formatado();
        var score = Clamp0a10000(sinal.ValorDaContaCentavos / 1_000);

        var facts = new Dictionary<string, string>
        {
            ["contaAPagarDescricao"] = descricaoConta,
            ["contaAPagarValor"] = valorConta,
            ["contaAPagarVencimento"] = vencimento,
            ["saldoAtual"] = saldoAtual.Formatado(),
            ["caixaProjetadoAteLa"] = caixaProjetado,
            ["falta"] = falta,
        };

        var frase = $"Atenção: a conta \"{descricaoConta}\" de {valorConta} vence em {vencimento} — seu caixa projetado até lá ({caixaProjetado}, considerando entradas e saídas já esperadas) não cobre esse valor. Faltariam {falta}.";

        return new ConsultorFato(Modulo, ruleId, tela, score, facts, frase, new DrillTarget(tela));
    }

    /// <summary>Achata <see cref="ContaFinanceiraBase.Parcelas"/> em aberto (Aberto/Parcial/Atrasado)
    /// dentro do horizonte pedido para o formato de insumo de
    /// <see cref="SinalContaGrandeAntesDoRecebimento.ParcelaAberta"/> — dias já vencidos (negativos)
    /// são CLAMPADOS a 0 (urgência máxima), exatamente como o contrato do Quant documenta. Acumula
    /// a descrição da conta de origem em <paramref name="descricaoPorParcelaId"/> só para a
    /// mensagem — o Quant em si não conhece descrição, só números.</summary>
    private static IReadOnlyList<SinalContaGrandeAntesDoRecebimento.ParcelaAberta> ParaParcelasAbertas(
        IReadOnlyList<ContaFinanceiraBase> contas, DateTimeOffset agora, IDictionary<string, string> descricaoPorParcelaId)
    {
        var resultado = new List<SinalContaGrandeAntesDoRecebimento.ParcelaAberta>();
        foreach (var conta in contas)
        {
            foreach (var parcela in conta.Parcelas)
            {
                if (parcela.Status is not (StatusFinanceiro.Aberto or StatusFinanceiro.Parcial or StatusFinanceiro.Atrasado)) continue;

                var dias = Math.Max(0, (int)Math.Ceiling((parcela.Vencimento - agora).TotalDays));
                var restante = (parcela.Valor - parcela.ValorPago).Centavos;
                descricaoPorParcelaId[parcela.Id] = conta.Descricao;
                resultado.Add(new SinalContaGrandeAntesDoRecebimento.ParcelaAberta(parcela.Id, restante, dias));
            }
        }
        return resultado;
    }

    /// <summary>
    /// Análise por Projeto (P5) — três fatos novos, um por PROJETO ativo com dado suficiente:
    /// payback projetado, custo de ociosidade das licenças e (uma vez, cross-projeto) o cliente
    /// com maior consumo de tempo. FAIL-QUIET em cada camada: toggle desligado → lista vazia sem
    /// tocar em nenhum repositório de projeto; projeto sem payback/ociosidade/tempo mensurável →
    /// simplesmente não gera aquele fato (nunca um <c>null</c> formatado ou um crash).
    /// </summary>
    private async Task<IReadOnlyList<ConsultorFato>> ColetarFatosDeProjetoAsync(string businessId, CancellationToken ct)
    {
        var configuracao = await configuracoes.ObterAsync(businessId, ct).ConfigureAwait(false);
        if (configuracao is not { AnalisePorProjetoAtiva: true }) return [];

        var listaDeProjetos = await projetos.ListarAsync(businessId, incluirArquivados: false, ct).ConfigureAwait(false);
        if (listaDeProjetos.Count == 0) return [];

        var fatos = new List<ConsultorFato>();
        foreach (var projeto in listaDeProjetos)
        {
            var painel = await painelDoProjeto.CalcularAsync(businessId, projeto.Id, ct).ConfigureAwait(false);
            if (painel.Falha) continue; // fail-quiet — não deveria acontecer (acabamos de listar o projeto), mas nunca crasha o Consultor

            var valor = painel.Valor;

            if (valor.Payback.PaybackProjetadoMeses is { } meses)
            {
                fatos.Add(ColetarPaybackProjetado(projeto.Nome, meses, valor.Payback.InvestimentoTotalCentavos));
            }

            if (valor.Capacidade.UnidadesTotais > 0 && valor.Capacidade.CustoOciosidadeMesCentavos > 0)
            {
                fatos.Add(ColetarCustoDeOciosidade(projeto.Nome, valor.Capacidade));
            }
        }

        var gargalo = await ColetarGargaloDeTempoAsync(businessId, ct).ConfigureAwait(false);
        if (gargalo is not null) fatos.Add(gargalo);

        return fatos;
    }

    private ConsultorFato ColetarPaybackProjetado(string nomeProjeto, int meses, long investimentoTotalCentavos)
    {
        const string ruleId = "fin.projeto.payback";
        const string tela = "projetos";

        var score = Clamp0a10000(10_000 - meses * 60L);
        var investimento = new Money(investimentoTotalCentavos).Formatado();

        return new ConsultorFato(
            Modulo, ruleId, tela, score,
            Facts: new Dictionary<string, string>
            {
                ["projeto"] = nomeProjeto,
                ["paybackProjetadoMeses"] = meses.ToString(CultureInfo.InvariantCulture),
                ["investimentoTotal"] = investimento,
            },
            TemplateFallback: $"No ritmo atual, o projeto \"{nomeProjeto}\" recupera o investimento de {investimento} em cerca de {meses} meses (projeção determinística).",
            Drill: new DrillTarget(tela));
    }

    private static ConsultorFato ColetarCustoDeOciosidade(string nomeProjeto, PainelCapacidadeProjeto capacidade)
    {
        const string ruleId = "fin.projeto.ociosidade";
        const string tela = "projetos";

        var custo = new Money(capacidade.CustoOciosidadeMesCentavos).Formatado();
        var score = Clamp0a10000(capacidade.CustoOciosidadeMesCentavos / 1_000);

        return new ConsultorFato(
            Modulo, ruleId, tela, score,
            Facts: new Dictionary<string, string>
            {
                ["projeto"] = nomeProjeto,
                ["utilizacaoPercent"] = capacidade.UtilizacaoPercent.ToString("0.0", CultureInfo.InvariantCulture),
                ["custoOciosidadeMes"] = custo,
            },
            TemplateFallback: $"O projeto \"{nomeProjeto}\" está usando {capacidade.UtilizacaoPercent:0.0}% da capacidade contratada — a ociosidade custa {custo}/mês (a amortização corre sobre o total, independente do uso).",
            Drill: new DrillTarget(tela));
    }

    /// <summary>Índice de gargalo cross-projeto (design §9.7) — SIMPLIFICADO nesta fatia: sem
    /// custo/hora resolvido (P4, decisão do dono), o "índice" é a própria ordenação por minutos
    /// desc (o cliente que mais consome tempo de atendimento). Fail-quiet: sem apontamentos no
    /// período, nenhum fato.</summary>
    private async Task<ConsultorFato?> ColetarGargaloDeTempoAsync(string businessId, CancellationToken ct)
    {
        var agora = relogio.Agora();
        var inicioMes = new DateTimeOffset(agora.Year, agora.Month, 1, 0, 0, 0, agora.Offset);
        var resumo = await resumoDeTempo.CalcularAsync(businessId, inicioMes, agora, ct).ConfigureAwait(false);

        var maiorConsumo = resumo.PorCliente.FirstOrDefault();
        if (maiorConsumo is null || maiorConsumo.Minutos <= 0) return null;

        const string ruleId = "fin.tempo.gargalo-cliente";
        const string tela = "projetos";

        var horas = Math.Round(maiorConsumo.Minutos / 60m, 1);
        var nomeCliente = maiorConsumo.ClienteNome ?? maiorConsumo.ClienteId;
        var score = Clamp0a10000(maiorConsumo.Minutos * 5L);

        return new ConsultorFato(
            Modulo, ruleId, tela, score,
            Facts: new Dictionary<string, string>
            {
                ["cliente"] = nomeCliente,
                ["minutosNoMes"] = maiorConsumo.Minutos.ToString(CultureInfo.InvariantCulture),
                ["horasNoMes"] = horas.ToString(CultureInfo.InvariantCulture),
            },
            TemplateFallback: $"O cliente \"{nomeCliente}\" consumiu {horas:0.0}h de atendimento este mês — o maior volume de tempo entre seus clientes tageados.",
            Drill: new DrillTarget(tela));
    }

    /// <summary>Orquestra os 3 fatos do painel de ROI (design §11) — SEMPRE consulta
    /// <see cref="RoiDoNegocioService"/> primeiro: toggle desligado (ou sem marco m0 resolvível)
    /// devolve <c>Falha</c> e a lista fica vazia sem tocar em mais nada, o mesmo gate que o
    /// endpoint <c>GET /financeiro/roi-negocio</c> já usa.</summary>
    private async Task<IReadOnlyList<ConsultorFato>> ColetarFatosDeRoiAsync(string businessId, CancellationToken ct)
    {
        var roi = await roiDoNegocio.CalcularAsync(businessId, ct).ConfigureAwait(false);
        if (roi.Falha) return [];

        var fatos = new List<ConsultorFato> { ColetarFaltamProRoiCompleto(roi.Valor) };

        var tir = ColetarTirDoNegocio(roi.Valor.Tir);
        if (tir is not null) fatos.Add(tir);

        var depreciacao = await ColetarDepreciacaoMensalAsync(businessId, ct).ConfigureAwait(false);
        if (depreciacao is not null) fatos.Add(depreciacao);

        return fatos;
    }

    /// <summary>"Faltam R$X (~N meses) pro ROI completo" (design §11) — reusa
    /// <see cref="RoiRecuperacao"/>/<see cref="RoiPayback.ProjetadoMeses"/> de
    /// <see cref="RoiDoNegocioService"/>, zero cálculo novo. Recuperação já completa
    /// (<c>FaltamCentavos == 0</c>) vira boa notícia, não "faltam R$0" sem sentido.</summary>
    private ConsultorFato ColetarFaltamProRoiCompleto(RoiDoNegocioResultado roi)
    {
        const string ruleId = "fin.roi.faltam-para-completo";
        const string tela = "imobilizado-roi";
        var percentRecuperado = roi.Recuperacao.PercentRecuperado.ToString("0.0", CulturaPtBr);

        if (roi.Recuperacao.FaltamCentavos <= 0)
        {
            return new ConsultorFato(
                Modulo, ruleId, tela, Score: 100,
                Facts: new Dictionary<string, string> { ["percentRecuperado"] = percentRecuperado },
                TemplateFallback: "Seu ROI do negócio já está completo — o investimento total já voltou em caixa/lucro operacional.",
                Drill: new DrillTarget(tela));
        }

        var faltam = new Money(roi.Recuperacao.FaltamCentavos).Formatado();
        var meses = roi.Payback.ProjetadoMeses;
        var score = meses is { } m ? Clamp0a10000(10_000 - m * 60L) : 4_000;

        var frase = meses is { } mesesProjetados
            ? $"Faltam {faltam} (cerca de {mesesProjetados} meses no ritmo atual) para o ROI completo do seu negócio."
            : $"Faltam {faltam} para o ROI completo do seu negócio — sem tendência clara pra projetar quando, no ritmo atual.";

        return new ConsultorFato(
            Modulo, ruleId, tela, score,
            Facts: new Dictionary<string, string>
            {
                ["faltamParaRoiCompleto"] = faltam,
                ["mesesProjetados"] = meses?.ToString(CultureInfo.InvariantCulture) ?? "sem projeção",
                ["percentRecuperado"] = percentRecuperado,
            },
            TemplateFallback: frase,
            Drill: new DrillTarget(tela));
    }

    /// <summary>"TIR atual Y% a.a." (design §11) — reusa <see cref="TaxaInternaDeRetorno"/> via
    /// <see cref="RoiDoNegocioService"/>. TIR indefinida (sem troca de sinal/sem raiz no intervalo)
    /// não emite fato nenhum — Lei 2, nunca formata um número que não existe.</summary>
    private ConsultorFato? ColetarTirDoNegocio(RoiTir tir)
    {
        if (tir.AnualizadaPercent is not { } anualizada) return null;

        const string ruleId = "fin.roi.tir";
        const string tela = "imobilizado-roi";

        var tirTexto = anualizada.ToString("0.0", CulturaPtBr);
        var score = Clamp0a10000((long)Math.Round(anualizada * 100));

        return new ConsultorFato(
            Modulo, ruleId, tela, score,
            Facts: new Dictionary<string, string> { ["tirAnualizada"] = $"{tirTexto}% a.a." },
            TemplateFallback: $"A taxa interna de retorno (TIR) do seu negócio hoje é de {tirTexto}% ao ano.",
            Drill: new DrillTarget(tela));
    }

    /// <summary>"Depreciação mensal da estrutura R$Z" (design §11) — reusa a linha
    /// <see cref="DreResultado.DepreciacaoEAmortizacao"/> que o próprio DRE já expõe (mesmo lar
    /// único de <see cref="CronogramaLinear"/>, nunca um segundo cálculo de amortização aqui). Mês
    /// sem nenhum bem amortizável rodando (linha zerada) não emite fato — silêncio é a resposta
    /// certa quando não há o que narrar.</summary>
    private async Task<ConsultorFato?> ColetarDepreciacaoMensalAsync(string businessId, CancellationToken ct)
    {
        var agora = relogio.Agora();
        var inicioDoMes = new DateTimeOffset(agora.Year, agora.Month, 1, 0, 0, 0, agora.Offset);
        var dre = await dreGerencial.CalcularAsync(businessId, inicioDoMes, agora, ct).ConfigureAwait(false);

        if (dre.DepreciacaoEAmortizacao.EhZero) return null;

        const string ruleId = "fin.roi.depreciacao-mensal";
        const string tela = "imobilizado-roi";

        var valor = dre.DepreciacaoEAmortizacao.Formatado();
        var score = Clamp0a10000(dre.DepreciacaoEAmortizacao.Centavos / 1_000);

        return new ConsultorFato(
            Modulo, ruleId, tela, score,
            Facts: new Dictionary<string, string> { ["depreciacaoMensal"] = valor },
            TemplateFallback: $"A depreciação/amortização da sua estrutura (equipamentos, licenças, obras) é de {valor} neste mês.",
            Drill: new DrillTarget(tela));
    }

    private static int Clamp0a10000(long valor) => (int)Math.Clamp(valor, 0, 10_000);
}
