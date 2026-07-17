using SistemaX.Modules.Estoque.Application.Comum;
using SistemaX.Modules.Estoque.Application.Ports;
using SistemaX.Modules.Estoque.Domain.Comum;
using SistemaX.Modules.Estoque.Domain.Razao;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Estoque.Application.CasosDeUso;

/// <summary>Entrada avulsa fora de evento de integração (doação, ajuste de implantação de
/// estoque inicial). Chave de idempotência é o próprio ULID do movimento — gesto manual, não
/// replay de evento externo.</summary>
public sealed class RegistrarEntradaManualUseCase(IProdutoRepository produtos, IMovimentoRepository movimentos, ISaldoRepository saldos)
{
    public async Task<Result<MovimentoDeEstoque>> ExecutarAsync(
        string tenantId, string produtoId, Quantidade quantidade, Money custoUnitario, string motivo,
        string operadorId, string operadorNome, DateTimeOffset ocorridoEm, CancellationToken ct = default)
    {
        var produto = await produtos.ObterPorIdAsync(produtoId, ct);
        if (produto is null)
            return Result.Falhar<MovimentoDeEstoque>(new Error("estoque.produto.nao_encontrado", $"Produto '{produtoId}' não encontrado."));

        if (!produto.ControlaEstoque)
            return Result.Falhar<MovimentoDeEstoque>(new Error("estoque.produto.nao_controla_estoque", "Produto não controla estoque — entrada manual não se aplica."));

        var movimentoId = IdGenerator.NovoId();
        var saldo = await saldos.ObterOuCriarAsync(tenantId, produtoId, EstoqueConstantes.DepositoPadrao, ct);

        var movimentoResultado = MovimentoDeEstoque.Registrar(
            tenantId, EstoqueConstantes.DepositoPadrao, produtoId, TipoMovimento.Entrada, quantidade, custoUnitario,
            new SourceRef("manual", movimentoId), $"manual:{movimentoId}", motivo, operadorId, operadorNome, ocorridoEm);

        if (movimentoResultado.Falha) return movimentoResultado;

        saldo.AplicarMovimento(movimentoResultado.Valor);
        await movimentos.SalvarAsync(movimentoResultado.Valor, ct);
        await saldos.SalvarAsync(saldo, ct);

        return movimentoResultado;
    }
}
