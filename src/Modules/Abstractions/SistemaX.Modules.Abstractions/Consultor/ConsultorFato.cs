namespace SistemaX.Modules.Abstractions.Consultor;

/// <summary>
/// Recorte de tenant + dia usado como unidade de "período" pelo Super Consultor — o mesmo padrão
/// de <c>businessId + dia de hoje</c> já usado como seed determinística em
/// <c>Financeiro.Application.Quant.SeedDeterministico</c> (docs/financeiro/inteligencia-arquitetura.md
/// §3.4/ADR-0005): reprodutível dentro do mesmo dia, muda a cada dia porque o "hoje" muda.
/// </summary>
public sealed record PeriodoRef(string BusinessId, DateOnly Dia);

/// <summary>
/// Navegação read-only para a UI ir "ver como calculamos" (Lei 2 — IA observa/aconselha, nunca
/// age): um slot de tela + parâmetros opcionais para a UI montar a URL de drill-down. Nunca uma
/// ação, sempre um destino de leitura.
/// </summary>
public sealed record DrillTarget(string Tela, IReadOnlyDictionary<string, string>? Parametros = null);

/// <summary>
/// Fato bruto que um <see cref="IConsultorFactProvider"/> entrega ao pipeline do Super Consultor —
/// contrato central do plano de inteligência do Financeiro (docs/financeiro/inteligencia-arquitetura.md
/// §3.5/ADR-0005). Puramente descritivo: nenhum campo aqui é calculado pelo LLM.
///
/// <see cref="Facts"/> traz só VALORES JÁ FORMATADOS para exibição (ex.: <c>"R$ 4.200,00"</c>,
/// nunca <c>long</c> cru) — é o que efetivamente sai da máquina local rumo ao narrador (dado cru
/// jamais sai, só strings pré-formatadas). <see cref="TemplateFallback"/> é a frase pronta,
/// determinística, já interpolada com esses valores — o "piso" que <c>NarradorTemplate</c> usa e
/// que qualquer <c>NarradorLLM</c> futuro cai de volta quando falha/estoura o teto/reprova a
/// validação anti-alucinação.
/// </summary>
public sealed record ConsultorFato(
    string Modulo,
    string RuleId,
    string Tela,
    int Score,
    IReadOnlyDictionary<string, string> Facts,
    string TemplateFallback,
    DrillTarget? Drill = null);

/// <summary>Origem da frase final de um insight — nunca "erro": o pior caminho é sempre
/// <see cref="Template"/> (ver ADR-0005 §7 "Falha nunca vira erro na UI").</summary>
public enum ConsultorNarracaoOrigem
{
    Template,
    Llm,
}

/// <summary>
/// Fato já narrado — o que a UI consome. <see cref="Facts"/>/<see cref="Drill"/> viajam junto
/// (nunca só a frase) para o painel "Ver como calculamos" nunca depender do LLM.
/// </summary>
public sealed record ConsultorInsightNarrado(
    string Modulo,
    string RuleId,
    string Tela,
    int Score,
    string Frase,
    ConsultorNarracaoOrigem Origem,
    IReadOnlyDictionary<string, string> Facts,
    DrillTarget? Drill);
