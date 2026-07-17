using SistemaX.Modules.Financeiro.Domain.Comum;

namespace SistemaX.Modules.Financeiro.Application.Quant;

/// <summary>
/// Anexo do Simples Nacional — cópia PRÓPRIA do Financeiro (este módulo não referencia
/// <c>Fiscal.Domain</c>: isolamento de módulo, mesmo racional documentado para
/// <c>NaturezaOperacao</c> em <c>Modules.Abstractions/IntegrationEvents.cs</c> — "cada módulo
/// mantém sua PRÓPRIA cópia" de um fato estável do domínio compartilhado).
/// </summary>
public enum AnexoSimplesNacional { I, II, III, IV, V }

/// <summary>
/// Radar do Simples Nacional (catálogo #4 do plano de inteligência do Financeiro —
/// docs/financeiro/inteligencia-arquitetura.md §4/ADR-0005) — "vou pular de faixa? vale vender
/// mais?". Alerta de sublimite ANTES de estourar, a partir do RBT12 (receita bruta acumulada dos
/// últimos 12 meses) e da tabela de faixas do Anexo.
///
/// P0-4 (docs/financeiro/revisao-domain-fit-cnpj.md) — MULTI-ANEXO: além do Anexo I (comércio),
/// agora também os Anexos III e V (serviço, com Fator R decidindo entre os dois —
/// <see cref="FatorR"/>) e a repartição do mix por corrente de receita (<see cref="CalcularMix"/>),
/// necessários para o CNPJ-alvo (assistência técnica: peças no Anexo I, mão de obra no III/V).
/// </summary>
public static class RadarDoSimplesNacional
{
    public sealed record Faixa(int Numero, long TetoRbt12Centavos, double AliquotaNominal, long ParcelaADeduzirCentavos);

    /// <summary>
    /// Tabela OFICIAL do Anexo I (Comércio) do Simples Nacional — LC 123/2006 art. 18, redação da
    /// LC 155/2016 (em vigor desde jan/2018, valores estáveis desde então). Fato legal fechado (só
    /// muda por lei nova, nunca por configuração de tenant).
    /// </summary>
    public static readonly IReadOnlyList<Faixa> AnexoI =
    [
        new(1, 18_000_000, 0.0400, 0),
        new(2, 36_000_000, 0.0730, 594_000),
        new(3, 72_000_000, 0.0950, 1_386_000),
        new(4, 180_000_000, 0.1070, 2_250_000),
        new(5, 360_000_000, 0.1430, 8_730_000),
        new(6, 480_000_000, 0.1900, 37_800_000),
    ];

    /// <summary>
    /// Tabela OFICIAL do Anexo III (Serviços — mão de obra, Fator R ≥ 28%) — LC 123/2006 art. 18,
    /// redação da LC 155/2016. Os 6 TETOS de RBT12 são IDÊNTICOS aos do Anexo I (e de qualquer
    /// outro anexo do Simples — é a MESMA régua de enquadramento em faixa para toda a empresa,
    /// LC 123/2006 art. 18 §1º); só a alíquota nominal e a parcela a deduzir mudam por tabela
    /// (P0-4, docs/financeiro/revisao-domain-fit-cnpj.md).
    /// </summary>
    public static readonly IReadOnlyList<Faixa> AnexoIII =
    [
        new(1, 18_000_000, 0.0600, 0),
        new(2, 36_000_000, 0.1120, 936_000),
        new(3, 72_000_000, 0.1350, 1_764_000),
        new(4, 180_000_000, 0.1600, 3_564_000),
        new(5, 360_000_000, 0.2100, 12_564_000),
        new(6, 480_000_000, 0.3300, 64_800_000),
    ];

    /// <summary>
    /// Tabela OFICIAL do Anexo V (Serviços — mão de obra, Fator R &lt; 28%) — LC 123/2006 art. 18,
    /// redação da LC 155/2016. Mesmos 6 tetos de RBT12 do Anexo I/III — ver comentário de
    /// <see cref="AnexoIII"/>.
    /// </summary>
    public static readonly IReadOnlyList<Faixa> AnexoV =
    [
        new(1, 18_000_000, 0.1550, 0),
        new(2, 36_000_000, 0.1800, 450_000),
        new(3, 72_000_000, 0.1950, 990_000),
        new(4, 180_000_000, 0.2050, 1_710_000),
        new(5, 360_000_000, 0.2300, 6_210_000),
        new(6, 480_000_000, 0.3050, 54_000_000),
    ];

