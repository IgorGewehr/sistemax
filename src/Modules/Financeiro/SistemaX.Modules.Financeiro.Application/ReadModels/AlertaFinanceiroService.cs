using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Domain.Comum;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Application.ReadModels;

public enum TipoAlertaFinanceiro
{
    ContaVencendoOuVencida,
    CaixaProjetadoNegativo
}

public sealed record AlertaFinanceiro(TipoAlertaFinanceiro Tipo, string Mensagem, DateTimeOffset ReferenciaData, Money ValorReferencia);

/// <summary>
/// Alertas inteligentes — escopo do MVP (docs/financeiro-features.md §4.14, priorização #7):
/// só os 2 dos 5 alertas do catálogo completo que não dependem de módulos ainda não construídos
/// nesta partição (margem de contribuição precisa de CMV/estoque; inadimplência recorrente
/// precisa de histórico de cliente; imposto a recolher precisa de DasRecord/Fiscal). Os outros 3
/// ficam documentados como gap de Fase 2 no README do módulo.
/// </summary>
public sealed class AlertaFinanceiroService(
    IContaAPagarRepository contasAPagar, FluxoDeCaixaService fluxoDeCaixa, IRelogio relogio)
{
    private const int DiasAntecedenciaContaVencendo = 3;
    private const int DiasHorizonteProjecaoCaixa = 30;

    public async Task<IReadOnlyList<AlertaFinanceiro>> AvaliarAsync(string businessId, CancellationToken ct = default)
    {
        var agora = relogio.Agora();
        var alertas = new List<AlertaFinanceiro>();

        var contasPagarProximas = await contasAPagar.ListarAbertasAteAsync(businessId, agora.AddDays(DiasAntecedenciaContaVencendo), ct);
        foreach (var conta in contasPagarProximas)
        {
            foreach (var parcela in conta.Parcelas.Where(p => p.Status is StatusFinanceiro.Aberto or StatusFinanceiro.Parcial or StatusFinanceiro.Atrasado))
            {
                var restante = parcela.Valor - parcela.ValorPago;
                var vencida = parcela.Status == StatusFinanceiro.Atrasado;
                var mensagem = vencida
                    ? $"Sua conta '{conta.Descricao}' ({restante.Formatado()}) está atrasada desde {parcela.Vencimento:dd/MM}."
                    : $"Sua conta '{conta.Descricao}' ({restante.Formatado()}) vence em {parcela.Vencimento:dd/MM}.";

                alertas.Add(new AlertaFinanceiro(TipoAlertaFinanceiro.ContaVencendoOuVencida, mensagem, parcela.Vencimento, restante));
            }
        }

        var fluxo = await fluxoDeCaixa.ProjetarAsync(businessId, diasHistorico: 1, diasProjecao: DiasHorizonteProjecaoCaixa, ct);
        if (fluxo.PrimeiroDiaNegativo is { } diaNegativo)
        {
            var ponto = fluxo.Pontos.First(p => p.Data == diaNegativo);
            alertas.Add(new AlertaFinanceiro(
                TipoAlertaFinanceiro.CaixaProjetadoNegativo,
                $"Se nada mudar, seu caixa fica negativo em {diaNegativo:dd/MM} ({ponto.SaldoAcumulado.Formatado()}).",
                diaNegativo.InicioDoDia(),
                ponto.SaldoAcumulado));
        }

        return alertas;
    }
}
