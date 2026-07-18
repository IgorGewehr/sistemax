using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Domain.Ativos;
using SistemaX.Modules.Financeiro.Domain.Contabil;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Application.Ativos;

/// <summary>
/// O GERADOR (cron) de reconhecimento de competência — clone estrutural de
/// <c>GerarCobrancasAssinaturasUseCase</c> (docs/financeiro/design-analise-por-projeto.md §4.5):
/// mesmo catch-up em loop (um ativo pode ter várias competências atrasadas — cron parado, primeira
/// execução em produção), mesma idempotência DUPLA (cursor no domínio via
/// <see cref="AtivoDeCapital.ReconhecerCompetencia"/> + <c>BuscarPorOrigemAsync</c> antes de
/// persistir o <c>LancamentoContabil</c>), e mesma publicação do evento de integração
/// (<c>CustoAmortizadoReconhecido</c>) só quando o reconhecimento É NOVO (nunca em replay).
///
/// IMPORTANTE (design §4.5): o DRE e o painel NÃO dependem deste cron para o número — eles leem
/// <see cref="AtivoDeCapitalQuant"/> (função pura sobre o cronograma) diretamente. Este cron só
/// materializa o RASTRO contábil (o <c>LancamentoContabil</c> mensal) e o evento no ledger — um
/// cron atrasado nunca produz um número errado no DRE, só atrasa o lançamento/evento.
/// </summary>
public sealed class ReconhecerAmortizacoesUseCase(
    IAtivoDeCapitalRepository ativos, ILancamentoContabilRepository lancamentos, IIntegrationEventBus barramentoDeEventos)
{
    public async Task<int> ExecutarAsync(string businessId, DateTimeOffset ate, CancellationToken ct = default)
    {
        var reconhecidas = 0;
        var limite = new DateOnly(ate.Year, ate.Month, 1);

        foreach (var ativo in await ativos.ListarEmUsoAsync(businessId, ct).ConfigureAwait(false))
        {
            var mudou = false;
            while (true)
            {
                var devida = ativo.ProximaCompetenciaDevida;
                if (devida > limite) break;

                var valorCentavos = AtivoDeCapitalQuant.ValorNaCompetencia(ativo, devida);
                var resultado = ativo.ReconhecerCompetencia(devida, valorCentavos, ate);
                if (resultado.Falha) break; // guarda-chuva: não deveria acontecer dado o check acima

                mudou = true;

                var origemId = $"{ativo.Id}:{devida:yyyyMM}";
                var origemChave = $"financeiro.amortizacao:{origemId}";
                if (await lancamentos.BuscarPorOrigemAsync(businessId, origemChave, ct).ConfigureAwait(false) is null)
                {
                    var descricao = $"Amortização/depreciação — {ativo.Nome} ({devida:yyyy-MM})";
                    var competenciaData = new DateTimeOffset(devida.Year, devida.Month, 1, 0, 0, 0, TimeSpan.Zero);
                    var lancamentoResultado = LancamentoContabilFactory.DeReconhecimentoDeAtivoDeCapital(
                        businessId, competenciaData, descricao, "amortizacao", origemId, new Money(valorCentavos));
                    if (lancamentoResultado.Sucesso)
                    {
                        await lancamentos.SalvarAsync(lancamentoResultado.Valor, ct).ConfigureAwait(false);
                    }

                    await barramentoDeEventos.PublishAsync(
                        new CustoAmortizadoReconhecido(ativo.Id, businessId, ativo.ProjetoId, $"{devida:yyyy-MM}", valorCentavos, ate), ct)
                        .ConfigureAwait(false);

                    reconhecidas++;
                }

                if (ativo.Status != StatusAtivoDeCapital.EmUso) break; // encerrou nesta rodada — nada mais a reconhecer
            }
            if (mudou) await ativos.SalvarAsync(ativo, ct).ConfigureAwait(false);
        }
        return reconhecidas;
    }
}
