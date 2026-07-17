using SistemaX.Modules.Financeiro.Application.Categorias;
using SistemaX.Modules.Financeiro.Application.Comum;
using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Domain.Comum;
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
/// </summary>
public sealed record DreResultado(
    Money ReceitaBruta, Money CustoDireto, Money DespesaOperacional, Money ResultadoOperacional,
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
    IContaAReceberRepository contasAReceber, IContaAPagarRepository contasAPagar, IFatoCustoDiarioRepository fatoCustoDiario)
{
    private static readonly CorrenteDeReceita[] TodasAsCorrentes =
        [CorrenteDeReceita.Recorrente, CorrenteDeReceita.Servico, CorrenteDeReceita.Comercio];

    public async Task<DreResultado> CalcularAsync(string businessId, DateTimeOffset inicio, DateTimeOffset fim, CancellationToken ct = default)
    {
        var receitas = await contasAReceber.ListarPorCompetenciaAsync(businessId, inicio, fim, ct);
        var despesas = await contasAPagar.ListarPorCompetenciaAsync(businessId, inicio, fim, ct);
        var fatosCusto = await ListarFatoCustoAsync(businessId, inicio, fim, ct);
        var cmvReal = new Money(fatosCusto.Sum(f => f.CustoCentavos));

        var receitasReconhecidas = receitas.Where(c => c.Status != StatusFinanceiro.Cancelado).ToList();
        var receitaBruta = Somar(receitasReconhecidas);
        var receitaRecorrente = Somar(receitasReconhecidas.Where(c => c.CategoriaId == CategoriaFinanceiraPadrao.ReceitaRecorrente));
        var receitaOperacional = receitaBruta - receitaRecorrente;

        var comissoes = Somar(despesas.Where(c =>
            c.Status != StatusFinanceiro.Cancelado && c.CategoriaId == CategoriaFinanceiraPadrao.Comissoes));
        var custoDireto = cmvReal + comissoes;

        var despesaOperacional = Somar(despesas.Where(c =>
            c.Status != StatusFinanceiro.Cancelado &&
            c.CategoriaId != CategoriaFinanceiraPadrao.CustoMercadoriaVendida &&
            c.CategoriaId != CategoriaFinanceiraPadrao.Comissoes));

        var resultadoOperacional = receitaBruta - custoDireto - despesaOperacional;

        var porCorrente = CalcularPorCorrente(receitasReconhecidas, despesas, fatosCusto);

        return new DreResultado(
            receitaBruta, custoDireto, despesaOperacional, resultadoOperacional,
            receitaRecorrente, receitaOperacional, porCorrente);
    }

    /// <summary>
    /// Quebra por corrente (P0-1): receita reconhecida com a <c>Corrente</c> gravada na origem
    /// (fallback: <see cref="CorrenteDeReceitaInferencia.InferirDaCategoria"/> — mesma rede de
    /// segurança do backfill de migração, nunca diverge dela); custo direto = CMV real da corrente
    /// (<c>fato_custo_diario</c>, sempre classificado pelo fold) + comissão categorizada nela.
    /// </summary>
    private static IReadOnlyList<DrePorCorrente> CalcularPorCorrente(
        IReadOnlyList<Domain.ContasAPagarReceber.ContaFinanceiraBase> receitasReconhecidas,
        IReadOnlyList<Domain.ContasAPagarReceber.ContaFinanceiraBase> despesas,
        IReadOnlyList<Analitico.FatoCustoDiario> fatosCusto)
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
            var receitaBruta = Somar(receitasReconhecidas.Where(c => ClassificarCorrente(c) == corrente));
            var comissao = Somar(comissoesAbertas.Where(c => ClassificarCorrente(c) == corrente));
            var custoDireto = cmvPorCorrente.GetValueOrDefault(corrente, Money.Zero) + comissao;
            resultado.Add(new DrePorCorrente(corrente, receitaBruta, custoDireto, receitaBruta - custoDireto));
        }
        return resultado;
    }

    private static CorrenteDeReceita? ClassificarCorrente(Domain.ContasAPagarReceber.ContaFinanceiraBase conta)
        => conta.Corrente ?? CorrenteDeReceitaInferencia.InferirDaCategoria(conta.CategoriaId);

    private Task<IReadOnlyList<Analitico.FatoCustoDiario>> ListarFatoCustoAsync(string businessId, DateTimeOffset inicio, DateTimeOffset fim, CancellationToken ct)
    {
        var de = BucketingTemporalDoTenant.DiaLocal(inicio);
        var ate = BucketingTemporalDoTenant.DiaLocal(fim);
        return fatoCustoDiario.ListarAsync(businessId, de, ate, ct);
    }

    private static Money Somar(IEnumerable<Domain.ContasAPagarReceber.ContaFinanceiraBase> contas)
        => contas.Aggregate(Money.Zero, (acumulado, conta) => acumulado + conta.ValorTotal);
}
