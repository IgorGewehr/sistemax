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
/// </summary>
public sealed class BaixarParcelaUseCase(
    IContaAReceberRepository contasAReceber,
    IContaAPagarRepository contasAPagar,
    IMovimentoFinanceiroRepository movimentos,
    ILancamentoContabilRepository lancamentos)
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

        var movimentoResultado = MovimentoFinanceiro.Registrar(
            conta.BusinessId, comando.ContaBancariaCaixaId, comando.FormaPagamentoId, comando.ParcelaId,
            conta.Id, tipo, comando.ValorPago, comando.DataPagamento, origemMovimento);
        if (movimentoResultado.Falha) return movimentoResultado;

        await movimentos.SalvarAsync(movimentoResultado.Valor, ct);

        var lancamentoResultado = LancamentoContabilFactory.DeMovimento(movimentoResultado.Valor);
        if (lancamentoResultado.Falha) return Result.Falhar<MovimentoFinanceiro>(lancamentoResultado.Erro);

        await lancamentos.SalvarAsync(lancamentoResultado.Valor, ct);
        return movimentoResultado;
    }

    private static Error ContaNaoEncontrada(string contaId) => new("financeiro.conta.nao_encontrada", $"Conta '{contaId}' não encontrada.");
}
