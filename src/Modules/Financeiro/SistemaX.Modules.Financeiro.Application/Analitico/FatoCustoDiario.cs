using SistemaX.Modules.Financeiro.Domain.Comum;

namespace SistemaX.Modules.Financeiro.Application.Analitico;

/// <summary>
/// Fact table que fecha o gap documentado no plano de inteligência do Financeiro
/// (docs/financeiro/inteligencia-arquitetura.md §3.3/ADR-0005, item "CMV economicamente errado"):
/// <c>CustoBaixadoPorVenda</c> (Estoque→Financeiro) é publicado de verdade e persiste no ledger,
/// mas antes desta tabela nenhum fold reagia a ele — o CMV entrava no ledger e não chegava a
/// nenhuma tabela consultável. <see cref="FatoCustoDiarioProjection"/> faz esse fold: CMV
/// RECONHECIDO (mesma base de competência de <see cref="FatoReceitaDiaria"/>) por dia local do
/// tenant.
///
/// Deliberadamente uma tabela PRÓPRIA, não uma coluna a mais em <see cref="FatoReceitaDiaria"/>:
/// custo e receita nascem de eventos diferentes (<c>CustoBaixadoPorVenda</c> vs.
/// <c>VendaConcluida</c>/<c>PedidoPago</c>/<c>OsFaturada</c>), e a Fase 1 do roadmap já reserva
/// <c>fato_margem_produto</c> para a versão com granularidade por produto — esta tabela é o
/// primeiro degrau (granularidade diária), consultável já na F0, sem contaminar a semântica de
/// "receita" com uma dimensão de custo. Margem do dia = <c>fato_receita_diaria.ReceitaCentavos -
/// fato_custo_diario.CustoCentavos</c> (mesmo tenant/dia/corrente).
///
/// <see cref="Corrente"/> (P0-1) — chave é (tenant, dia, corrente), mesmo racional de
/// <see cref="FatoReceitaDiaria"/>. Hoje o único evento que folda aqui (<c>CustoBaixadoPorVenda</c>)
/// é sempre <see cref="CorrenteDeReceita.Comercio"/> (nasce de uma venda) — quando P0-5
/// (<c>CustoBaixadoPorOs</c>) existir, essa projeção também gravará linhas
/// <see cref="CorrenteDeReceita.Servico"/>.
/// </summary>
public sealed record FatoCustoDiario(string TenantId, DateOnly Dia, CorrenteDeReceita Corrente, long CustoCentavos, DateTimeOffset AtualizadoEmUtc);
