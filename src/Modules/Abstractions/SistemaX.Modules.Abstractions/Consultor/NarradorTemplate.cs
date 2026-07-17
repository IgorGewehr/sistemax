namespace SistemaX.Modules.Abstractions.Consultor;

/// <summary>
/// Narrador determinístico e grátis — o "piso" do Super Consultor (ADR-0005 §7: "o pior caminho em
/// qualquer ponto ... é <c>source: 'template'</c> — frase correta, só menos gostosa"). Cada
/// <see cref="ConsultorFato.TemplateFallback"/> já chega aqui como frase pronta, com os números
/// interpolados pelo <see cref="IConsultorFactProvider"/> que a produziu — este narrador só
/// empacota, sem tocar rede, sem custo, 100% reprodutível.
///
/// Esta é a implementação REGISTRADA nesta rodada (nenhuma chamada a LLM real, de propósito — ver
/// tarefa do Super Consultor). Quando um <c>NarradorLLM</c> existir, ele troca de lugar aqui via DI
/// (mesma porta <see cref="IConsultorNarrador"/>) sem qualquer mudança no <c>ConsultorService</c>,
/// no ranking ou na UI.
/// </summary>
public sealed class NarradorTemplate : IConsultorNarrador
{
    public Task<IReadOnlyList<ConsultorInsightNarrado>> NarrarAsync(
        IReadOnlyList<ConsultorFato> fatos, CancellationToken ct = default)
    {
        IReadOnlyList<ConsultorInsightNarrado> resultado = fatos
            .Select(fato => new ConsultorInsightNarrado(
                fato.Modulo,
                fato.RuleId,
                fato.Tela,
                fato.Score,
                fato.TemplateFallback,
                ConsultorNarracaoOrigem.Template,
                fato.Facts,
                fato.Drill))
            .ToList();

        return Task.FromResult(resultado);
    }
}
