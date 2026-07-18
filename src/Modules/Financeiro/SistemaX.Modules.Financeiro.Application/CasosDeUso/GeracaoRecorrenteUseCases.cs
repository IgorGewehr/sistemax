using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Financeiro.Application.Categorias;
using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Domain.Comum;
using SistemaX.Modules.Financeiro.Domain.Contabil;
using SistemaX.Modules.Financeiro.Domain.ContasAPagarReceber;
using SistemaX.Modules.Financeiro.Domain.Recorrencia;

namespace SistemaX.Modules.Financeiro.Application.CasosDeUso;

/// <summary>
/// MOTOR DE RECORRÊNCIA — materializa contas (a pagar/receber) futuras a partir dos templates
/// <c>Recorrencia</c>, até a data de referência (catch-up de ocorrências atrasadas). Idempotente
/// por <c>SourceRef</c> determinística <c>recorrencia:{id}:{yyyyMMdd}</c> — rodar 2× (cron diário,
/// boot da loja) não duplica. Cada conta gera também seu lançamento de partida dobrada.
/// </summary>
public sealed class GerarContasRecorrentesUseCase(
    IRecorrenciaRepository recorrencias,
    IContaAPagarRepository contasAPagar,
    IContaAReceberRepository contasAReceber,
    ILancamentoContabilRepository lancamentos)
{
    public async Task<int> ExecutarAsync(string businessId, DateTimeOffset ate, CancellationToken ct = default)
    {
        var geradas = 0;
        foreach (var rec in await recorrencias.ListarAtivasAsync(businessId, ct))
        {
            var mudou = false;
            while (true)
            {
                var proxima = rec.CalcularProximaOcorrencia();
                if (proxima.Falha || proxima.Valor > ate) break;

                var vencimento = proxima.Valor;
                var origem = new SourceRef("recorrencia", $"{rec.Id}:{vencimento:yyyyMMdd}");
                var parcelas = ContaFinanceiraBase.ParcelaUnica(rec.ValorPrevisto, vencimento);

                // Corrente inferida da categoria do template (P0-1) — Recorrencia é genérica
                // (aluguel, folha, uma assinatura antiga cadastrada assim...) e não carrega, por
                // si, nenhum sinal mais forte que a categoria configurada. Ver
                // CorrenteDeReceitaInferencia para o racional de ser o único lugar com "melhor
                // esforço" em vez de tagging explícito.
                var corrente = CorrenteDeReceitaInferencia.InferirDaCategoria(rec.CategoriaId);

                if (rec.Tipo == TipoContaRecorrente.APagar)
                {
                    if (await contasAPagar.BuscarPorOrigemAsync(businessId, origem.Chave, ct) is null)
                    {
                        var conta = ContaAPagar.Criar(businessId, origem, rec.Descricao, rec.CategoriaId, vencimento, rec.ValorPrevisto, parcelas, null, null, corrente, rec.ProjetoId);
                        if (conta.Sucesso)
                        {
                            await contasAPagar.SalvarAsync(conta.Valor, ct);
                            var lancamento = LancamentoContabilFactory.DeContaAPagar(conta.Valor);
                            if (lancamento.Sucesso) await lancamentos.SalvarAsync(lancamento.Valor, ct);
                            geradas++;
                        }
                    }
                }
                else
                {
                    if (await contasAReceber.BuscarPorOrigemAsync(businessId, origem.Chave, ct) is null)
                    {
                        var conta = ContaAReceber.Criar(
                            businessId, origem, rec.Descricao, rec.CategoriaId, vencimento, rec.ValorPrevisto, parcelas, null, null, corrente,
                            projetoId: rec.ProjetoId);
                        if (conta.Sucesso)
                        {
                            await contasAReceber.SalvarAsync(conta.Valor, ct);
                            var lancamento = LancamentoContabilFactory.DeContaAReceber(conta.Valor);
                            if (lancamento.Sucesso) await lancamentos.SalvarAsync(lancamento.Valor, ct);
                            geradas++;
                        }
                    }
                }

                rec.RegistrarGeracao(vencimento);
                mudou = true;
            }
            if (mudou) await recorrencias.SalvarAsync(rec, ct);
        }
        return geradas;
    }
}

/// <summary>
/// Gera os recebíveis DEVIDOS das ASSINATURAS ativas (receita recorrente por cliente), com
/// CATCH-UP: cada assinatura pode ter vários ciclos vencidos e não cobrados ainda (cron parado,
/// primeira execução em produção etc.) — o loop interno gera UMA competência por vez
/// (<see cref="Assinatura.GerarCobranca"/> já recusa competência cujo ciclo não venceu — P0-3),
/// avança <c>UltimaCobrancaGeradaEm</c> e repete até não haver mais nada devido até
/// <paramref name="ate"/>. Mesmo padrão de <see cref="GerarContasRecorrentesUseCase"/> para
/// <c>Recorrencia</c>. Idempotente por período (<c>assinatura:{id}:{yyyyMM}</c>) — rodar de novo no
/// mesmo período não duplica: a checagem de ciclo no domínio já bloqueia a re-geração, e a
/// verificação de <see cref="IContaAReceberRepository.BuscarPorOrigemAsync"/> é uma segunda rede
/// contra corrida/replay.
///
/// P0-4 (docs/financeiro/revisao-domain-fit-cnpj.md) — toda cobrança NOVA (nunca em replay) também
/// publica <see cref="CobrancaDeAssinaturaGerada"/> no barramento de integração: é o que fecha o
/// gap de <c>fato_receita_diaria</c>/RBT12 nunca enxergar receita de assinatura (o gerador só criava
/// a <c>ContaAReceber</c> diretamente, sem nenhum evento de integração equivalente a
/// <see cref="VendaConcluida"/>/<see cref="OsFaturada"/> para o fold de receita reagir).
/// </summary>
public sealed class GerarCobrancasAssinaturasUseCase(
    IAssinaturaRepository assinaturas,
    IContaAReceberRepository contasAReceber,
    ILancamentoContabilRepository lancamentos,
    IIntegrationEventBus barramentoDeEventos)
{
    public async Task<int> ExecutarAsync(string businessId, DateTimeOffset ate, CancellationToken ct = default)
    {
        var geradas = 0;
        foreach (var assinatura in await assinaturas.ListarAtivasAsync(businessId, ct))
        {
            var mudou = false;
            while (true)
            {
                var competencia = assinatura.ProximaCompetenciaDevida;
                if (competencia > new DateTimeOffset(ate.Year, ate.Month, 1, 0, 0, 0, ate.Offset)) break;

                var conta = assinatura.GerarCobranca(competencia, CategoriaFinanceiraPadrao.ReceitaRecorrente);
                if (conta.Falha) break; // guarda-chuva: não deveria acontecer dado o check acima

                if (await contasAReceber.BuscarPorOrigemAsync(businessId, conta.Valor.SourceRef.Chave, ct) is null)
                {
                    await contasAReceber.SalvarAsync(conta.Valor, ct);
                    var lancamento = LancamentoContabilFactory.DeContaAReceber(conta.Valor);
                    if (lancamento.Sucesso) await lancamentos.SalvarAsync(lancamento.Valor, ct);

                    await barramentoDeEventos.PublishAsync(
                        new CobrancaDeAssinaturaGerada(assinatura.Id, businessId, conta.Valor.ValorTotal.Centavos, conta.Valor.DataCompetencia, assinatura.ProjetoId), ct);

                    geradas++;
                }
                mudou = true;
            }
            if (mudou) await assinaturas.SalvarAsync(assinatura, ct);
        }
        return geradas;
    }
}
