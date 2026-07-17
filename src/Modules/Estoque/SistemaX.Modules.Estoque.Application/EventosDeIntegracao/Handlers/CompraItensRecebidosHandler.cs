using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Estoque.Application.Comum;
using SistemaX.Modules.Estoque.Application.Ports;
using SistemaX.Modules.Estoque.Domain.Comum;
using SistemaX.Modules.Estoque.Domain.Razao;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Estoque.Application.EventosDeIntegracao.Handlers;

/// <summary>
/// <c>compra.itens</c> (companion de <c>compra.recebida</c>) → <c>Entrada</c> por item com o custo
/// da nota, recalculando custo médio. Idempotente por
/// <c>compra.entrada:{CompraId}:{ItemId}</c>. Cria lote quando <c>LoteNumero</c> vem preenchido
/// (o campo já existe no razão desde V1; consumo por FEFO é V5).
/// </summary>
public sealed class CompraItensRecebidosHandler(IProdutoRepository produtos, IMovimentoRepository movimentos, ISaldoRepository saldos)
    : IIntegrationEventHandler<CompraItensRecebidos>
{
    public async Task HandleAsync(CompraItensRecebidos evento, CancellationToken ct = default)
    {
        foreach (var item in evento.Itens)
        {
            var produto = await produtos.ObterPorIdAsync(item.ProdutoId, ct);
            if (produto is null || !produto.ControlaEstoque) continue;

            var itemId = item.ItemId ?? item.ProdutoId;
            var chave = $"compra.entrada:{evento.CompraId}:{itemId}";
            if (await movimentos.ExisteComChaveAsync(chave, ct)) continue;

            var saldo = await saldos.ObterOuCriarAsync(evento.TenantId, produto.Id, EstoqueConstantes.DepositoPadrao, ct);
            var custoUnitario = new Money(item.PrecoUnitarioCentavos);

            var movimentoResultado = MovimentoDeEstoque.Registrar(
                evento.TenantId, EstoqueConstantes.DepositoPadrao, produto.Id, TipoMovimento.Entrada,
                new Quantidade(item.QuantidadeMilesimos), custoUnitario, new SourceRef("purchaseNote", evento.CompraId),
                chave, $"Compra {evento.CompraId} — {item.Descricao}", EstoqueConstantes.OperadorSistema,
                EstoqueConstantes.OperadorSistemaNome, evento.OcorridoEm, loteId: item.LoteNumero);

            if (movimentoResultado.Falha)
                throw new InvalidOperationException($"Falha ao registrar entrada da compra {evento.CompraId}: {movimentoResultado.Erro.Mensagem}");

            var movimento = movimentoResultado.Valor;
            saldo.AplicarMovimento(movimento);

            await movimentos.SalvarAsync(movimento, ct);
            await saldos.SalvarAsync(saldo, ct);
        }
    }
}
