using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Domain.Caixa;
using SistemaX.Modules.Financeiro.Domain.Comum;
using SistemaX.Modules.Financeiro.Domain.Contabil;
using SistemaX.Modules.Financeiro.Domain.ContasAPagarReceber;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Application.CasosDeUso;

public sealed record BaixarParcelaComando(
    string ContaId,
    string ParcelaId,
    Money ValorPago,
    DateTimeOffset DataPagamento,
    string ContaBancariaCaixaId,
    string FormaPagamentoId,
    string IdempotencyKey);

/// <summary>
/// "Pagar conta"/"baixar parcela" — o caso de uso operacional mais comum do módulo. Orquestra,
/// numa única unidade lógica, as DUAS escritas que o modelo híbrido exige: (1) a liquidação de
/// competência na <c>Parcela</c> dentro do agregado <c>ContaAPagar</c>/<c>ContaAReceber</c>, e
/// (2) o fato de caixa <c>MovimentoFinanceiro</c> correspondente + seu <c>LancamentoContabil</c>
/// gerado automaticamente. Idempotente pela chave composta em <see cref="BaixarParcelaComando.IdempotencyKey"/>.
///
/// P1-3 (docs/financeiro/revisao-domain-fit-cnpj.md) — publica <see cref="ParcelaBaixada"/> pós-commit
/// para <c>FatoCaixaDiarioProjection</c> foldar o caixa REALIZADO dos dois lados (entradas E saídas
/// de liquidação, não só venda à vista). P1-6: para ENTRADA (ContaAReceber) o valor de caixa é o
/// LÍQUIDO de MDR quando a forma de pagamento tem taxa — resolvido via
/// <see cref="Domain.Caixa.FormaDePagamento.CalcularValorLiquido"/>, o mesmo lar único que
/// <c>FatoRecebiveisProjection</c> usa (nunca uma segunda fonte/fórmula de taxa). Esse valor é
/// calculado UMA vez e usado tanto para o <see cref="MovimentoFinanceiro"/> registrado (fonte de
/// <c>IMovimentoFinanceiroRepository.CalcularSaldoAsync</c>, o "saldo atual" que
/// <c>PrevisaoDeCaixaService</c>/<c>QuantoSobrouDeVerdadeService</c>/<c>ContasBancariasService</c>
/// leem) quanto para o evento publicado (fonte de <c>fato_caixa_diario</c>) — as DUAS leituras de
/// caixa nunca podem divergir de unidade (bug fechado nesta revisão: saldo atual em bruto somado a
/// ruído histórico em líquido eram grandezas diferentes).
/// </summary>
public sealed class BaixarParcelaUseCase(
    IContaAReceberRepository contasAReceber,
    IContaAPagarRepository contasAPagar,
    IMovimentoFinanceiroRepository movimentos,
    ILancamentoContabilRepository lancamentos,
    IFormaDePagamentoRepository formasDePagamento,
    IIntegrationEventBus barramentoDeEventos)
{
    public async Task<Result<MovimentoFinanceiro>> BaixarParcelaDeContaAReceberAsync(BaixarParcelaComando comando, CancellationToken ct = default)
    {
        var conta = await contasAReceber.ObterPorIdAsync(comando.ContaId, ct);
        if (conta is null) return Result.Falhar<MovimentoFinanceiro>(ContaNaoEncontrada(comando.ContaId));

        var movimentoResultado = await ProcessarLiquidacaoAsync(conta, TipoMovimentoFinanceiro.Entrada, comando, ct);
        if (movimentoResultado.Falha) return movimentoResultado;

        await contasAReceber.SalvarAsync(conta, ct);
        return movimentoResultado;
    }

    public async Task<Result<MovimentoFinanceiro>> BaixarParcelaDeContaAPagarAsync(BaixarParcelaComando comando, CancellationToken ct = default)
    {
        var conta = await contasAPagar.ObterPorIdAsync(comando.ContaId, ct);
        if (conta is null) return Result.Falhar<MovimentoFinanceiro>(ContaNaoEncontrada(comando.ContaId));

        var movimentoResultado = await ProcessarLiquidacaoAsync(conta, TipoMovimentoFinanceiro.Saida, comando, ct);
        if (movimentoResultado.Falha) return movimentoResultado;

        await contasAPagar.SalvarAsync(conta, ct);
        return movimentoResultado;
    }

    private async Task<Result<MovimentoFinanceiro>> ProcessarLiquidacaoAsync(
        ContaFinanceiraBase conta, TipoMovimentoFinanceiro tipo, BaixarParcelaComando comando, CancellationToken ct)
    {
        var origemMovimento = new SourceRef("financeiro-baixa", comando.IdempotencyKey);
        var existente = await movimentos.BuscarPorOrigemAsync(conta.BusinessId, origemMovimento.Chave, ct);
        if (existente is not null) return Result.Ok(existente); // idempotência: mesma baixa reenviada não duplica

        var liquidacao = conta.RegistrarLiquidacaoParcela(comando.ParcelaId, comando.ValorPago, comando.DataPagamento, comando.FormaPagamentoId);
        if (liquidacao.Falha) return Result.Falhar<MovimentoFinanceiro>(liquidacao.Erro);

        // A parcela em si liquida o BRUTO acima (o cliente pagou o ticket cheio — MDR não é
        // inadimplência do cliente, é custo do lojista com a adquirente). O CAIXA que de fato move
        // (MovimentoFinanceiro E o evento ParcelaBaixada) é o valor calculado abaixo, UMA vez.
        var valorCaixa = await ResolverValorCaixaAsync(conta.BusinessId, tipo, comando, ct).ConfigureAwait(false);

        // A conta tageada carrega TANTO a corrente quanto o projeto — o movimento de caixa
        // resultante da baixa herda os dois (docs/financeiro/design-analise-por-projeto.md §3.2,
        // tabela §10: "BaixarParcelaUseCase propaga conta.ProjetoId e conta.Corrente"). Sem tag na
        // conta, os dois nascem null aqui, como sempre.
        var movimentoResultado = MovimentoFinanceiro.Registrar(
            conta.BusinessId, comando.ContaBancariaCaixaId, comando.FormaPagamentoId, comando.ParcelaId,
            conta.Id, tipo, valorCaixa, comando.DataPagamento, origemMovimento, conta.Corrente, conta.ProjetoId);
        if (movimentoResultado.Falha) return movimentoResultado;

        await movimentos.SalvarAsync(movimentoResultado.Valor, ct);

        var lancamentoResultado = LancamentoContabilFactory.DeMovimento(movimentoResultado.Valor);
        if (lancamentoResultado.Falha) return Result.Falhar<MovimentoFinanceiro>(lancamentoResultado.Erro);

        await lancamentos.SalvarAsync(lancamentoResultado.Valor, ct);

        await barramentoDeEventos.PublishAsync(
            new ParcelaBaixada(conta.Id, comando.ParcelaId, conta.BusinessId, tipo == TipoMovimentoFinanceiro.Saida, valorCaixa.Centavos, comando.DataPagamento), ct)
            .ConfigureAwait(false);

        return movimentoResultado;
    }

    /// <summary>
    /// Insumo do caixa bilateral (P1-3) — MESMO valor para <c>MovimentoFinanceiro</c> e
    /// <c>ParcelaBaixada</c> (ver doc da classe). SAÍDA (ContaAPagar) não tem MDR — o valor de caixa
    /// é o pago integral. ENTRADA (ContaAReceber) resolve a forma de pagamento informada na baixa
    /// contra o LAR ÚNICO (<see cref="IFormaDePagamentoRepository"/>) e usa o LÍQUIDO — forma não
    /// encontrada cai no mesmo fallback conservador de sempre (0% de taxa, caixa = bruto), nunca
    /// inventa desconto que o tenant não configurou.
    /// </summary>
    private async Task<Money> ResolverValorCaixaAsync(string businessId, TipoMovimentoFinanceiro tipo, BaixarParcelaComando comando, CancellationToken ct)
    {
        if (tipo == TipoMovimentoFinanceiro.Saida) return comando.ValorPago;

        var forma = await formasDePagamento.ObterPorNomeAsync(businessId, comando.FormaPagamentoId, ct).ConfigureAwait(false);
        return forma?.CalcularValorLiquido(comando.ValorPago) ?? comando.ValorPago;
    }

    private static Error ContaNaoEncontrada(string contaId) => new("financeiro.conta.nao_encontrada", $"Conta '{contaId}' não encontrada.");
}
