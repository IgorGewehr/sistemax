using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Financeiro.Application.Caixa;
using SistemaX.Modules.Financeiro.Application.Categorias;
using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Domain.Caixa;
using SistemaX.Modules.Financeiro.Domain.Comum;
using SistemaX.Modules.Financeiro.Domain.Contabil;
using SistemaX.Modules.Financeiro.Domain.ContasAPagarReceber;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Application.EventosDeIntegracao.Handlers;

/// <summary>
/// <c>os.faturada</c> → cria <c>ContaAReceber</c> (serviço + peças) na corrente Servico e, se a OS
/// carregar forma de pagamento (P1-7, docs/financeiro/revisao-domain-fit-cnpj.md), LIQUIDA na hora
/// — mesmo espelho de <c>VendaConcluidaHandler</c>. Idempotente por
/// <c>SourceRef("appointment", OrdemServicoId)</c>.
///
/// POR QUE LIQUIDAR SEMPRE QUE HÁ FORMA DE PAGAMENTO: a assistência não tem "a prazo" na entrega —
/// <c>OrdemDeServico.Entregar</c> EXIGE forma de pagamento (o cliente paga na retirada, seja
/// dinheiro/pix/débito/crédito). Sem liquidar, cada OS entregue nasceria como recebível "Aberto" e
/// o cron de vencidas a marcaria "Atrasado" no dia seguinte — um inadimplente FANTASMA que nunca
/// existiu. O único caso legítimo de <see cref="OsFaturada.FormaPagamento"/> nulo é a taxa de
/// diagnóstico de <c>OrdemDeServico.DevolverSemReparo</c> — essa sim fica Aberta de verdade.
///
/// <see cref="OsFaturada.TecnicoId"/> é persistido em <c>ContaAReceber.TecnicoId</c> — já dá pra
/// consultar "qual técnico faturou esta OS" a partir do Financeiro. GAP DOCUMENTADO (comissão): a
/// criação automática de <c>ContaAPagar</c> categoria Comissões continua pendente porque não existe
/// cadastro de percentual por tenant (R6: não inventar percentual que o tipo/cadastro não expressa);
/// quando esse cadastro existir, este handler ganha a criação condicional.
/// </summary>
public sealed class OsFaturadaHandler(
    IContaAReceberRepository contasAReceber,
    IMovimentoFinanceiroRepository movimentos,
    ILancamentoContabilRepository lancamentos) : IIntegrationEventHandler<OsFaturada>
{
    public async Task HandleAsync(OsFaturada evento, CancellationToken ct = default)
    {
        var origem = new SourceRef("appointment", evento.OrdemServicoId);
        if (await contasAReceber.BuscarPorOrigemAsync(evento.TenantId, origem.Chave, ct) is not null)
            return;

        var valorTotal = new Money(evento.ValorServicoCentavos + evento.ValorPecasCentavos);
        var parcelas = ContaFinanceiraBase.ParcelaUnica(valorTotal, evento.OcorridoEm);
        var descricao = evento.NumeroOs is not null ? $"OS {evento.NumeroOs}" : $"OS {evento.OrdemServicoId}";

        // Corrente: OS é sempre a corrente Servico (P0-1). A cobrança/parcela continua sobre o
        // TOTAL (fato_receita_diaria soma os dois na mesma corrente, e o CMV de peça entra separado
        // via CustoBaixadoPorOs — ver FatoCustoDiarioProjection); a repartição mão de obra vs peças
        // fica gravada em ValorServico/ValorPecas só para granularidade de relatório (P1-7).
        var contaResultado = ContaAReceber.Criar(
            evento.TenantId, origem, descricao, CategoriaFinanceiraPadrao.Servicos, evento.OcorridoEm, valorTotal, parcelas,
            clienteId: evento.ClienteId, corrente: CorrenteDeReceita.Servico, tecnicoId: evento.TecnicoId,
            valorServico: new Money(evento.ValorServicoCentavos), valorPecas: new Money(evento.ValorPecasCentavos));
        if (contaResultado.Falha)
            throw new InvalidOperationException($"Falha ao criar ContaAReceber para OS {evento.OrdemServicoId}: {contaResultado.Erro.Mensagem}");

        var conta = contaResultado.Valor;
        var parcela = conta.Parcelas[0];
        var formaPagamento = evento.FormaPagamento;

        if (formaPagamento is not null)
        {
            var liquidacao = conta.RegistrarLiquidacaoParcela(parcela.Id, valorTotal, evento.OcorridoEm, formaPagamento);
            if (liquidacao.Falha)
                throw new InvalidOperationException($"Falha ao liquidar parcela da OS {evento.OrdemServicoId}: {liquidacao.Erro.Mensagem}");
        }

        await contasAReceber.SalvarAsync(conta, ct);

        var lancamentoCompetencia = LancamentoContabilFactory.DeContaAReceber(conta);
        if (lancamentoCompetencia.Falha)
            throw new InvalidOperationException($"Falha ao gerar lançamento contábil da OS {evento.OrdemServicoId}: {lancamentoCompetencia.Erro.Mensagem}");
        await lancamentos.SalvarAsync(lancamentoCompetencia.Valor, ct);

        if (formaPagamento is null) return; // taxa de diagnóstico a prazo — nenhum dinheiro mudou de mão ainda

        var movimentoResultado = MovimentoFinanceiro.Registrar(
            evento.TenantId, ClassificadorFormaPagamento.ContaCaixaPadraoId, formaPagamento, parcela.Id,
            conta.Id, TipoMovimentoFinanceiro.Entrada, valorTotal, evento.OcorridoEm, new SourceRef("os-payment", evento.OrdemServicoId),
            corrente: CorrenteDeReceita.Servico);
        if (movimentoResultado.Falha)
            throw new InvalidOperationException($"Falha ao registrar movimento de caixa da OS {evento.OrdemServicoId}: {movimentoResultado.Erro.Mensagem}");
        await movimentos.SalvarAsync(movimentoResultado.Valor, ct);

        var lancamentoCaixa = LancamentoContabilFactory.DeMovimento(movimentoResultado.Valor);
        if (lancamentoCaixa.Falha)
            throw new InvalidOperationException($"Falha ao gerar lançamento contábil de caixa da OS {evento.OrdemServicoId}: {lancamentoCaixa.Erro.Mensagem}");
        await lancamentos.SalvarAsync(lancamentoCaixa.Valor, ct);
    }
}
