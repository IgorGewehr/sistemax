using SistemaX.Modules.Financeiro.Domain.Caixa;
using SistemaX.Modules.Financeiro.Domain.ContasAPagarReceber;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Domain.Contabil;

/// <summary>
/// O MAPEAMENTO determinístico fato-de-negócio → partidas contábeis. É CÓDIGO, não input do
/// usuário — é isso que torna a partida dobrada uma checagem de integridade automática e
/// invisível, nunca um fardo cognitivo para o dono leigo (docs/financeiro-datamodel.md §1,
/// opção C). Cada <c>ContaAPagar</c>/<c>ContaAReceber</c> gera exatamente 1
/// <see cref="LancamentoContabil"/> de COMPETÊNCIA ao ser criada; cada <c>MovimentoFinanceiro</c>
/// gera exatamente 1 lançamento de CAIXA ao ser registrado. O caso de uso da camada de aplicação
/// chama estes métodos logo após persistir o fato single-entry correspondente — nunca o
/// contrário (o fato single-entry é sempre a fonte, o lançamento contábil é sempre derivado).
///
/// Estorno NÃO passa por aqui: usa-se <see cref="LancamentoContabil.GerarEstorno"/> diretamente
/// sobre o lançamento original (espelha as partidas existentes), preservando o vínculo explícito
/// entre o lançamento de estorno e o lançamento original que ele reverte.
/// </summary>
public static class LancamentoContabilFactory
{
    /// <summary>
    /// Análise por Projeto/Imobilizado (docs/financeiro/design-analise-por-projeto.md §4.4,
    /// docs/financeiro/design-imobilizado-roi.md §4.4) — slug da categoria que desvia
    /// <see cref="DeContaAPagar"/> para debitar <see cref="PlanoDeContasPadrao.AtivosDeCapital"/> em
    /// vez de <see cref="PlanoDeContasPadrao.CustoDespesa"/>. Vive AQUI (não em
    /// <c>Application.Categorias.CategoriaFinanceiraPadrao</c>, que o reexporta) porque este é o
    /// ÚNICO lar do mapeamento fato→partidas — Domain não pode depender de Application para ler a
    /// constante de volta.
    /// </summary>
    public const string CategoriaAtivoDeCapital = "ativo-de-capital";

    public static Result<LancamentoContabil> DeContaAReceber(ContaAReceber conta)
    {
        var origem = new OrigemLancamento("financeiro", "conta-a-receber", conta.Id);
        PartidaContabil[] partidas =
        [
            PartidaContabil.Debito(PlanoDeContasPadrao.ContasAReceber.Id, conta.ValorTotal),
            PartidaContabil.Credito(PlanoDeContasPadrao.Receita.Id, conta.ValorTotal)
        ];
        return LancamentoContabil.Criar(conta.BusinessId, conta.DataCompetencia, $"Receita a receber — {conta.Descricao}", origem, partidas);
    }

    public static Result<LancamentoContabil> DeContaAPagar(ContaAPagar conta)
    {
        // Análise por Projeto/Imobilizado — comprar um AtivoDeCapital é troca de ativo (a conta
        // credora vira 1.3 Ativos de Capital), nunca despesa direta (4.1): a despesa só nasce com o
        // cronograma de reconhecimento mensal (ver ReconhecerAmortizacoesUseCase), espelho exato do
        // desvio já feito para CMV (compra de mercadoria não é custo — o custo nasce na venda).
        var contaDebito = conta.CategoriaId == CategoriaAtivoDeCapital
            ? PlanoDeContasPadrao.AtivosDeCapital
            : PlanoDeContasPadrao.CustoDespesa;

        var origem = new OrigemLancamento("financeiro", "conta-a-pagar", conta.Id);
        PartidaContabil[] partidas =
        [
            PartidaContabil.Debito(contaDebito.Id, conta.ValorTotal),
            PartidaContabil.Credito(PlanoDeContasPadrao.ContasAPagar.Id, conta.ValorTotal)
        ];
        return LancamentoContabil.Criar(conta.BusinessId, conta.DataCompetencia, $"Custo/despesa a pagar — {conta.Descricao}", origem, partidas);
    }

    /// <summary>
    /// Reconhecimento mensal (cron, idempotente) de depreciação/amortização de um
    /// <c>AtivoDeCapital</c> — débito 4.1 Custo/Despesa, crédito 1.3 Ativos de Capital (§4.4 dos
    /// dois designs). <paramref name="origemId"/> é <c>{ativoId}:{yyyyMM}</c> (ou <c>{ativoId}</c> na
    /// baixa antecipada, com <paramref name="origemTipo"/> <c>"amortizacao-baixa"</c>) — a mesma
    /// dupla (tipo, id) que compõe a chave de idempotência do evento de integração
    /// <c>CustoAmortizadoReconhecido</c>.
    /// </summary>
    public static Result<LancamentoContabil> DeReconhecimentoDeAtivoDeCapital(
        string businessId, DateTimeOffset competencia, string descricao, string origemTipo, string origemId, Money valor)
    {
        var origem = new OrigemLancamento("financeiro", origemTipo, origemId);
        PartidaContabil[] partidas =
        [
            PartidaContabil.Debito(PlanoDeContasPadrao.CustoDespesa.Id, valor),
            PartidaContabil.Credito(PlanoDeContasPadrao.AtivosDeCapital.Id, valor)
        ];
        return LancamentoContabil.Criar(businessId, competencia, descricao, origem, partidas);
    }

