using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Estoque.Application.Comum;
using SistemaX.Modules.Estoque.Application.Ports;
using SistemaX.Modules.Estoque.Domain.Comum;
using SistemaX.Modules.Estoque.Domain.Razao;

namespace SistemaX.Modules.Estoque.Application.EventosDeIntegracao.Handlers;

/// <summary>
/// <c>venda.itens</c> (companion de <c>venda.concluida</c> — ver GAP DOCUMENTADO em
/// <c>Modules.Abstractions/IntegrationEvents.cs</c>) → <c>Saida</c> por item, expandindo ficha
/// técnica quando o produto é composto. Produto sem <c>ControlaEstoque</c> (serviço/taxa) é
/// ignorado silenciosamente. Idempotente por
/// <c>venda.baixa:{VendaId}:{ItemId}:{ProdutoId}</c> — chave POR LINHA (e por insumo, quando a
/// linha expande), então replay do mesmo evento não duplica nem mesmo parcialmente.
///
/// F0 do plano de inteligência do Financeiro (docs/financeiro/inteligencia-arquitetura.md/
/// ADR-0005): ao final, soma o custo de TODAS as linhas baixadas nesta venda (custo médio vigente
/// no instante da baixa × quantidade) e publica <see cref="CustoBaixadoPorVenda"/> — o CMV
/// correto que a F1 vai usar para substituir <c>CompraRecebida</c> como custo na DRE.
/// </summary>
public sealed class VendaItensMovimentadosHandler(
    IProdutoRepository produtos, IMovimentoRepository movimentos, ISaldoRepository saldos, IIntegrationEventBus bus)
    : IIntegrationEventHandler<VendaItensMovimentados>
{
    public async Task HandleAsync(VendaItensMovimentados evento, CancellationToken ct = default)
    {
        long custoTotalCentavos = 0;

        foreach (var item in evento.Itens)
        {
            var produto = await produtos.ObterPorIdAsync(item.ProdutoId, ct);
            if (produto is null) continue;

            var quantidadeVendida = new Quantidade(item.QuantidadeMilesimos);
            var itemId = item.ItemId ?? item.ProdutoId;

            if (produto.FichaTecnica.Count > 0)
            {
                var expansao = await ExpansorDeFichaTecnica.ExpandirAsync(produtos, produto.Id, quantidadeVendida, ct);
                if (expansao.Falha)
                    throw new InvalidOperationException($"Falha ao expandir ficha técnica do produto {produto.Id} na venda {evento.VendaId}: {expansao.Erro.Mensagem}");

                foreach (var (produtoInsumoId, quantidadeInsumo) in expansao.Valor)
                    custoTotalCentavos += await BaixarUmItemAsync(evento, produtoInsumoId, itemId, quantidadeInsumo, ct);
            }
            else if (produto.ControlaEstoque)
            {
                custoTotalCentavos += await BaixarUmItemAsync(evento, produto.Id, itemId, quantidadeVendida, ct);
            }
            // else: serviço/taxa sem ficha técnica — não gera movimento.
        }

        // Só publica quando ALGUMA baixa nova de fato aconteceu nesta chamada — num replay do
        // mesmo evento, todo BaixarUmItemAsync já idempotente retorna 0 (ver comentário abaixo),
        // então o total fica zero e não republicamos um CustoBaixadoPorVenda redundante.
        if (custoTotalCentavos > 0)
        {
            await bus.PublishAsync(new CustoBaixadoPorVenda(evento.VendaId, evento.TenantId, custoTotalCentavos, evento.OcorridoEm), ct);
        }
    }

    /// <summary>Baixa uma linha e retorna o custo desta baixa em centavos (custo médio vigente ×
    /// quantidade) — 0 se a linha já tinha sido processada antes (idempotência por
    /// <paramref name="itemId"/>+<paramref name="produtoId"/>) ou se o produto não controla
    /// estoque.</summary>
    private async Task<long> BaixarUmItemAsync(VendaItensMovimentados evento, string produtoId, string itemId, Quantidade quantidade, CancellationToken ct)
    {
        var chave = $"venda.baixa:{evento.VendaId}:{itemId}:{produtoId}";
        if (await movimentos.ExisteComChaveAsync(chave, ct)) return 0;

        var produto = await produtos.ObterPorIdAsync(produtoId, ct);
        if (produto is null || !produto.ControlaEstoque) return 0;

        var saldo = await saldos.ObterOuCriarAsync(evento.TenantId, produtoId, EstoqueConstantes.DepositoPadrao, ct);
        var disponivelAntes = saldo.Disponivel;
        var custoUnitarioNoInstanteDaBaixa = saldo.CustoMedio;

        var movimentoResultado = MovimentoDeEstoque.Registrar(
            evento.TenantId, EstoqueConstantes.DepositoPadrao, produtoId, TipoMovimento.Saida, quantidade,
            custoUnitarioNoInstanteDaBaixa, new SourceRef("venda", evento.VendaId), chave, $"Venda {evento.VendaId}",
            EstoqueConstantes.OperadorSistema, EstoqueConstantes.OperadorSistemaNome, evento.OcorridoEm);

        if (movimentoResultado.Falha)
            throw new InvalidOperationException($"Falha ao registrar saída da venda {evento.VendaId}: {movimentoResultado.Erro.Mensagem}");

        var movimento = movimentoResultado.Valor;
        saldo.AplicarMovimento(movimento);

        await movimentos.SalvarAsync(movimento, ct);
        await saldos.SalvarAsync(saldo, ct);

        await AlertaDeEstoqueMinimo.AvaliarEPublicarAsync(bus, produto, disponivelAntes, saldo, movimento.Id, evento.TenantId, evento.OcorridoEm, ct);

        // custoUnitário é por UNIDADE inteira (Money); quantidade é em milésimos — mesma
        // convenção de escala de CalculadoraDeCustoMedio. Arredondamento bancário, mesmo critério
        // de Money/Quantidade em todo o resto do sistema.
        return (long)Math.Round(custoUnitarioNoInstanteDaBaixa.Centavos * quantidade.Milesimos / 1000m, MidpointRounding.ToEven);
    }
}
