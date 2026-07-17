using SistemaX.Modules.Estoque.Domain.Comum;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Estoque.Domain.Saldos;

/// <summary>
/// Custo médio móvel — o método PADRÃO (e o que o fisco brasileiro aceita para empresa sem custo
/// integrado). A cada <c>Entrada</c>: <c>novoCM = (Fisico×CM + Qtd×CustoEntrada) / (Fisico+Qtd)</c>,
/// em centavos com arredondamento bancário. <c>Saida</c>/<c>Perda</c> NÃO chamam esta calculadora —
/// elas só CONGELAM o CM vigente no <c>CustoUnitario</c> do movimento (CMV da operação).
///
/// Caso-borda deliberado: <c>Fisico ≤ 0</c> (saldo negativo por venda além do físico) zera a
/// história e adota o custo da entrada — carregar uma média "negativa" não tem significado
/// contábil e o próximo lote é a única referência de custo disponível.
/// </summary>
public static class CalculadoraDeCustoMedio
{
    public static Money Recalcular(Quantidade fisicoAntesDaEntrada, Money custoMedioAtual, Quantidade quantidadeEntrada, Money custoEntrada)
    {
        if (!fisicoAntesDaEntrada.EhPositiva)
            return custoEntrada;

        var novoFisico = fisicoAntesDaEntrada + quantidadeEntrada;
        if (!novoFisico.EhPositiva)
            return custoEntrada; // guarda defensiva — não deveria ocorrer (entrada é sempre positiva)

        var valorAtual = (decimal)fisicoAntesDaEntrada.Milesimos * custoMedioAtual.Centavos;
        var valorEntrada = (decimal)quantidadeEntrada.Milesimos * custoEntrada.Centavos;
        var novoCustoCentavos = (valorAtual + valorEntrada) / novoFisico.Milesimos;

        return new Money((long)Math.Round(novoCustoCentavos, MidpointRounding.ToEven));
    }
}
