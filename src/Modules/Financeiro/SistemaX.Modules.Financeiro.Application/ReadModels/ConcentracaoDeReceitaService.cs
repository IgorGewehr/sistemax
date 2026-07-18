using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Application.Quant;
using SistemaX.Modules.Financeiro.Domain.Comum;

namespace SistemaX.Modules.Financeiro.Application.ReadModels;

/// <summary>Um cliente e sua fatia de receita reconhecida no período — só clientes com
/// <c>ContaAReceber.ClienteId</c> preenchido entram aqui (receita anônima/balcão nunca concentra
/// em "ninguém").</summary>
public sealed record ClientePorReceita(string ClienteId, long ReceitaCentavos, double ParticipacaoPercentual);

public sealed record ConcentracaoDeReceitaResultado(
    long ReceitaTotalCentavos,
    long ReceitaIdentificadaPorClienteCentavos,
    ClientePorReceita? MaiorCliente,
    double? ConcentracaoNoMaiorClientePercentual,
    IReadOnlyList<ClientePorReceita> TopClientes);

/// <summary>
/// Concentração de receita por cliente (P2-4, docs/financeiro/revisao-domain-fit-cnpj.md) — "que
/// fração do meu faturamento depende de UM cliente só?", o risco clássico de dependência de conta
/// grande (perder o maior cliente derruba X% da receita). Reusa <c>ContaAReceber.ClienteId</c> (já
/// gravado na origem — venda de balcão com cliente identificado, OS, assinatura) e a mesma janela
/// de reconhecimento de receita do <see cref="DreGerencialService"/>
/// (<see cref="ReceitaReconhecidaResolver"/>, P1-5) — nenhuma fact table nova, nenhum evento novo.
///
/// <see cref="ConcentracaoDeReceitaResultado.ConcentracaoNoMaiorClientePercentual"/> = receita do
/// maior cliente ÷ receita TOTAL do período (não só a identificada por cliente — um negócio com
/// muita venda anônima de balcão dilui a concentração de qualquer cliente nomeado, o que é
/// correto: o risco de dependência é relativo ao faturamento inteiro, não só à fatia rastreada).
/// </summary>
public sealed class ConcentracaoDeReceitaService(IContaAReceberRepository contasAReceber)
{
    private const int TopN = 5;

    public async Task<ConcentracaoDeReceitaResultado> CalcularAsync(
        string businessId, DateTimeOffset inicio, DateTimeOffset fim, CancellationToken ct = default)
    {
        var contas = await contasAReceber.ListarPorCompetenciaAsync(businessId, inicio, fim, ct).ConfigureAwait(false);
        var reconhecidas = contas.Where(c => c.Status != StatusFinanceiro.Cancelado);

        long receitaTotal = 0;
        var porCliente = new Dictionary<string, long>();

        foreach (var conta in reconhecidas)
        {
            var centavos = ReceitaReconhecidaResolver.CentavosNaJanela(conta, inicio, fim);
            receitaTotal += centavos;
            if (conta.ClienteId is { } clienteId && centavos != 0)
            {
                porCliente[clienteId] = porCliente.GetValueOrDefault(clienteId) + centavos;
            }
        }

        var receitaIdentificada = porCliente.Values.Sum();

        var ranking = porCliente
            .OrderByDescending(par => par.Value)
            .ThenBy(par => par.Key, StringComparer.Ordinal)
            .Take(TopN)
            .Select(par => new ClientePorReceita(
                par.Key, par.Value, receitaTotal > 0 ? (double)par.Value / receitaTotal : 0))
            .ToList();

        var maiorCliente = ranking.Count > 0 ? ranking[0] : null;
        var concentracaoNoMaior = maiorCliente is not null && receitaTotal > 0
            ? (double)maiorCliente.ReceitaCentavos / receitaTotal
            : (double?)null;

        return new ConcentracaoDeReceitaResultado(receitaTotal, receitaIdentificada, maiorCliente, concentracaoNoMaior, ranking);
    }
}
