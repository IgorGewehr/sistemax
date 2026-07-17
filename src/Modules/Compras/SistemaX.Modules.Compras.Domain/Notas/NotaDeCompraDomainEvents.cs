using SistemaX.Modules.Abstractions;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Compras.Domain.Notas;

// ─────────────────────────────────────────────────────────────────────────────────────────────
// EVENTO DE DOMÍNIO vs EVENTO DE INTEGRAÇÃO — mesmo par documentado em
// SistemaX.Modules.Vendas.Domain/VendaDomainEvents.cs. Os eventos DE DOMÍNIO abaixo nascem dentro
// do agregado NotaDeCompra, no mesmo processo/transação; os de INTEGRAÇÃO (CompraRecebida,
// CompraItensRecebidos, CompraEstornada — Modules.Abstractions) são o contrato ESTÁVEL que
// Financeiro e Estoque assinam. A ponte é ParaCompraRecebida()/ParaCompraItensRecebidos()/
// ParaEventoDeIntegracao(): função pura, sem side-effect.
//
// QUEM CHAMA E QUANDO: a Application (CasosDeUso/NotaDeCompraUseCases.cs), DEPOIS do commit da
// transação que gravou a NotaDeCompra — nunca dentro do mesmo escopo transacional. Um fato
// (NotaDeCompraRecebidaDomainEvent), DOIS eventos de integração publicados lado a lado
// (CompraRecebida para o Financeiro + CompraItensRecebidos, companion, para o Estoque) — o mesmo
// desenho "um fato, N assinantes" do VendaConcluida/VendaItensMovimentados.
// ─────────────────────────────────────────────────────────────────────────────────────────────

/// <summary>Linha de item já CONVERTIDA para a unidade de estoque, com o custo de entrada
/// (landed) já congelado — o formato exato que <see cref="ItemMovimentado"/> (Modules.Abstractions)
/// exige. Ponte entre o mundo rico do agregado (<see cref="ItemDeNotaDeCompra"/>, com NF-e/match/
/// rateio) e o contrato pobre e estável que Estoque consome.</summary>
public sealed record ItemRecebidoParaEstoque(
    string ProdutoId, string Descricao, long QuantidadeMilesimos, long CustoUnitarioCentavos,
    string ItemId, string? LoteFornecedor, DateOnly? Validade);

/// <summary>Evento de DOMÍNIO: uma <see cref="NotaDeCompra"/> teve o recebimento confirmado (FSM
/// em <see cref="StatusNotaDeCompra.Recebida"/>, custos de entrada já congelados). Privado ao
/// módulo Compras.</summary>
public sealed record NotaDeCompraRecebidaDomainEvent(
    string CompraId, string TenantId, string FornecedorId, Money Total,
    IReadOnlyList<ItemRecebidoParaEstoque> Itens) : DomainEvent
{
    /// <summary>Traduz para o evento que o Financeiro assina (já existe — <c>CompraRecebidaHandler</c>
    /// cria a ContaAPagar). Contrato intocado: só totais.</summary>
    public CompraRecebida ParaCompraRecebida() => new(CompraId, TenantId, FornecedorId, Total.Centavos, OccurredOn);

    /// <summary>Traduz para o companion que o Estoque assina (já existe — <c>CompraItensRecebidosHandler</c>
    /// credita quantidade e recalcula custo médio por item).</summary>
    public CompraItensRecebidos ParaCompraItensRecebidos() => new(
        CompraId, TenantId, FornecedorId,
        Itens.Select(i => new ItemMovimentado(
            i.ProdutoId, i.Descricao, i.QuantidadeMilesimos, i.CustoUnitarioCentavos,
            ItemId: i.ItemId, LoteNumero: i.LoteFornecedor, Validade: i.Validade)).ToArray(),
        OccurredOn);
}

/// <summary>Evento de DOMÍNIO: um recebimento confirmado foi estornado (FSM volta a
/// <see cref="StatusNotaDeCompra.EmConferencia"/>).</summary>
public sealed record NotaDeCompraEstornadaDomainEvent(
    string CompraId, string TenantId, string FornecedorId, Money Total,
    IReadOnlyList<ItemRecebidoParaEstoque> Itens) : DomainEvent
{
    public CompraEstornada ParaEventoDeIntegracao() => new(
        CompraId, TenantId, FornecedorId,
        Itens.Select(i => new ItemMovimentado(
            i.ProdutoId, i.Descricao, i.QuantidadeMilesimos, i.CustoUnitarioCentavos,
            ItemId: i.ItemId, LoteNumero: i.LoteFornecedor, Validade: i.Validade)).ToArray(),
        Total.Centavos, OccurredOn);
}