    /// <summary>Tabela de faixas do anexo pedido — só I/III/V têm tabela nesta fase (II e IV ainda
    /// não têm tenant que precise — mesmo padrão de "preparado, não operante" do enum
    /// <see cref="AnexoSimplesNacional"/>, que já tem os 5 valores).</summary>
    public static IReadOnlyList<Faixa> ObterTabela(AnexoSimplesNacional anexo) => anexo switch
    {
        AnexoSimplesNacional.I => AnexoI,
        AnexoSimplesNacional.III => AnexoIII,
        AnexoSimplesNacional.V => AnexoV,
        _ => throw new NotSupportedException($"Anexo {anexo} sem tabela de faixas implementada nesta fase (só I, III e V)."),
    };

    public sealed record Resultado(
        long Rbt12Centavos,
        int FaixaAtual,
        double AliquotaEfetiva,
        double AliquotaNominalFaixaAtual,
        long DistanciaAoProximoDegrauCentavos,
        int? MesesProjetadosAteOProximoDegrau);

    /// <summary>Primeira faixa cujo teto acomoda o RBT12; se o RBT12 já superou o teto da última
    /// faixa (sublimite estourado — regime vira <c>SimplesNacionalSublimite</c> no Fiscal), retorna
    /// a última faixa mesmo assim (distância ao próximo degrau fica 0 — não há "próximo degrau"
    /// dentro do Simples).</summary>
    public static Faixa EncontrarFaixa(IReadOnlyList<Faixa> tabela, long rbt12Centavos)
        => tabela.FirstOrDefault(f => rbt12Centavos <= f.TetoRbt12Centavos) ?? tabela[^1];

    /// <summary>Alíquota efetiva — fórmula OFICIAL da LC 123/2006 art. 18 §1º-A:
    /// <c>((RBT12 × Alíquota nominal) − Parcela a deduzir) / RBT12</c>. Clampada em 0 (nunca
    /// negativa) e 0 para RBT12 ≤ 0 (empresa sem receita acumulada não tem alíquota efetiva a
    /// calcular).</summary>
    public static double CalcularAliquotaEfetiva(long rbt12Centavos, Faixa faixa)
    {
        if (rbt12Centavos <= 0) return 0;
        var efetiva = (rbt12Centavos * faixa.AliquotaNominal - faixa.ParcelaADeduzirCentavos) / rbt12Centavos;
        return Math.Max(0, efetiva);
    }

    /// <summary>
    /// Radar completo (UM anexo/UMA corrente) — faixa atual, alíquota efetiva, distância ao
    /// próximo degrau em R$ e, se a receita mensal recente vem crescendo, em quantos MESES o RBT12
    /// cruza o próximo teto (<see cref="ProjetarMesesAteODegrau"/> — P1-1, matemática de janela
    /// móvel corrigida). Sem crescimento médio POSITIVO (receita estável ou caindo) não há
    /// projeção — <c>null</c> significa "no ritmo atual, não cruza".
    /// </summary>
    public static Resultado Calcular(long rbt12Centavos, IReadOnlyList<Faixa> tabela, IReadOnlyList<long> receitaMensalRecenteCentavos)
    {
        var faixaAtual = EncontrarFaixa(tabela, rbt12Centavos);
        var aliquotaEfetiva = CalcularAliquotaEfetiva(rbt12Centavos, faixaAtual);
        var distancia = Math.Max(0, faixaAtual.TetoRbt12Centavos - rbt12Centavos);

        var mesesProjetados = ProjetarMesesAteODegrau(rbt12Centavos, faixaAtual.TetoRbt12Centavos, distancia, receitaMensalRecenteCentavos);

        return new Resultado(rbt12Centavos, faixaAtual.Numero, aliquotaEfetiva, faixaAtual.AliquotaNominal, distancia, mesesProjetados);
    }

