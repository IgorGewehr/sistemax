namespace SistemaX.Modules.Financeiro.Application.Quant;

/// <summary>
/// LAR ÚNICO do payback (docs/financeiro/design-imobilizado-roi.md §6/§10) — extraído da simulação
/// determinística §9.5 de docs/financeiro/design-analise-por-projeto.md: "primeiro mês em que um
/// acumulado cruza zero", usado tanto por <c>ReadModels.RoiDoNegocioService</c> (o painel do
/// NEGÓCIO) quanto — quando o design-pai P3 rodar — por <c>Projetos.PainelDoProjetoService</c> (o
/// painel de UM projeto). O reuso é da MATEMÁTICA, nunca da tela (§6 do design): as duas lentes têm
/// fontes de fluxo diferentes, mas "acumular e achar o primeiro cruzamento" é a MESMA função pura.
/// </summary>
public static class MatematicaDePayback
{
    /// <summary>
    /// Payback SIMPLES (§7.3): menor competência T tal que <c>Acum(T) ≥ 0</c>, tendo existido um
    /// t &lt; T com <c>Acum(t) &lt; 0</c> — "cruzou de negativo pra não-negativo", nunca "nunca ficou
    /// negativo" (fluxo sempre positivo não tem payback A REALIZAR, é <c>null</c> por definição do
    /// cruzamento; o chamador decide separadamente se isso é "já pago desde o início").
    /// <paramref name="serieMensal"/> deve vir ORDENADA por competência crescente, cobrindo
    /// <c>m0..T</c> — a função não reordena nem preenche buracos, mesma responsabilidade do
    /// chamador em <c>PainelDoProjetoService.CalcularFluxoRealizado</c>.
    /// </summary>
    public static DateOnly? PaybackSimples(IReadOnlyList<(DateOnly Competencia, long LiquidoCentavos)> serieMensal)
    {
        long acumulado = 0;
        var jaFoiNegativo = false;

        foreach (var (competencia, liquido) in serieMensal)
        {
            if (acumulado < 0) jaFoiNegativo = true;
            acumulado += liquido;

            if (jaFoiNegativo && acumulado >= 0) return competencia;
        }
        return null;
    }

    /// <summary>
    /// Payback DESCONTADO (§7.5): mesma regra de cruzamento do <see cref="PaybackSimples"/>, mas
    /// sobre o fluxo trazido a valor presente na taxa MENSAL <paramref name="taxaMensal"/> — o
    /// índice <c>i</c> na série é o expoente do desconto (mês 0 = primeira competência da série,
    /// sem desconto; mês 1 = segunda competência, descontada uma vez; …).
    /// </summary>
    public static DateOnly? PaybackDescontado(IReadOnlyList<(DateOnly Competencia, long LiquidoCentavos)> serieMensal, decimal taxaMensal)
    {
        decimal acumulado = 0;
        var jaFoiNegativo = false;

        for (var indice = 0; indice < serieMensal.Count; indice++)
        {
            var (competencia, liquido) = serieMensal[indice];
            var fatorDesconto = (decimal)Math.Pow((double)(1 + taxaMensal), -indice);
            var valorDescontado = liquido * fatorDesconto;

            if (acumulado < 0) jaFoiNegativo = true;
            acumulado += valorDescontado;

            if (jaFoiNegativo && acumulado >= 0) return competencia;
        }
        return null;
    }

    /// <summary>
    /// Projeção DETERMINÍSTICA mês a mês (§7.7/§9.5 do design-pai) — NUNCA fórmula de bolso: dado o
    /// acumulado de HOJE (<paramref name="acumuladoAtual"/>) e o fluxo futuro do mês k
    /// (<paramref name="fluxoFuturoNoMes"/>, k = 1, 2, 3…, já líquido de margem − capex
    /// comprometido, já descontado se o chamador quiser payback descontado projetado), simula
    /// <paramref name="horizonteMeses"/> meses à frente e devolve o primeiro k com
    /// <c>AcumProj(k) ≥ 0</c>. Retorna <c>0</c> se o acumulado de hoje já é ≥ 0 (ROI já completo —
    /// nunca <c>null</c> disfarçando "já atingido" de "nunca atinge"); <c>null</c> se não cruza no
    /// horizonte.
    /// </summary>
    public static int? ProjetarCruzamento(long acumuladoAtual, Func<int, long> fluxoFuturoNoMes, int horizonteMeses)
    {
        if (acumuladoAtual >= 0) return 0;

        var acumulado = acumuladoAtual;
        for (var k = 1; k <= horizonteMeses; k++)
        {
            acumulado += fluxoFuturoNoMes(k);
            if (acumulado >= 0) return k;
        }
        return null;
    }

    /// <summary>Mesma projeção de <see cref="ProjetarCruzamento(long,Func{int,long},int)"/>, em
    /// <c>decimal</c> — usada pela variante DESCONTADA (§7.7: <c>descontadoProjetadoMeses</c>), cujo
    /// fluxo futuro já vem multiplicado pelo fator de desconto do chamador (fracionário, não mais
    /// centavos inteiros).</summary>
    public static int? ProjetarCruzamento(decimal acumuladoAtual, Func<int, decimal> fluxoFuturoNoMes, int horizonteMeses)
    {
        if (acumuladoAtual >= 0) return 0;

        var acumulado = acumuladoAtual;
        for (var k = 1; k <= horizonteMeses; k++)
        {
            acumulado += fluxoFuturoNoMes(k);
            if (acumulado >= 0) return k;
        }
        return null;
    }
}
