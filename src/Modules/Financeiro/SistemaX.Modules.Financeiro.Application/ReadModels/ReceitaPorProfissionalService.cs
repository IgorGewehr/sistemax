using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Domain.Comum;

namespace SistemaX.Modules.Financeiro.Application.ReadModels;

public sealed record LinhaReceitaPorProfissional(
    string TecnicoId, long ReceitaTotalCentavos, long ReceitaServicoCentavos, long ReceitaPecasCentavos,
    int QuantidadeOs, long MargemAproximadaCentavos);

/// <summary>
/// LENTE VERTICAL SERVIÇOS/BELEZA (opt-in) — receita e margem aproximada por técnico/profissional.
/// ZERO DADO NOVO: reusa <c>ContaAReceber.TecnicoId</c> (já persistido por <c>OsFaturadaHandler</c>
/// desde P1-7, docs/financeiro/revisao-domain-fit-cnpj.md — "já dá pra consultar qual técnico
/// faturou esta OS") + <c>ValorServico</c>/<c>ValorPecas</c> (mesma conta, repartição mão de
/// obra×peças que a OS já grava).
///
/// <see cref="LinhaReceitaPorProfissional.MargemAproximadaCentavos"/> = <c>ReceitaServicoCentavos</c>
/// (mão de obra é margem quase pura, sem CMV) — GAP DOCUMENTADO, o MESMO já registrado em
/// <c>OsFaturadaHandler</c>: não existe cadastro de comissão por tenant nem CMV de peça atribuído
/// por TÉCNICO (<c>CustoBaixadoPorOs</c> só folda agregado em <c>fato_custo_diario</c> por
/// dia/corrente, nunca por OS/técnico) — inventar esse desconto violaria a regra de nunca produzir
/// um percentual que o cadastro não expressa. Quando o cadastro de comissão existir, este
/// read-model ganha o desconto no mesmo PR que o handler de origem.
///
/// OPT-IN por presença de dado: só aparece técnico com ao menos uma OS faturada (corrente Serviço)
/// no período — sem OS nenhuma com técnico atribuído, lista vazia (a lente não aparece pra quem
/// não usa o vertical Assistência/serviço com técnico).
/// </summary>
public sealed class ReceitaPorProfissionalService(IContaAReceberRepository contasAReceber)
{
    public async Task<IReadOnlyList<LinhaReceitaPorProfissional>> CalcularAsync(
        string businessId, DateTimeOffset de, DateTimeOffset ate, CancellationToken ct = default)
    {
        var contas = await contasAReceber.ListarPorCompetenciaAsync(businessId, de, ate, ct).ConfigureAwait(false);

        return contas
            .Where(conta => conta.TecnicoId is not null && conta.Corrente == CorrenteDeReceita.Servico)
            .GroupBy(conta => conta.TecnicoId!)
            .Select(grupo =>
            {
                var servico = grupo.Sum(conta => conta.ValorServico?.Centavos ?? 0);
                var pecas = grupo.Sum(conta => conta.ValorPecas?.Centavos ?? 0);
                return new LinhaReceitaPorProfissional(grupo.Key, servico + pecas, servico, pecas, grupo.Count(), servico);
            })
            .OrderByDescending(linha => linha.ReceitaTotalCentavos)
            .ToList();
    }
}
