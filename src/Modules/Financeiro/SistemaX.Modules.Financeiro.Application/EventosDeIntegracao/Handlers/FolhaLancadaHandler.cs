using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Financeiro.Application.Categorias;
using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Domain.Comum;
using SistemaX.Modules.Financeiro.Domain.Contabil;
using SistemaX.Modules.Financeiro.Domain.ContasAPagarReceber;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Application.EventosDeIntegracao.Handlers;

/// <summary>
/// <c>folha.lancada</c> → cria <c>ContaAPagar</c> categoria "Despesa com Pessoal",
/// <c>dataCompetencia</c> = mês de referência. Folha é paga depois de lançada — nenhum
/// <c>MovimentoFinanceiro</c> nasce aqui (docs/financeiro-datamodel.md §4.2). Idempotente por
/// <c>SourceRef("payroll", LancamentoId)</c> — a spec recomenda chave composta
/// <c>{periodo}_{employeeId}</c>; como <see cref="FolhaLancada"/> já expõe um <c>LancamentoId</c>
/// próprio (presumidamente estável por período+funcionário do lado de origem), usamos-o direto.
///
/// CORRENTE (P0-1) deliberadamente NÃO marcada: folha é despesa operacional geral (a empresa toda
/// tem folha, não uma corrente específica) — fica <c>null</c>/não-classificada nesta dimensão, o
/// mesmo tratamento de qualquer despesa que não é custo direto de uma corrente.
/// </summary>
public sealed class FolhaLancadaHandler(
    IContaAPagarRepository contasAPagar, ILancamentoContabilRepository lancamentos) : IIntegrationEventHandler<FolhaLancada>
{
    public async Task HandleAsync(FolhaLancada evento, CancellationToken ct = default)
    {
        var origem = new SourceRef("payroll", evento.LancamentoId);
        if (await contasAPagar.BuscarPorOrigemAsync(evento.TenantId, origem.Chave, ct) is not null)
            return;

        var valor = new Money(evento.TotalCentavos);
        var parcelas = ContaFinanceiraBase.ParcelaUnica(valor, evento.OcorridoEm);

        var contaResultado = ContaAPagar.Criar(
            evento.TenantId, origem, $"Folha {evento.Competencia}", CategoriaFinanceiraPadrao.DespesaComPessoal, evento.OcorridoEm, valor, parcelas);
        if (contaResultado.Falha)
            throw new InvalidOperationException($"Falha ao criar ContaAPagar para folha {evento.LancamentoId}: {contaResultado.Erro.Mensagem}");

        await contasAPagar.SalvarAsync(contaResultado.Valor, ct);

        var lancamentoResultado = LancamentoContabilFactory.DeContaAPagar(contaResultado.Valor);
        if (lancamentoResultado.Falha)
            throw new InvalidOperationException($"Falha ao gerar lançamento contábil da folha {evento.LancamentoId}: {lancamentoResultado.Erro.Mensagem}");

        await lancamentos.SalvarAsync(lancamentoResultado.Valor, ct);
    }
}