    /// <summary>
    /// Alienação (venda) de um <c>AtivoDeCapital</c> — fatia I4, docs/financeiro/design-imobilizado-roi.md
    /// §4.6: "C-1.3 pelo valor contábil, D-4.1 pela perda (ou crédito residual em 3.1 pelo ganho)".
    /// UM lançamento auto-contido (nunca dois lançamentos concorrentes para o mesmo fato de venda):
    /// débito 1.2 (o recebível, se houver preço de venda rastreado), crédito 1.3 (baixa do valor
    /// contábil restante, se houver), e a diferença fecha a partida — crédito 3.1 se ganho
    /// (<paramref name="valorVenda"/> &gt; <paramref name="valorContabil"/>), débito 4.1 se perda.
    /// Pernas de valor ZERO são omitidas (<see cref="LancamentoContabil.Criar"/> rejeita partida com
    /// valor não-positivo) — o balanceamento (Σdébito == Σcrédito) é garantido por construção em
    /// qualquer combinação de sinais; se as duas pernas principais forem zero (bem já 100%
    /// depreciado, doado sem preço) o resultado é <c>Falha</c> por partidas insuficientes — o
    /// chamador trata como "nada a lançar", nunca propaga como erro do usuário.
    /// </summary>
    public static Result<LancamentoContabil> DeVendaDeAtivoDeCapital(
        string businessId, DateTimeOffset competencia, string descricao, string ativoId, Money valorVenda, Money valorContabil)
    {
        var origem = new OrigemLancamento("financeiro", "alienacao-ativo", ativoId);
        var partidas = new List<PartidaContabil>();

        if (valorVenda.EhPositivo)
            partidas.Add(PartidaContabil.Debito(PlanoDeContasPadrao.ContasAReceber.Id, valorVenda));
        if (valorContabil.EhPositivo)
            partidas.Add(PartidaContabil.Credito(PlanoDeContasPadrao.AtivosDeCapital.Id, valorContabil));

        var resultadoCentavos = valorVenda.Centavos - valorContabil.Centavos;
        if (resultadoCentavos > 0)
            partidas.Add(PartidaContabil.Credito(PlanoDeContasPadrao.Receita.Id, new Money(resultadoCentavos)));
        else if (resultadoCentavos < 0)
            partidas.Add(PartidaContabil.Debito(PlanoDeContasPadrao.CustoDespesa.Id, new Money(-resultadoCentavos)));

        return LancamentoContabil.Criar(businessId, competencia, descricao, origem, partidas);
    }

    /// <summary>Dinheiro ENTRA (recebimento de parcela): débito Caixa/Bancos, crédito Contas a Receber.</summary>
    public static Result<LancamentoContabil> DeMovimentoEntrada(MovimentoFinanceiro movimento)
    {
        var origem = new OrigemLancamento("financeiro", "movimento-entrada", movimento.Id);
        PartidaContabil[] partidas =
        [
            PartidaContabil.Debito(PlanoDeContasPadrao.CaixaEBancos.Id, movimento.Valor),
            PartidaContabil.Credito(PlanoDeContasPadrao.ContasAReceber.Id, movimento.Valor)
        ];
        return LancamentoContabil.Criar(movimento.BusinessId, movimento.DataMovimento, "Recebimento de parcela", origem, partidas);
    }

    /// <summary>Dinheiro SAI (pagamento de parcela): débito Contas a Pagar, crédito Caixa/Bancos.</summary>
    public static Result<LancamentoContabil> DeMovimentoSaida(MovimentoFinanceiro movimento)
    {
        var origem = new OrigemLancamento("financeiro", "movimento-saida", movimento.Id);
        PartidaContabil[] partidas =
        [
            PartidaContabil.Debito(PlanoDeContasPadrao.ContasAPagar.Id, movimento.Valor),
            PartidaContabil.Credito(PlanoDeContasPadrao.CaixaEBancos.Id, movimento.Valor)
        ];
        return LancamentoContabil.Criar(movimento.BusinessId, movimento.DataMovimento, "Pagamento de parcela", origem, partidas);
    }

    /// <summary>Roteia pelo <see cref="TipoMovimentoFinanceiro"/> — o chamador não precisa saber qual método usar.</summary>
    public static Result<LancamentoContabil> DeMovimento(MovimentoFinanceiro movimento)
        => movimento.Tipo == TipoMovimentoFinanceiro.Entrada ? DeMovimentoEntrada(movimento) : DeMovimentoSaida(movimento);
}
