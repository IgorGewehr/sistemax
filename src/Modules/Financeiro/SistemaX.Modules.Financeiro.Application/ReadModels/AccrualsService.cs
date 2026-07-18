using SistemaX.Modules.Financeiro.Application.Comum;
using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Application.ReadModels;

/// <summary>Accruals = <see cref="LucroDeCompetenciaCentavos"/> − <see cref="FluxoDeCaixaOperacionalCentavos"/>
/// do mesmo período. Positivo e alto = lucro no papel sem caixa correspondente ainda (alerta de
/// pré-pagamento a fornecedor/recebível crescendo); negativo = caixa operacional ENTROU antes do
/// lucro ser reconhecido (ex.: pacote pré-pago do cliente — caixa cai na hora, a receita reconhece
/// depois, ver <c>ReceitaReconhecidaResolver</c>/P1-5).</summary>
public sealed record AccrualsResultado(
    long LucroDeCompetenciaCentavos, long FluxoDeCaixaOperacionalCentavos, long AccrualsCentavos);

/// <summary>
/// Accruals — "Lucro ≠ Caixa" (ideia 3 do matemonstro, docs/financeiro/ideias-matemonstro.md:
/// `fin-contabilidade` "Qualidade do Lucro e Red Flags" + `fin-gestao-04` "empresa lucrativa quebra
/// por falta de caixa"). É uma SUBTRAÇÃO entre as DUAS LENTES que o Financeiro já mantém — nenhum
/// dado novo, nenhuma fact table nova, nenhum evento novo:
///
/// <list type="bullet">
/// <item><b>Competência</b> — <see cref="DreGerencialService.CalcularAsync"/>.ResultadoOperacional:
/// receita reconhecida (com receita diferida — P1-5) menos custo direto, despesa operacional e MDR
/// (P1-6) do período.</item>
/// <item><b>Caixa operacional</b> — <c>fato_caixa_diario</c> somado no MESMO período:
/// <c>SaldoDiaCentavos</c> (Entradas − Saídas) já é BILATERAL desde a Fatia 6/P1-3 (liquidação de
/// parcela a pagar E a receber, não só entrada à vista) — é exatamente o "caixa gerado pela
/// operação" que a fórmula clássica <c>Accruals = LL − FCO</c> pede.</item>
/// </list>
///
/// LEITURA: accruals alto e positivo por vários períodos seguidos é o sinal clássico de "lucro
/// inflado" (estoque/recebível crescendo sem o caixa acompanhar) — o alerta que a trilha do
/// matemonstro chama de red flag de qualidade do lucro. Accruals muito negativo é o espelho
/// (pré-pagamento do cliente): caixa saudável, lucro contábil ainda represado pela receita diferida.
/// </summary>
public sealed class AccrualsService(DreGerencialService dreGerencial, IFatoCaixaDiarioRepository fatoCaixaDiario)
{
    public async Task<AccrualsResultado> CalcularAsync(
        string businessId, DateTimeOffset inicio, DateTimeOffset fim, CancellationToken ct = default)
    {
        var dre = await dreGerencial.CalcularAsync(businessId, inicio, fim, ct).ConfigureAwait(false);

        var de = BucketingTemporalDoTenant.DiaLocal(inicio);
        var ate = BucketingTemporalDoTenant.DiaLocal(fim);
        var fatosCaixa = await fatoCaixaDiario.ListarAsync(businessId, de, ate, ct).ConfigureAwait(false);
        var fluxoDeCaixaOperacional = fatosCaixa.Sum(f => f.SaldoDiaCentavos);

        var lucroDeCompetencia = dre.ResultadoOperacional.Centavos;

        return new AccrualsResultado(lucroDeCompetencia, fluxoDeCaixaOperacional, lucroDeCompetencia - fluxoDeCaixaOperacional);
    }
}
