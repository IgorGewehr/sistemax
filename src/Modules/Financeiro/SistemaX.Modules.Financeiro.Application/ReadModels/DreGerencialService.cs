using SistemaX.Modules.Financeiro.Application.Categorias;
using SistemaX.Modules.Financeiro.Application.Comum;
using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Application.Quant;
using SistemaX.Modules.Financeiro.Domain.Comum;
using SistemaX.Modules.Financeiro.Domain.ContasAPagarReceber;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Application.ReadModels;

/// <summary>
/// DRE gerencial simplificado — POR COMPETÊNCIA, não por caixa (docs/financeiro-features.md
/// §4.4): "esse mês, considerando tudo que foi vendido/gasto (pago ou não), sobrou quanto de
/// verdade?". SIMPLIFICAÇÃO DO MVP: sem <c>ICategoriaRepository</c>/<c>LinhaDre</c> resolvidos de
/// verdade neste estágio (ver <see cref="CategoriaFinanceiraPadrao"/>), classificamos direto pelo
/// slug de categoria conhecido. Fase 2: resolver <c>Categoria.LinhaDreId</c> de verdade e agrupar
/// por <c>LinhaDre</c> arbitrária, não só pelas 3 categorias nativas do catálogo de handlers.
///
/// CMV (docs/financeiro/inteligencia-arquitetura.md §2 item "CMV economicamente errado" / §6 F1
/// pendência): o custo de mercadoria vendida NÃO vem mais do <c>ContaAPagar</c> gerado por
/// <c>CompraRecebida</c> — comprar estoque é troca de ativo (caixa/dívida por mercadoria), não
/// despesa do período; contá-lo como custo no mês da compra infla o CMV de meses de reposição e
/// zera o CMV de meses só de venda. O CMV real é o RECONHECIDO por venda, foldado do ledger em
/// <c>fato_custo_diario</c> (<see cref="Analitico.FatoCustoDiarioProjection"/>, alimentado por
/// <c>CustoBaixadoPorVenda</c> — mesma base de competência de <c>fato_receita_diaria</c>).
/// <c>ContaAPagar</c> com <see cref="CategoriaFinanceiraPadrao.CustoMercadoriaVendida"/> continua
/// existindo (é o registro de que a compra foi feita/precisa ser paga) mas SAI do cálculo do DRE —
/// nem custo direto, nem despesa operacional; é balanço (estoque), não resultado.
///
/// <c>ReceitaRecorrente</c>/<c>ReceitaOperacional</c> (P0-1, docs/financeiro/revisao-domain-fit-cnpj.md):
/// primeiro corte, ADITIVO, da dimensão "corrente de receita" — quanto de <c>ReceitaBruta</c> é MRR
/// de assinatura vs. o resto. Preservados por compatibilidade — quem só olha os 4 campos originais
/// não quebra — mas <see cref="PorCorrente"/> é a versão COMPLETA da dimensão (Recorrente × Servico
/// × Comercio, com custo direto e margem de cada uma), formalizada com o enum
/// <see cref="CorrenteDeReceita"/> gravado em <c>ContaAReceber.Corrente</c>/<c>ContaAPagar.Corrente</c>/
/// <c>fato_custo_diario.Corrente</c> em vez de inferida só pela categoria.
///
/// <c>DespesaFinanceira</c> (P1-6, docs/financeiro/revisao-domain-fit-cnpj.md) — MDR (taxa de
/// cartão) do período, derivado AO VIVO de <c>fato_recebiveis</c> (soma de <c>ValorBrutoCentavos
/// − ValorLiquidoCentavos</c> de todo recebível com <c>Vencimento</c> na competência): nunca
/// recomputa a taxa — <c>fato_recebiveis</c> já resolveu o MDR contra o lar único
/// <c>FormaDePagamento</c> no momento em que a linha nasceu. Reduz <see cref="ResultadoOperacional"/>
/// sem entrar em <see cref="CustoDireto"/> (não é CMV/comissão — é despesa financeira). Dinheiro/PIX
/// (taxa 0%) contribuem zero: venda sem cartão não gera esta despesa.
///
/// RECEITA DIFERIDA (P1-5, docs/financeiro/revisao-domain-fit-cnpj.md) — <c>ReceitaBruta</c>,
/// <c>ReceitaRecorrente</c> e a receita de cada <see cref="DrePorCorrente"/> não somam mais
/// <c>ContaAReceber.ValorTotal</c> direto: passam por <see cref="ReceitaReconhecidaResolver"/>, que
/// reconhece pró-rata (via <c>CronogramaLinear</c>) as cobranças de assinatura de ciclo &gt;
/// mensal. Por isso a consulta amplia a janela pra TRÁS (até
/// <see cref="ReceitaReconhecidaResolver.MaiorHorizonteDeReconhecimentoEmMeses"/> competências)
/// antes de filtrar: uma cobrança ANUAL feita 5 meses atrás ainda tem fração reconhecida no mês
/// corrente, mesmo com <c>DataCompetencia</c> fora da janela pedida. O RECEBÍVEL
/// (<c>ContaAReceber</c>) nunca muda — só esta leitura separa caixa (a cobrança) de competência
/// (o reconhecimento). Despesas (<c>ContaAPagar</c>) não são afetadas — nenhuma tem reconhecimento
/// diferido hoje.
/// </summary>
public sealed record DreResultado(
    Money ReceitaBruta, Money CustoDireto, Money DespesaOperacional, Money DespesaFinanceira, Money ResultadoOperacional,
    Money ReceitaRecorrente, Money ReceitaOperacional,
    IReadOnlyList<DrePorCorrente> PorCorrente);

