namespace SistemaX.Modules.Abstractions.Consultor;

/// <summary>
/// Porta que cada módulo implementa para alimentar o Super Consultor com os fatos que já calculou
/// (docs/financeiro/inteligencia-arquitetura.md §3.5/ADR-0005: "cada módulo registra o seu via
/// IModule/DI", R5 — zero <c>if(vertical)</c> num orquestrador central). O <c>ConsultorService</c>
/// coleta de <c>IEnumerable&lt;IConsultorFactProvider&gt;</c> — plugar um módulo novo (Estoque,
/// Vendas, Compras, Fiscal — Fase 3 do roadmap) é só registrar mais uma implementação, zero
/// mudança no pipeline/ranking/narração/cache, que já existem e são module-agnostic.
///
/// Implementação DETERMINÍSTICA e testável: nada aqui chama LLM — só lê read-models/fact tables já
/// calculados e formata os números em <see cref="ConsultorFato.Facts"/>.
/// </summary>
public interface IConsultorFactProvider
{
    Task<IReadOnlyList<ConsultorFato>> ColetarAsync(PeriodoRef periodo, CancellationToken ct = default);
}
