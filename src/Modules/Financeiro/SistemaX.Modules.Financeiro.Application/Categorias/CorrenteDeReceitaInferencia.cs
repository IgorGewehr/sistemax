using SistemaX.Modules.Financeiro.Domain.Comum;

namespace SistemaX.Modules.Financeiro.Application.Categorias;

/// <summary>
/// Inferência de <see cref="CorrenteDeReceita"/> a partir de sinais fracos (categoria, e nada
/// mais) — usada SÓ onde o chamador não tem como saber a corrente de outro jeito:
/// <c>GerarContasRecorrentesUseCase</c> materializa <c>Recorrencia</c>, um template genérico de
/// conta a pagar/receber que não é, por si, nenhuma das três correntes do negócio (pode ser
/// aluguel, uma assinatura de cliente cadastrada como recorrência antiga, etc.) — então a única
/// pista disponível é a categoria configurada no template.
///
/// TODO OS DEMAIS criadores de receita/custo direto (venda, OS, pedido, assinatura de verdade,
/// comissão) SABEM a corrente e marcam explicitamente no ponto de criação — não passam por aqui
/// (P0-1, docs/financeiro/revisao-domain-fit-cnpj.md: "cada criador de receita marca a corrente
/// certa"). Esta classe é deliberadamente o único lugar com inferência "melhor esforço" — mantém a
/// mesma lógica (categoria → corrente) que a migração SQL usa para o backfill retrocompatível de
/// dado histórico (<c>FinanceiroSchemaMigrationV16</c>), documentada aqui para as duas não
/// divergirem silenciosamente.
/// </summary>
public static class CorrenteDeReceitaInferencia
{
    /// <summary>Só reconhece o sinal mais forte que uma categoria isolada pode carregar — receita
    /// recorrente e comissão (custo direto da corrente Serviço). Qualquer outra categoria retorna
    /// <c>null</c> (não classificado nesta dimensão) em vez de arriscar um palpite errado.</summary>
    public static CorrenteDeReceita? InferirDaCategoria(string categoriaId) => categoriaId switch
    {
        CategoriaFinanceiraPadrao.ReceitaRecorrente => CorrenteDeReceita.Recorrente,
        CategoriaFinanceiraPadrao.Comissoes => CorrenteDeReceita.Servico,
        CategoriaFinanceiraPadrao.CustoMercadoriaVendida => CorrenteDeReceita.Comercio,
        _ => null,
    };
}
