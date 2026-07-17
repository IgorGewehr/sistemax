using SistemaX.Modules.Financeiro.Domain.Comum;

namespace SistemaX.Modules.Financeiro.Application.Analitico;

/// <summary>
/// Fact table de PROVA da F0 do plano de inteligência do Financeiro
/// (docs/financeiro/inteligencia-arquitetura.md/ADR-0005) — receita diária RECONHECIDA
/// (competência), foldada do ledger <c>integration_events</c> por
/// <see cref="FatoReceitaDiariaProjection"/>. <see cref="Dia"/> já vem bucketado no fuso do tenant
/// (ver <c>BucketingTemporalDoTenant</c>) — nunca UTC cru.
///
/// <see cref="Corrente"/> (P0-1, docs/financeiro/revisao-domain-fit-cnpj.md) é parte da CHAVE —
/// a linha é por (tenant, dia, corrente), não só (tenant, dia): cada evento fold sabe exatamente
/// de qual corrente é sua receita (venda/pedido = Comercio, OS = Servico — ver
/// <see cref="FatoReceitaDiariaProjection"/>), então não há "corrente desconhecida" aqui como pode
/// haver em <c>ContaAReceber.Corrente</c> (nullable, aditivo). Consumidores que só querem o TOTAL
/// do dia (<c>RadarDoSimplesService</c>, <c>PontoDeEquilibrioService</c>) continuam somando todas
/// as linhas retornadas por <c>ListarAsync</c> sem precisar saber desta dimensão — o total é
/// sempre a soma das correntes.
/// </summary>
public sealed record FatoReceitaDiaria(string TenantId, DateOnly Dia, CorrenteDeReceita Corrente, long ReceitaCentavos, DateTimeOffset AtualizadoEmUtc);
