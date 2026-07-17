using SistemaX.Modules.Compras.Domain.Notas;

namespace SistemaX.Modules.Compras.Application.CasosDeUso;

/// <summary>
/// Linha de entrada para <see cref="RegistrarEntradaDeNotaUseCase"/> — já o resultado do passo 1
/// (PARSE) do pipeline de importação (plano §4). O parser de XML em si (regex namespace-agnóstico
/// sobre o <c>&lt;det&gt;</c> da NF-e) é responsabilidade de um adapter de Infrastructure (ex.:
/// leitor de arquivo → este DTO) fora do escopo desta entrega — este caso de uso já recebe o fato
/// estruturado, seja de um upload de XML ou de uma digitação manual.
/// </summary>
public sealed record ItemDeEntradaInput(
    int NItem,
    string? CProd,
    string DescricaoNf,
    string? Ncm,
    string UnidadeNf,
    long QuantidadeNfMilesimos,
    long VProdCentavos,
    long VDescCentavos = 0,
    long? VFreteItemCentavos = null,
    long? VSegItemCentavos = null,
    long? VOutroItemCentavos = null,
    long VIpiCentavos = 0,
    long VIcmsStCentavos = 0,
    string? LoteFornecedor = null,
    DateOnly? Validade = null,
    // ProdutoIdConhecido: preenchido quando quem chama já sabe o produto (nota Manual digitada, ou
    // UI que deixou o operador escolher direto) — cai na estratégia 2 do cascata (§5 do plano) sem
    // precisar do de-para aprendido.
    string? ProdutoIdConhecido = null,
    long? FatorConversaoConhecidoMilesimos = null);

/// <summary>Entrada completa de uma nota — os totais (<c>ICMSTot</c>) e a lista de itens.</summary>
public sealed record EntradaDeNotaInput(
    string TenantId,
    string LojaId,
    OrigemNota Origem,
    string Numero,
    string Serie,
    DateTimeOffset DataEmissao,
    string? FornecedorId,
    string? ChaveDeAcessoBruta,
    long VProdCentavos,
    long VNfCentavos,
    IReadOnlyList<ItemDeEntradaInput> Itens,
    long VFreteCentavos = 0,
    long VSeguroCentavos = 0,
    long VOutroCentavos = 0,
    long VDescontoCentavos = 0,
    long VStCentavos = 0,
    long VIpiCentavos = 0);