    /// <summary>
    /// P1-1 (docs/financeiro/revisao-domain-fit-cnpj.md) — quantos MESES até o RBT12 cruzar
    /// <paramref name="tetoCentavos"/>, rolando EXPLICITAMENTE a janela móvel de N meses (N =
    /// <paramref name="ultimosMesesFechadosCentavos"/>.Count — em produção sempre 12, o tamanho
    /// real do RBT12; testes podem usar N menor, a matemática não muda).
    ///
    /// BUG ANTIGO: dividia a distância pelo crescimento médio mensal simples (`g` = média dos
    /// deltas mês a mês) como se o incremento do RBT12 fosse `g`. Mas o RBT12 é uma SOMA MÓVEL de
    /// N meses: seu incremento real de um mês para o outro é `m_{t+1} − m_{t−(N−1)}`, não `g`. Sob
    /// crescimento linear sustentado `m_t = a + g·t`, esse incremento vale `N·g` — N VEZES maior
    /// que `g` — então a fórmula antiga superestimava o prazo em ~N× (para N=12, ~12× otimista:
    /// o alerta de sublimite chegava tarde demais, exatamente o que o radar existe para evitar).
    ///
    /// FÓRMULA CORRIGIDA: projeta `m̂_{t+j} = m_t + g·j` (extrapolação linear a partir do último mês
    /// fechado) e acumula `RBT12_{t+k} = RBT12_t + Σ_{j=1..k} (m̂_{t+j} − m_{t+j−N})`, onde
    /// `m_{t+j−N}` é um mês HISTÓRICO real enquanto `j ≤ N` (vem de
    /// <paramref name="ultimosMesesFechadosCentavos"/>) e um mês PROJETADO (mesma extrapolação)
    /// quando `j &gt; N` — o menor `k` cujo RBT12 acumulado cruza o teto é a resposta.
    /// Determinístico, sem dependência nova, e degrada corretamente para "não cruza" quando `g ≤ 0`.
    /// </summary>
    private static int? ProjetarMesesAteODegrau(
        long rbt12InicialCentavos, long tetoCentavos, long distanciaInicialCentavos, IReadOnlyList<long> ultimosMesesFechadosCentavos)
    {
        if (ultimosMesesFechadosCentavos.Count < 2 || distanciaInicialCentavos <= 0) return null;

        var n = ultimosMesesFechadosCentavos.Count;
        var ultimoMesFechado = ultimosMesesFechadosCentavos[^1];

        var deltas = new List<long>(n - 1);
        for (var i = 1; i < n; i++)
            deltas.Add(ultimosMesesFechadosCentavos[i] - ultimosMesesFechadosCentavos[i - 1]);

        var crescimentoMedioMensal = deltas.Average();
        if (crescimentoMedioMensal <= 0) return null;

        const int horizonteMaximoMeses = 1_200; // 100 anos — guarda contra loop com crescimento residual
        var rbt12Projetado = rbt12InicialCentavos;

        for (var k = 1; k <= horizonteMaximoMeses; k++)
        {
            var mesFuturo = ultimoMesFechado + crescimentoMedioMensal * k;
            var mesQueSai = k <= n
                ? ultimosMesesFechadosCentavos[k - 1] // mês histórico real (m_{t+k−N}, k ≤ N)
                : ultimoMesFechado + crescimentoMedioMensal * (k - n); // mês projetado (k > N)

            rbt12Projetado += (long)Math.Round(mesFuturo - mesQueSai, MidpointRounding.AwayFromZero);
            if (rbt12Projetado >= tetoCentavos) return k;
        }

        return null; // crescimento residual demais para cruzar num horizonte relevante
    }

    /// <summary>Resultado do Radar para UM anexo dentro do mix (P0-4) — a fração da receita do mês
    /// tributada nesse anexo, sua faixa/alíquota efetiva (usando o RBT12 TOTAL da empresa, LC
    /// 123/2006 art. 18 §§1º-A/3º) e o imposto estimado dessa fatia.</summary>
    public sealed record ResultadoPorAnexo(
        AnexoSimplesNacional Anexo,
        long ReceitaMesCentavos,
        int FaixaNumero,
        double AliquotaEfetiva,
        long ImpostoEstimadoCentavos);

    /// <summary>Resultado do Radar para o MIX inteiro (P0-4) — faixa/distância/projeção são
    /// company-wide (os 6 tetos de RBT12 são os mesmos em qualquer anexo — ver <see cref="AnexoIII"/>);
    /// <see cref="PorAnexo"/> é a repartição por anexo e <see cref="ImpostoTotalEstimadoCentavos"/>
    /// é a soma — o DAS estimado do mês.</summary>
    public sealed record ResultadoMix(
        long Rbt12Centavos,
        double FatorR,
        int FaixaAtual,
        long DistanciaAoProximoDegrauCentavos,
        int? MesesProjetadosAteOProximoDegrau,
        IReadOnlyList<ResultadoPorAnexo> PorAnexo,
        long ImpostoTotalEstimadoCentavos);

