using SistemaX.Modules.Vendas.Domain;

namespace SistemaX.Modules.Vendas.Application.Ports;

/// <summary>
/// Port do repositório de <see cref="Venda"/>. No PDV real isso é SQLite local — a venda
/// <c>Aberta</c> é persistida a cada mudança (item, desconto, pagamento), não só na conclusão;
/// é essa escrita incremental que dá crash-safety ao carrinho (ver nota de MONTAGEM vs PAGAMENTO
/// em <see cref="Venda"/>). Este port não assume isso: só busca/salva o agregado inteiro, cabe à
/// Infrastructure decidir a granularidade da escrita física.
/// </summary>
public interface IVendaRepository
{
    Task<Venda?> ObterPorIdAsync(string id, CancellationToken ct = default);

    Task SalvarAsync(Venda venda, CancellationToken ct = default);
}
