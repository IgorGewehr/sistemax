using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Estoque.Application.Comum;
using SistemaX.Modules.Estoque.Application.Ports;
using SistemaX.Modules.Estoque.Domain.Comum;
using SistemaX.Modules.Estoque.Domain.Razao;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Estoque.Application.EventosDeIntegracao.Handlers;

/// <summary>
/// <c>compra.estornada</c> — fecha o GAP DOCUMENTADO em
/// <c>SistemaX.Modules.Abstractions.IntegrationEvents.CompraEstornada</c>: reverte exatamente o
/// que <see cref="CompraItensRecebidosHandler"/> creditou, item a item, gerando uma <c>Saida</c>
/// espelho de cada <c>Entrada</c> — mesmo racional de <c>VendaEstornadaHandler</c> (espelha o razão
/// já registrado, nunca edita/apaga o movimento original). Usa os itens do PRÓPRIO evento (que
/// carrega os mesmos <see cref="ItemMovimentado"/> de <c>CompraItensRecebidos</c>) em vez de
/// consultar o razão por origem — evita reprocessar produtos que nunca controlaram estoque (o
/// handler de entrada já pulava esses, então nunca existiu movimento pra eles).
/// </summary>
public sealed class CompraEstornadaHandler(IProdutoRepository produtos, IMovimentoRepository movimentos, ISaldoRepository saldos)
    : IIntegrationEventHandler<CompraEstornada>
{
    public async Task HandleAsync(CompraEstornada evento, CancellationToken ct = default)
    {
        foreach (var item in evento.Itens)
        {
            var produto = await produtos.ObterPorIdAsync(item.ProdutoId, ct);
            if (produto is null || !produto.ControlaEstoque) continue; // nunca creditado — nada a reverter

            var itemId = item.ItemId ?? item.ProdutoId;
            var chave = $"compra.estorno:{evento.CompraId}:{itemId}";
            if (await movimentos.ExisteComChaveAsync(chave, ct)) continue; // replay do mesmo evento — idempotência

            var saldo = await saldos.ObterOuCriarAsync(evento.TenantId, produto.Id, EstoqueConstantes.DepositoPadrao, ct);
            var custoUnitario = new Money(item.PrecoUnitarioCentavos);

            var estornoResultado = MovimentoDeEstoque.Registrar(
                evento.TenantId, EstoqueConstantes.DepositoPadrao, produto.Id, TipoMovimento.Saida,
                new Quantidade(item.QuantidadeMilesimos), custoUnitario, new SourceRef("purchase-reversal", evento.CompraId),
                chave, $"Estorno da compra {evento.CompraId} — {item.Descricao}", EstoqueConstantes.OperadorSistema,
                EstoqueConstantes.OperadorSistemaNome, evento.OcorridoEm);

            if (estornoResultado.Falha)
                throw new InvalidOperationException($"Falha ao estornar entrada da compra {evento.CompraId}: {estornoResultado.Erro.Mensagem}");

            var estorno = estornoResultado.Valor;
            saldo.AplicarMovimento(estorno);

            await movimentos.SalvarAsync(estorno, ct);
            await saldos.SalvarAsync(saldo, ct);
        }
    }
}