    /// <summary>
    /// Radar MULTI-ANEXO (P0-4, docs/financeiro/revisao-domain-fit-cnpj.md) — reparte
    /// <paramref name="receitaMesPorCorrenteCentavos"/> pelo anexo de cada corrente (via
    /// <paramref name="mapeamento"/>, resolvido com <paramref name="fatorR"/> quando a regra da
    /// corrente for <see cref="RegraDeEnquadramento.PorFatorR"/>), calcula a alíquota efetiva de
    /// CADA anexo presente usando o MESMO <paramref name="rbt12Centavos"/> TOTAL da empresa (LC
    /// 123/2006 art. 18 §§1º-A/3º — nunca um RBT12 "só daquela corrente") e soma o imposto
    /// estimado de cada fatia: <c>DAS_mês = Σ_anexo (receita_mês_anexo × alíquota_efetiva_do_anexo)</c>.
    /// </summary>
    public static ResultadoMix CalcularMix(
        long rbt12Centavos,
        IReadOnlyList<long> ultimosMesesFechadosCentavos,
        double fatorR,
        IReadOnlyDictionary<CorrenteDeReceita, long> receitaMesPorCorrenteCentavos,
        IReadOnlyList<MapeamentoCorrenteAnexo> mapeamento)
    {
        // Os 6 tetos de RBT12 são idênticos em todos os anexos do Simples Nacional — o AnexoI serve
        // de "régua" de faixa/distância/projeção para o mix inteiro (ver comentário de AnexoIII).
        var faixaReferencia = EncontrarFaixa(AnexoI, rbt12Centavos);
        var distancia = Math.Max(0, faixaReferencia.TetoRbt12Centavos - rbt12Centavos);
        var mesesProjetados = ProjetarMesesAteODegrau(rbt12Centavos, faixaReferencia.TetoRbt12Centavos, distancia, ultimosMesesFechadosCentavos);

        var receitaPorAnexo = new Dictionary<AnexoSimplesNacional, long>();
        foreach (var m in mapeamento)
        {
            if (!receitaMesPorCorrenteCentavos.TryGetValue(m.Corrente, out var receitaDaCorrente) || receitaDaCorrente == 0) continue;

            var anexo = m.Resolver(fatorR);
            receitaPorAnexo[anexo] = receitaPorAnexo.GetValueOrDefault(anexo) + receitaDaCorrente;
        }

        var porAnexo = new List<ResultadoPorAnexo>();
        var impostoTotal = 0L;
        foreach (var (anexo, receitaDoAnexo) in receitaPorAnexo.OrderBy(par => par.Key))
        {
            var tabela = ObterTabela(anexo);
            var faixaDoAnexo = tabela[faixaReferencia.Numero - 1]; // mesmo índice de faixa — só nominal/PD mudam
            var aliquotaEfetiva = CalcularAliquotaEfetiva(rbt12Centavos, faixaDoAnexo);
            var imposto = (long)Math.Round(receitaDoAnexo * aliquotaEfetiva, MidpointRounding.AwayFromZero);

            impostoTotal += imposto;
            porAnexo.Add(new ResultadoPorAnexo(anexo, receitaDoAnexo, faixaDoAnexo.Numero, aliquotaEfetiva, imposto));
        }

        return new ResultadoMix(rbt12Centavos, fatorR, faixaReferencia.Numero, distancia, mesesProjetados, porAnexo, impostoTotal);
    }
}

/// <summary>
/// Fator R (P0-4, docs/financeiro/revisao-domain-fit-cnpj.md) — LC 123/2006 art. 18 §5º-J/§5º-M:
/// decide se a corrente de SERVIÇO cai no Anexo III (mão de obra "pesada" em folha) ou no Anexo V
/// (mão de obra "leve"). <c>FatorR = folha de salários dos últimos 12 meses (incl. encargos e
/// pró-labore) ÷ RBT12</c>; <c>≥ 28% → Anexo III</c> (alíquotas iniciais menores), <c>&lt; 28% →
/// Anexo V</c>. A folha de 12 meses nasce de <c>FolhaLancada</c> (categoria
/// <c>despesa-com-pessoal</c> em <c>ContaAPagar</c> — já no ledger, ver
/// <c>RadarDoSimplesService.CarregarFolhaDozeMesesAsync</c>).
/// </summary>
public static class FatorR
{
    /// <summary>Limiar legal (LC 123/2006 art. 18 §5º-J) — 28% de folha/RBT12. Fato legal fechado,
    /// nunca configurável por tenant (diferente do mapeamento corrente→anexo).</summary>
    public const double LimiarAnexoIii = 0.28;

