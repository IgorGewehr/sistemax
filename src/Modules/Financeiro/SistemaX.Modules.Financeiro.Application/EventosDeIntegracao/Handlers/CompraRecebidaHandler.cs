using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Financeiro.Application.Caixa;
using SistemaX.Modules.Financeiro.Application.Categorias;
using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Domain.Comum;
using SistemaX.Modules.Financeiro.Domain.Contabil;
using SistemaX.Modules.Financeiro.Domain.ContasAPagarReceber;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Application.EventosDeIntegracao.Handlers;

/// <summary>
/// <c>compra.recebida</c> (NF-e de fornecedor) → cria <c>ContaAPagar</c> com
/// <c>dataCompetencia</c> = emissão da NF, categoria CMV/Custo. Compra a prazo é o caso comum —
/// nenhum <c>MovimentoFinanceiro</c> nasce aqui, só na liquidação (docs/financeiro-datamodel.md
/// §4.2). Idempotente por <c>SourceRef("purchaseNote", CompraId)</c>.
///
/// CORRENTE (P0-1) deliberadamente NÃO marcada aqui: no momento da compra ainda não se sabe se o
/// estoque recém-chegado vai ser revendido no balcão (<see cref="CorrenteDeReceita.Comercio"/>) ou
/// consumido numa OS (<see cref="CorrenteDeReceita.Servico"/>, quando P0-5/<c>CustoBaixadoPorOs</c>
/// existir) — a corrente só é conhecida no CONSUMO, não na entrada. Além disso esta ContaAPagar já
/// não entra no custo direto do DRE de qualquer forma (ver <c>DreGerencialService</c> — compra é
/// balanço, não resultado); marcar errado aqui não teria como ser corrigido sem reprocessar.
/// </summary>
public sealed class CompraRecebidaHandler(
    IContaAPagarRepository contasAPagar, ILancamentoContabilRepository lancamentos) : IIntegrationEventHandler<CompraRecebida>
{
    public async Task HandleAsync(CompraRecebida evento, CancellationToken ct = default)
    {
        var origem = new SourceRef("purchaseNote", evento.CompraId);
        if (await contasAPagar.BuscarPorOrigemAsync(evento.TenantId, origem.Chave, ct) is not null)
            return;

        var valor = new Money(evento.TotalCentavos);
        var vencimento = evento.OcorridoEm.AddDays(ClassificadorFormaPagamento.PrazoPadraoDiasAPrazo);
        var parcelas = ContaFinanceiraBase.ParcelaUnica(valor, vencimento);

        var contaResultado = ContaAPagar.Criar(
            evento.TenantId, origem, $"Compra {evento.CompraId} — fornecedor {evento.FornecedorId}",
            CategoriaFinanceiraPadrao.CustoMercadoriaVendida, evento.OcorridoEm, valor, parcelas,
            fornecedorId: evento.FornecedorId);
        if (contaResultado.Falha)
            throw new InvalidOperationException($"Falha ao criar ContaAPagar para compra {evento.CompraId}: {contaResultado.Erro.Mensagem}");

        await contasAPagar.SalvarAsync(contaResultado.Valor, ct);

        var lancamentoResultado = LancamentoContabilFactory.DeContaAPagar(contaResultado.Valor);
        if (lancamentoResultado.Falha)
            throw new InvalidOperationException($"Falha ao gerar lançamento contábil da compra {evento.CompraId}: {lancamentoResultado.Erro.Mensagem}");

        await lancamentos.SalvarAsync(lancamentoResultado.Valor, ct);
    }
}
