using SistemaX.Modules.Financeiro.Application.Quant;
using SistemaX.Modules.Financeiro.Domain.Ativos;

namespace SistemaX.Modules.Financeiro.Application.Ativos;

/// <summary>
/// LAR ÚNICO de "quanto este AtivoDeCapital reconhece em qual competência" — todo leitor (DRE,
/// painel do projeto, o cron de reconhecimento, a baixa antecipada) recomputa daqui, nunca cacheia
/// (docs/financeiro/design-analise-por-projeto.md §4.2: "cron atrasado nunca produz número
/// errado"). Envolve <see cref="CronogramaLinear"/> — que <c>Domain.Ativos.AtivoDeCapital</c> não
/// pode chamar diretamente (Domain não referencia Application) — por isso este helper vive aqui,
/// não no agregado.
/// </summary>
public static class AtivoDeCapitalQuant
{
    /// <summary>O cronograma completo (Hamilton, Σ = <see cref="AtivoDeCapital.BaseDepreciavel"/>) —
    /// função pura, recomputada a cada chamada.</summary>
    public static IReadOnlyList<(DateOnly Competencia, long ValorCentavos)> Cronograma(AtivoDeCapital ativo)
        => CronogramaLinear.Gerar(ativo.BaseDepreciavel.Centavos, ativo.VidaUtilMeses, ativo.InicioDepreciacao);

    /// <summary>Valor da competência exata (0 se fora do cronograma — ex.: ativo baixado antes).</summary>
    public static long ValorNaCompetencia(AtivoDeCapital ativo, DateOnly competencia)
    {
        foreach (var (c, v) in Cronograma(ativo))
        {
            if (c == competencia) return v;
        }
        return 0;
    }

    /// <summary>Soma do cronograma na janela [de, ate] (inclusive) — usado pelo DRE/painel para "a
    /// amortização/depreciação reconhecida neste período", independente do cursor do cron (a
    /// leitura NUNCA depende do cron ter rodado — só do calendário). SAÍDA-AWARE (baixa OU venda):
    /// nenhuma competência POSTERIOR à saída contribui nada, em nenhum dos dois casos — a leitura
    /// nunca depende de recomputar a saída, só do estado persistido do agregado
    /// (<see cref="AtivoDeCapital.UltimaCompetenciaReconhecida"/> é avançado para a competência da
    /// saída por <see cref="AtivoDeCapital.Baixar"/>). NA competência da saída em si, os dois casos
    /// DIVERGEM (docs/financeiro/design-imobilizado-roi.md §4.6, DI6): write-off
    /// (<see cref="StatusAtivoDeCapital.Baixado"/>) reconhece o VALOR CONTÁBIL inteiro de uma vez
    /// (perda real de capacidade, permanece DENTRO do D&A/ResultadoOperacional — herdado do
    /// design-pai §4.6); venda (<see cref="StatusAtivoDeCapital.Vendido"/>) NÃO — fica só a fatia
    /// linear normal daquele mês, e o valor contábil restante (<see cref="AtivoDeCapital.ValorReconhecidoNaBaixaCentavos"/>)
    /// vira <see cref="AtivoDeCapital.ResultadoAlienacaoCentavos"/>, linha informativa FORA do
    /// resultado operacional — nunca duplicado aqui.</summary>
    public static long SomaNaJanela(AtivoDeCapital ativo, DateOnly de, DateOnly ate)
    {
        DateOnly? competenciaSaida = ativo.Status is StatusAtivoDeCapital.Baixado or StatusAtivoDeCapital.Vendido
            && ativo.UltimaCompetenciaReconhecida is { } ultima
            ? new DateOnly(ultima.Year, ultima.Month, 1)
            : null;

        long soma = 0;
        foreach (var (c, v) in Cronograma(ativo))
        {
            if (c < de || c > ate) continue;

            if (competenciaSaida is { } cs)
            {
                if (c > cs) continue; // nada reconhece depois da baixa/venda

                if (c == cs && ativo.Status == StatusAtivoDeCapital.Baixado)
                {
                    soma += ativo.ValorReconhecidoNaBaixaCentavos ?? v;
                    continue;
                }
                // Vendido: cai no `soma += v` de baixo — a fatia linear normal do mês, sem lump sum.
            }

            soma += v;
        }
        return soma;
    }

    /// <summary>Soma reconhecida ATÉ (e incluindo) <see cref="AtivoDeCapital.UltimaCompetenciaReconhecida"/>
    /// — insumo do valor contábil para a baixa antecipada (§4.5/§4.6 dos dois designs).</summary>
    public static long ReconhecidoAteOCursor(AtivoDeCapital ativo)
    {
        if (ativo.UltimaCompetenciaReconhecida is not { } ultima) return 0;
        var ate = new DateOnly(ultima.Year, ultima.Month, 1);
        return SomaNaJanela(ativo, DateOnly.MinValue, ate);
    }

    /// <summary>Valor contábil ATUAL (na data do cursor) — <c>CustoAquisicao − reconhecido</c>; ao
    /// fim da vida útil, converge para <see cref="AtivoDeCapital.ValorResidual"/> por construção
    /// (Σ cronograma == BaseDepreciavel). ZERO após <see cref="StatusAtivoDeCapital.Baixado"/> OU
    /// <see cref="StatusAtivoDeCapital.Vendido"/> — o bem saiu do balanço, independente de o D&A
    /// (<see cref="SomaNaJanela"/>) ter ou não reconhecido o resíduo inteiro naquele mês (invariante
    /// de teste #8, docs/financeiro/design-imobilizado-roi.md §14: "saldo de 1.3 zera após
    /// baixa/venda"); curto-circuito deliberado, independente de <see cref="ReconhecidoAteOCursor"/>.</summary>
    public static long ValorContabilAtualCentavos(AtivoDeCapital ativo)
    {
        if (ativo.Status is StatusAtivoDeCapital.Baixado or StatusAtivoDeCapital.Vendido) return 0;
        return ativo.CustoAquisicao.Centavos - ReconhecidoAteOCursor(ativo);
    }
}
