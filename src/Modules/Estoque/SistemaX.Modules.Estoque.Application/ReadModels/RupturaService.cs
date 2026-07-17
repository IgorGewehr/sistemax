using SistemaX.Modules.Estoque.Application.Comum;
using SistemaX.Modules.Estoque.Application.Ports;
using SistemaX.Modules.Estoque.Domain.Comum;
using SistemaX.Modules.Estoque.Domain.Razao;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Estoque.Application.ReadModels;

public sealed record LinhaRuptura(
    string ProdutoId, string ProdutoNome, int DiasEmRuptura, Quantidade ConsumoMedioDiarioNoPeriodo, Money VendaPerdidaEstimada);

/// <summary>
/// Ruptura: quantos dias, dentro da janela pedida, o produto ficou com <c>Disponivel ≤ 0</c> — e a
/// venda perdida estimada nesse tempo (consumo médio diário × dias em ruptura × preço de
/// referência do catálogo). O disponível é reconstruído por REPLAY COMPLETO do razão do produto
/// (nunca só os movimentos dentro da janela — senão o ponto de partida do disponível seria
/// desconhecido); só a CONTAGEM de dias em ruptura é recortada para [inicio, fim]. É a mesma
/// filosofia de "posição retroativa por replay" do resto do módulo.
/// </summary>
public sealed class RupturaService(IMovimentoRepository movimentos, IProdutoRepository produtos)
{
    public async Task<IReadOnlyList<LinhaRuptura>> AnalisarAsync(string tenantId, DateTimeOffset inicio, DateTimeOffset fim, CancellationToken ct = default)
    {
        var resultado = new List<LinhaRuptura>();
        var produtosControlados = (await produtos.ListarAsync(tenantId, ct)).Where(p => p.ControlaEstoque);

        foreach (var produto in produtosControlados)
        {
            // Ordem aqui é por OcorridoEm (tempo de NEGÓCIO), não por Id — diferente do replay de
            // saldo (RecalcularSaldoUseCase), que usa ordem de criação de propósito. Uma linha do
            // tempo de ruptura precisa responder "quando" com o instante que o fato realmente
            // ocorreu; um movimento tardio (backfill, retry do bus) pode ter Id "mais novo" que
            // OcorridoEm mais antigo, e usar Id aqui quebraria a reconstrução da janela.
            var razaoCompleto = (await movimentos.ListarPorProdutoAsync(tenantId, produto.Id, EstoqueConstantes.DepositoPadrao, ct))
                .OrderBy(m => m.OcorridoEm)
                .ThenBy(m => m.Id, StringComparer.Ordinal)
                .ToList();
            if (razaoCompleto.Count == 0) continue;

            var (diasEmRuptura, saidaNoPeriodo) = ReplayarJanela(razaoCompleto, inicio, fim);
            if (diasEmRuptura <= 0) continue;

            var diasDaJanela = Math.Max(1, (fim - inicio).Days);
            var consumoMedioDiario = Quantidade.DeDecimal(saidaNoPeriodo.EmDecimal / diasDaJanela);

            var vendaPerdidaCentavos = (decimal)consumoMedioDiario.Milesimos / 1000m * (decimal)diasEmRuptura * produto.PrecoVenda.Centavos;
            var vendaPerdidaEstimada = new Money((long)Math.Round(vendaPerdidaCentavos, MidpointRounding.ToEven));

            resultado.Add(new LinhaRuptura(produto.Id, produto.Nome, (int)Math.Round(diasEmRuptura), consumoMedioDiario, vendaPerdidaEstimada));
        }

        return resultado.OrderByDescending(linha => linha.VendaPerdidaEstimada.Centavos).ToList();
    }

    /// <summary>Disponível é piecewise-constant entre instantes de movimento — soma-se o tempo em
    /// que ficou ≤ 0, recortado à janela [inicio, fim].</summary>
    private static (double DiasEmRuptura, Quantidade SaidaNoPeriodo) ReplayarJanela(
        IReadOnlyList<MovimentoDeEstoque> razaoCompleto, DateTimeOffset inicio, DateTimeOffset fim)
    {
        var disponivel = Quantidade.Zero;
        var saidaNoPeriodo = Quantidade.Zero;
        double diasEmRuptura = 0;
        DateTimeOffset? instanteAnterior = null;

        foreach (var movimento in razaoCompleto)
        {
            if (instanteAnterior is { } anterior)
                diasEmRuptura += SobreposicaoEmDias(anterior, movimento.OcorridoEm, inicio, fim, disponivel);

            disponivel += movimento.EfeitoFisico - movimento.EfeitoReservado;
            if (movimento.Tipo == TipoMovimento.Saida && movimento.OcorridoEm >= inicio && movimento.OcorridoEm <= fim)
                saidaNoPeriodo += movimento.Quantidade;

            instanteAnterior = movimento.OcorridoEm;
        }

        if (instanteAnterior is { } ultimo)
            diasEmRuptura += SobreposicaoEmDias(ultimo, fim, inicio, fim, disponivel);

        return (diasEmRuptura, saidaNoPeriodo);
    }

    private static double SobreposicaoEmDias(DateTimeOffset trechoInicio, DateTimeOffset trechoFim, DateTimeOffset inicio, DateTimeOffset fim, Quantidade disponivelNoTrecho)
    {
        if (disponivelNoTrecho.EhPositiva) return 0;

        var overlapInicio = trechoInicio > inicio ? trechoInicio : inicio;
        var overlapFim = trechoFim < fim ? trechoFim : fim;

        return overlapFim > overlapInicio ? (overlapFim - overlapInicio).TotalDays : 0;
    }
}
