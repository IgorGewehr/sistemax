namespace SistemaX.Modules.Financeiro.Application.Quant;

/// <summary>
/// Sinal cross-fato do Super Consultor (docs/financeiro/inteligencia-arquitetura.md §3.5/ADR-0005:
/// exemplo dado no próprio plano — "conta grande vence antes de receber X"). Não é uma das 22
/// análises do catálogo (não tem número #N); é um alerta ÓBVIO construído sobre os mesmos insumos
/// de <c>PrevisaoDeCaixaService</c> (contas a pagar/receber em aberto + saldo atual), pensado para
/// ser regra COMPOSTA determinística — nunca LLM (regras compostas cross-módulo são código, não
/// LLM, mesma decisão de <c>CrossModuleConsultorRules.cs</c> do roadmap).
///
/// LÓGICA: pega a MAIOR parcela de conta a pagar em aberto dentro do horizonte; projeta o caixa
/// disponível no dia do vencimento dela (saldo atual + tudo que entra até lá − tudo que sai até lá,
/// EXCETO ela mesma); se esse caixa projetado não cobre o valor da conta, é sinal — "essa conta
/// especificamente vai faltar dinheiro". Deliberadamente mais simples que a simulação com bandas
/// P5/P50/P95 de <see cref="BandasDeFluxoDeCaixa"/>: aqui é um fato determinístico único (sem
/// bootstrap, sem incerteza) — o objetivo é o alerta pontual e explicável, não a distribuição de
/// probabilidade.
///
/// P2-3 (docs/financeiro/revisao-domain-fit-cnpj.md) — consumida por
/// <c>Consultor/FinanceiroConsultorFactProvider.ColetarSinalContaGrandeAntesDeReceberAsync</c>, que
/// antes reimplementava uma heurística mais fraca inline (comparava só a MAIOR parcela a receber,
/// não a projeção ACUMULADA de caixa até o vencimento). Este é o único consumidor — não é mais
/// dead code de design.
/// </summary>
public static class SinalContaGrandeAntesDoRecebimento
{
    /// <summary>Parcela em aberto (a pagar ou a receber) já reduzida ao que a regra precisa:
    /// quanto falta pagar/receber e em quantos dias vence a partir de hoje. Parcelas já vencidas
    /// (dias negativos) chegam aqui com <see cref="DiasParaVencer"/> zerado pelo chamador —
    /// "já vencida" é tratada como urgência máxima (dia 0), não como fora do radar.</summary>
    public sealed record ParcelaAberta(string Id, long ValorCentavos, int DiasParaVencer);

    public sealed record Resultado(
        string ParcelaId,
        long ValorDaContaCentavos,
        int DiasParaVencer,
        long CaixaProjetadoAntesCentavos,
        long FaltaCentavos);

    /// <summary>Devolve <c>null</c> quando não há conta a pagar no horizonte, ou quando o caixa
    /// projetado até o vencimento da maior conta já cobre o valor dela (sem alerta a fazer —
    /// determinístico: mesmos inputs, mesmo resultado sempre).</summary>
    public static Resultado? Detectar(
        long saldoAtualCentavos,
        IReadOnlyList<ParcelaAberta> contasAPagarAbertas,
        IReadOnlyList<ParcelaAberta> contasAReceberAbertas,
        int horizonteDias)
    {
        if (horizonteDias < 0) throw new ArgumentOutOfRangeException(nameof(horizonteDias), "Horizonte deve ser >= 0.");

        var maiorConta = contasAPagarAbertas
            .Where(parcela => parcela.DiasParaVencer <= horizonteDias)
            .OrderByDescending(parcela => parcela.ValorCentavos)
            .ThenBy(parcela => parcela.DiasParaVencer)
            .ThenBy(parcela => parcela.Id, StringComparer.Ordinal)
            .FirstOrDefault();

        if (maiorConta is null) return null;

        var entradasAntes = contasAReceberAbertas
            .Where(parcela => parcela.DiasParaVencer <= maiorConta.DiasParaVencer)
            .Sum(parcela => parcela.ValorCentavos);

        var saidasAntesExcluindoEla = contasAPagarAbertas
            .Where(parcela => parcela.DiasParaVencer <= maiorConta.DiasParaVencer && parcela.Id != maiorConta.Id)
            .Sum(parcela => parcela.ValorCentavos);

        var caixaProjetadoAntes = saldoAtualCentavos + entradasAntes - saidasAntesExcluindoEla;
        var falta = maiorConta.ValorCentavos - caixaProjetadoAntes;

        return falta > 0
            ? new Resultado(maiorConta.Id, maiorConta.ValorCentavos, maiorConta.DiasParaVencer, caixaProjetadoAntes, falta)
            : null;
    }
}
