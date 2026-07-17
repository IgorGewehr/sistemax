namespace SistemaX.Modules.Estoque.Domain.Catalogo;

/// <summary>
/// Método de custeio por produto. Todo movimento de <c>Saida</c> congela o custo no instante da
/// baixa (<c>MovimentoDeEstoque.CustoUnitario</c>) independente do método — é isso que torna a
/// migração de método um simples recálculo, nunca perda de informação.
///
/// NESTA ENTREGA só <see cref="CustoMedio"/> está implementado (<c>CalculadoraDeCustoMedio</c>,
/// Application). <see cref="Fifo"/> é o gancho de domínio para V5 (camadas por entrada, consumo
/// em ordem, custo da baixa = média ponderada das camadas consumidas) — o campo já existe no
/// produto hoje porque adicioná-lo depois custaria uma migração; a calculadora hoje trata todo
/// produto como custo médio móvel, seja qual for o valor aqui.
/// </summary>
public enum PoliticaDeValorizacao { CustoMedio, Fifo }
