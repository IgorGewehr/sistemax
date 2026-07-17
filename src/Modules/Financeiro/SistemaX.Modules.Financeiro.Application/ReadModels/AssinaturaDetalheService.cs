using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Domain.Assinaturas;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Application.ReadModels;

/// <summary>Uma assinatura ATIVA com o suficiente para a tabela "Todas as assinaturas" da lente
/// Assinaturas (docs/wiring/financeiro-telas-restantes.md §2/§C) — cliente, serviço, valor por
/// ciclo e a PRÓXIMA cobrança prevista. <see cref="ProximaCobranca"/> é DERIVADA (nunca persistida):
/// próximo múltiplo do ciclo a partir da última cobrança gerada (ou de <c>DataInicio</c>, se nunca
/// cobrou), com o dia ajustado para <c>DiaCobranca</c> — mesma regra de
/// <see cref="Assinatura.GerarCobranca"/>, só que projetada pra frente em vez de gravada.</summary>
public sealed record AssinaturaDetalhe(
    string Id, string ClienteId, string ClienteNome, string ServicoId, string ServicoNome,
    Money ValorPorCiclo, string Ciclo, string Status, DateTimeOffset ProximaCobranca);

/// <summary>
/// Painel de DETALHE por assinatura (a tabela "Todas as assinaturas" da lente Assinaturas) —
/// complementa o resumo agregado de <see cref="ReceitaRecorrenteService"/> (MRR/ARR/churn) com a
/// lista nominal que o resumo, de propósito, não devolve.
/// </summary>
public sealed class AssinaturaDetalheService(IAssinaturaRepository assinaturas)
{
    public async Task<IReadOnlyList<AssinaturaDetalhe>> ListarAtivasAsync(
        string businessId, DateTimeOffset referencia, CancellationToken ct = default)
    {
        var ativas = await assinaturas.ListarAtivasAsync(businessId, ct).ConfigureAwait(false);

        return ativas
            .Select(a => new AssinaturaDetalhe(
                a.Id, a.ClienteId, a.ClienteNome, a.ServicoId, a.ServicoNome,
                a.ValorPorCiclo, a.Ciclo.ToString(), a.Status.ToString(), CalcularProximaCobranca(a, referencia)))
            .OrderBy(a => a.ProximaCobranca)
            .ToList();
    }

    /// <summary>
    /// P0-3: este cálculo reusa <see cref="Assinatura.AdicionarCiclo"/>/<see cref="Assinatura.AjustarDiaCobranca"/>
    /// — os MESMOS helpers que <see cref="Assinatura.GerarCobranca"/> usa pra decidir se o ciclo já
    /// venceu. Antes deste ajuste, esta classe duplicava (verbatim) a mesma lógica; a UI projetava
    /// certo, mas o gerador ignorava o ciclo — agora há um lar só pra regra.
    /// </summary>
    private static DateTimeOffset CalcularProximaCobranca(Assinatura assinatura, DateTimeOffset referencia)
    {
        // O dia é ajustado ANTES de comparar com a referência em cada passo — ajustar só no
        // final (depois de já ter decidido "essa ocorrência está no futuro") pode empurrar a
        // data de volta pro passado quando DiaCobranca < dia-do-mês da referência.
        var candidata = Assinatura.AjustarDiaCobranca(
            Assinatura.AdicionarCiclo(assinatura.UltimaCobrancaGeradaEm ?? assinatura.DataInicio, assinatura.Ciclo), assinatura.DiaCobranca);

        // Se a última geração ficou atrasada (cron não rodou por um tempo), avança até a
        // próxima ocorrência de fato no futuro — nunca devolve uma "próxima cobrança" no passado.
        while (candidata <= referencia)
            candidata = Assinatura.AjustarDiaCobranca(Assinatura.AdicionarCiclo(candidata, assinatura.Ciclo), assinatura.DiaCobranca);

        return candidata;
    }
}