    /// <summary>Razão folha 12m ÷ RBT12. RBT12 ≤ 0 (empresa sem receita acumulada) não tem Fator R
    /// que faça sentido — retorna 0 (mesmo clamp de <see cref="RadarDoSimplesNacional.CalcularAliquotaEfetiva"/>).</summary>
    public static double Calcular(long folhaDozeMesesCentavos, long rbt12Centavos)
        => rbt12Centavos <= 0 ? 0 : (double)folhaDozeMesesCentavos / rbt12Centavos;

    /// <summary>Anexo da corrente de SERVIÇO/RECORRENTE dado o Fator R já calculado.</summary>
    public static AnexoSimplesNacional AnexoDeServicoPorFatorR(double fatorR)
        => fatorR >= LimiarAnexoIii ? AnexoSimplesNacional.III : AnexoSimplesNacional.V;
}

/// <summary>Como uma <see cref="CorrenteDeReceita"/> é enquadrada em anexo — <see cref="AnexoFixo"/>
/// (ex.: Comércio sempre Anexo I) ou <see cref="PorFatorR"/> (ex.: Serviço decide III/V pelo Fator
/// R da empresa, sempre recalculado por apuração).</summary>
public enum RegraDeEnquadramento
{
    PorFatorR = 0,
    AnexoFixo = 1,
}

/// <summary>
/// Mapeamento CONFIGURÁVEL POR TENANT (P0-4, docs/financeiro/revisao-domain-fit-cnpj.md) de
/// corrente de receita → anexo do Simples Nacional. Configurável porque o enquadramento real varia
/// por CNPJ (uma assinatura de software pode, a depender do contrato, ser enquadrada diferente de
/// uma OS de mão de obra) — ver <see cref="MapeamentoCorrenteAnexoPadrao"/> para o padrão
/// (assistência técnica) usado quando o tenant não personalizou nada.
/// </summary>
public sealed record MapeamentoCorrenteAnexo(CorrenteDeReceita Corrente, RegraDeEnquadramento Regra, AnexoSimplesNacional? AnexoFixo = null)
{
    /// <summary>Resolve o anexo desta corrente para a apuração atual — <paramref name="fatorR"/>
    /// só é consultado quando <see cref="Regra"/> for <see cref="RegraDeEnquadramento.PorFatorR"/>.</summary>
    public AnexoSimplesNacional Resolver(double fatorR) => Regra switch
    {
        RegraDeEnquadramento.AnexoFixo => AnexoFixo ?? throw new InvalidOperationException(
            $"Mapeamento da corrente {Corrente} está marcado como {nameof(RegraDeEnquadramento.AnexoFixo)} mas não define {nameof(AnexoFixo)}."),
        RegraDeEnquadramento.PorFatorR => FatorR.AnexoDeServicoPorFatorR(fatorR),
        _ => throw new ArgumentOutOfRangeException(nameof(Regra), Regra, "Regra de enquadramento desconhecida."),
    };
}

/// <summary>Mapeamento PADRÃO (P0-4) usado quando o tenant não configurou nada — o enquadramento
/// típico de uma assistência técnica: peças/comércio sempre Anexo I; mão de obra de OS e receita
/// recorrente (assinatura) decididas pelo Fator R da empresa (III se ≥ 28%, V se &lt; 28%).</summary>
public static class MapeamentoCorrenteAnexoPadrao
{
    public static IReadOnlyList<MapeamentoCorrenteAnexo> Obter() =>
    [
        new(CorrenteDeReceita.Comercio, RegraDeEnquadramento.AnexoFixo, AnexoSimplesNacional.I),
        new(CorrenteDeReceita.Servico, RegraDeEnquadramento.PorFatorR),
        new(CorrenteDeReceita.Recorrente, RegraDeEnquadramento.PorFatorR),
    ];
}
