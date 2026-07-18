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
    /// leitura NUNCA depende do cron ter rodado — só do calendário). BAIXA-AWARE: se o ativo foi
    /// baixado antecipadamente (§4.5/§4.6 dos dois designs), a competência da baixa contribui o
    /// VALOR CONTÁBIL reconhecido de uma vez (não a fatia linear daquele mês) e nenhuma competência
    /// posterior contribui nada — a leitura nunca depende de recomputar a baixa, só do estado
    /// persistido do agregado (<see cref="AtivoDeCapital.UltimaCompetenciaReconhecida"/> é
    /// avançado para a competência da baixa por <see cref="AtivoDeCapital.Baixar"/>).</summary>
    public static long SomaNaJanela(AtivoDeCapital ativo, DateOnly de, DateOnly ate)
    {
        DateOnly? competenciaBaixa = ativo.Status == StatusAtivoDeCapital.Baixado && ativo.UltimaCompetenciaReconhecida is { } ultima
            ? new DateOnly(ultima.Year, ultima.Month, 1)
            : null;

        long soma = 0;
        foreach (var (c, v) in Cronograma(ativo))
        {
            if (c < de || c > ate) continue;

            if (competenciaBaixa is { } cb)
            {
                if (c > cb) continue; // nada reconhece depois da baixa
                if (c == cb)
                {
                    soma += ativo.ValorReconhecidoNaBaixaCentavos ?? v;
                    continue;
                }
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
    /// (Σ cronograma == BaseDepreciavel).</summary>
    public static long ValorContabilAtualCentavos(AtivoDeCapital ativo)
        => ativo.CustoAquisicao.Centavos - ReconhecidoAteOCursor(ativo);
}
