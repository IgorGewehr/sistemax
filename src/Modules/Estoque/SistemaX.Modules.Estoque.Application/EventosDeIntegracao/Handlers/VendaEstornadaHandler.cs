using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Estoque.Application.Comum;
using SistemaX.Modules.Estoque.Application.Ports;
using SistemaX.Modules.Estoque.Domain.Comum;
using SistemaX.Modules.Estoque.Domain.Razao;

namespace SistemaX.Modules.Estoque.Application.EventosDeIntegracao.Handlers;

/// <summary>
/// <c>venda.estornada</c> (evento EXISTENTE, sem mudança de assinatura) → busca no razão os
/// movimentos de <c>Saida</c> com <c>Origem = ("venda", VendaId)</c> e gera uma <c>Entrada</c>
/// espelho de CADA UM — o Estoque estorna exatamente o que saiu, mesmo que a ficha técnica do
/// produto tenha mudado depois da venda (o razão não confia na ficha ATUAL, confia no que já
/// aconteceu). Não precisa dos itens da venda — o próprio razão já sabe o que baixou.
/// </summary>
public sealed class VendaEstornadaHandler(IMovimentoRepository movimentos, ISaldoRepository saldos)
    : IIntegrationEventHandler<VendaEstornada>
{
    public async Task HandleAsync(VendaEstornada evento, CancellationToken ct = default)
    {
        var origem = new SourceRef("venda", evento.VendaId);
        var movimentosOriginais = await movimentos.ListarPorOrigemAsync(evento.TenantId, origem.Chave, ct);

        foreach (var original in movimentosOriginais.Where(m => m.Tipo == TipoMovimento.Saida))
        {
            var chave = $"venda.estorno:{evento.VendaId}:{original.Id}";
            if (await movimentos.ExisteComChaveAsync(chave, ct)) continue;

            var saldo = await saldos.ObterOuCriarAsync(evento.TenantId, original.ProdutoId, original.DepositoId, ct);

            var estornoResultado = MovimentoDeEstoque.Registrar(
                evento.TenantId, original.DepositoId, original.ProdutoId, TipoMovimento.Entrada, original.Quantidade,
                original.CustoUnitario, new SourceRef("venda-estorno", evento.VendaId), chave,
                $"Estorno da venda {evento.VendaId}", EstoqueConstantes.OperadorSistema, EstoqueConstantes.OperadorSistemaNome,
                evento.OcorridoEm);

            if (estornoResultado.Falha)
                throw new InvalidOperationException($"Falha ao estornar movimento {original.Id} da venda {evento.VendaId}: {estornoResultado.Erro.Mensagem}");

            var estorno = estornoResultado.Valor;
            saldo.AplicarMovimento(estorno);

            await movimentos.SalvarAsync(estorno, ct);
            await saldos.SalvarAsync(saldo, ct);
        }
    }
}