/// <summary>
/// Unit economics de UMA corrente de receita (P0-1) — receita bruta reconhecida, custo direto
/// (CMV real da corrente + comissão/custo direto categorizado nela) e margem de contribuição
/// (<c>ReceitaBruta - CustoDireto</c>). <c>Σ PorCorrente.ReceitaBruta ≤ DreResultado.ReceitaBruta</c>
/// — igual quando toda receita do período está tagueada com uma corrente conhecida (o caso comum:
/// venda, OS e assinatura sempre marcam a corrente na origem — só <c>Recorrencia</c> genérica sem
/// categoria reconhecida fica de fora, ver <see cref="CorrenteDeReceitaInferencia"/>).
/// </summary>
public sealed record DrePorCorrente(CorrenteDeReceita Corrente, Money ReceitaBruta, Money CustoDireto, Money Margem);

public sealed class DreGerencialService(
    IContaAReceberRepository contasAReceber, IContaAPagarRepository contasAPagar,
    IFatoCustoDiarioRepository fatoCustoDiario, IFatoRecebiveisRepository fatoRecebiveis)
{
    private static readonly CorrenteDeReceita[] TodasAsCorrentes =
        [CorrenteDeReceita.Recorrente, CorrenteDeReceita.Servico, CorrenteDeReceita.Comercio];

    public async Task<DreResultado> CalcularAsync(string businessId, DateTimeOffset inicio, DateTimeOffset fim, CancellationToken ct = default)
    {
        // P1-5 — janela AMPLIADA pra trás: uma cobrança anual feita até 11 meses antes de `inicio`
        // ainda pode ter fração reconhecida caindo dentro de [inicio, fim]. ReceitaReconhecidaResolver
        // descarta, por conta própria, tudo que não reconhece na janela pedida — a lista mais larga
        // aqui é só o INSUMO, nunca o resultado.
        var janelaAmpliadaInicio = inicio.AddMonths(-(ReceitaReconhecidaResolver.MaiorHorizonteDeReconhecimentoEmMeses - 1));
        var receitas = await contasAReceber.ListarPorCompetenciaAsync(businessId, janelaAmpliadaInicio, fim, ct);
        var despesas = await contasAPagar.ListarPorCompetenciaAsync(businessId, inicio, fim, ct);
        var fatosCusto = await ListarFatoCustoAsync(businessId, inicio, fim, ct);
        var cmvReal = new Money(fatosCusto.Sum(f => f.CustoCentavos));
        var despesaFinanceira = await CalcularMdrDoPeriodoAsync(businessId, inicio, fim, ct);

        var receitasReconhecidas = receitas.Where(c => c.Status != StatusFinanceiro.Cancelado).ToList();
        var receitaBruta = SomarReconhecida(receitasReconhecidas, inicio, fim);
        var receitaRecorrente = SomarReconhecida(receitasReconhecidas.Where(c => c.CategoriaId == CategoriaFinanceiraPadrao.ReceitaRecorrente), inicio, fim);
        var receitaOperacional = receitaBruta - receitaRecorrente;

        var comissoes = Somar(despesas.Where(c =>
            c.Status != StatusFinanceiro.Cancelado && c.CategoriaId == CategoriaFinanceiraPadrao.Comissoes));
        var custoDireto = cmvReal + comissoes;

        var despesaOperacional = Somar(despesas.Where(c =>
            c.Status != StatusFinanceiro.Cancelado &&
            c.CategoriaId != CategoriaFinanceiraPadrao.CustoMercadoriaVendida &&
            c.CategoriaId != CategoriaFinanceiraPadrao.Comissoes));

        var resultadoOperacional = receitaBruta - custoDireto - despesaOperacional - despesaFinanceira;

        var porCorrente = CalcularPorCorrente(receitasReconhecidas, despesas, fatosCusto, inicio, fim);

        return new DreResultado(
            receitaBruta, custoDireto, despesaOperacional, despesaFinanceira, resultadoOperacional,
            receitaRecorrente, receitaOperacional, porCorrente);
    }

    /// <summary>
    /// P1-6 — soma <c>ValorBrutoCentavos − ValorLiquidoCentavos</c> de todo <c>fato_recebiveis</c>
    /// com <c>Vencimento</c> na competência do período: essa diferença JÁ É o MDR (a taxa foi
    /// aplicada uma vez só, na origem, por <c>FatoRecebiveisProjection</c> contra
    /// <c>FormaDePagamento</c>) — nunca uma segunda resolução de taxa. Dinheiro/PIX (0% de taxa)
    /// e a compensação de estorno somam exatamente zero de MDR líquido no par venda+estorno.
    /// </summary>
    private async Task<Money> CalcularMdrDoPeriodoAsync(string businessId, DateTimeOffset inicio, DateTimeOffset fim, CancellationToken ct)
    {
        var de = BucketingTemporalDoTenant.DiaLocal(inicio);
        var ate = BucketingTemporalDoTenant.DiaLocal(fim);
        var recebiveis = await fatoRecebiveis.ListarPorVencimentoAsync(businessId, de, ate, ct).ConfigureAwait(false);
        var mdrCentavos = recebiveis.Sum(r => r.ValorBrutoCentavos - r.ValorLiquidoCentavos);
        return new Money(mdrCentavos);
    }

    /// <summary>
    /// Quebra por corrente (P0-1): receita reconhecida com a <c>Corrente</c> gravada na origem
    /// (fallback: <see cref="CorrenteDeReceitaInferencia.InferirDaCategoria"/> — mesma rede de
    /// segurança do backfill de migração, nunca diverge dela); custo direto = CMV real da corrente
    /// (<c>fato_custo_diario</c>, sempre classificado pelo fold) + comissão categorizada nela.
    /// </summary>
    private static IReadOnlyList<DrePorCorrente> CalcularPorCorrente(
        IReadOnlyList<ContaFinanceiraBase> receitasReconhecidas,
        IReadOnlyList<ContaFinanceiraBase> despesas,
        IReadOnlyList<Analitico.FatoCustoDiario> fatosCusto,
        DateTimeOffset inicio, DateTimeOffset fim)
    {
        var cmvPorCorrente = fatosCusto
            .GroupBy(f => f.Corrente)
            .ToDictionary(g => g.Key, g => new Money(g.Sum(f => f.CustoCentavos)));

        var comissoesAbertas = despesas
            .Where(c => c.Status != StatusFinanceiro.Cancelado && c.CategoriaId == CategoriaFinanceiraPadrao.Comissoes)
            .ToList();

        var resultado = new List<DrePorCorrente>(TodasAsCorrentes.Length);
        foreach (var corrente in TodasAsCorrentes)
        {
            var receitaBruta = SomarReconhecida(receitasReconhecidas.Where(c => ClassificarCorrente(c) == corrente), inicio, fim);
            var comissao = Somar(comissoesAbertas.Where(c => ClassificarCorrente(c) == corrente));
            var custoDireto = cmvPorCorrente.GetValueOrDefault(corrente, Money.Zero) + comissao;
            resultado.Add(new DrePorCorrente(corrente, receitaBruta, custoDireto, receitaBruta - custoDireto));
        }
        return resultado;
    }

    private static CorrenteDeReceita? ClassificarCorrente(ContaFinanceiraBase conta)
        => conta.Corrente ?? CorrenteDeReceitaInferencia.InferirDaCategoria(conta.CategoriaId);

    private Task<IReadOnlyList<Analitico.FatoCustoDiario>> ListarFatoCustoAsync(string businessId, DateTimeOffset inicio, DateTimeOffset fim, CancellationToken ct)
    {
        var de = BucketingTemporalDoTenant.DiaLocal(inicio);
        var ate = BucketingTemporalDoTenant.DiaLocal(fim);
        return fatoCustoDiario.ListarAsync(businessId, de, ate, ct);
    }

    private static Money Somar(IEnumerable<ContaFinanceiraBase> contas)
        => contas.Aggregate(Money.Zero, (acumulado, conta) => acumulado + conta.ValorTotal);

    /// <summary>P1-5 — soma a fração RECONHECIDA de cada conta na janela [inicio,fim], via
    /// <see cref="ReceitaReconhecidaResolver"/>. Para contas sem reconhecimento diferido, é
    /// idêntico a <see cref="Somar"/> (a fração é 100% do valor, se a competência cair na janela) —
    /// nunca muda o comportamento de venda/OS/lançamento manual.</summary>
    private static Money SomarReconhecida(IEnumerable<ContaFinanceiraBase> contas, DateTimeOffset inicio, DateTimeOffset fim)
        => new(contas.Sum(c => ReceitaReconhecidaResolver.CentavosNaJanela(c, inicio, fim)));
}
