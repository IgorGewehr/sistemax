using SistemaX.Modules.Abstractions;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Vendas.Domain;

// ─────────────────────────────────────────────────────────────────────────────────────────────
// EVENTO DE DOMÍNIO vs EVENTO DE INTEGRAÇÃO — o par abaixo é o exemplo trabalhado da diferença.
//
//   • VendaConcluidaDomainEvent  → IDomainEvent (SharedKernel). Nasce dentro do agregado Venda,
//     no MESMO processo, na MESMA transação que persiste a Venda. Só o módulo Vendas conhece
//     este tipo — ele nunca atravessa a fronteira de módulo.
//
//   • VendaConcluida             → IIntegrationEvent (Modules.Abstractions). O contrato
//     cross-módulo que o Financeiro (e qualquer outro assinante futuro) conhece e consome.
//     Vive no kernel compartilhado (Modules.Abstractions) justamente para que Vendas e
//     Financeiro concordem no mesmo tipo sem que um referencie o outro.
//
// A ponte entre os dois é o método ParaEventoDeIntegracao() abaixo: uma função pura,
// determinística, sem side-effect — ela só sabe COMO montar o fato de integração a partir do
// fato de domínio, nunca QUANDO publicá-lo.
//
// QUEM CHAMA ParaEventoDeIntegracao() E QUANDO (importante, mesmo não estando implementado
// neste esqueleto, que só tem Domain): a camada de Application/Infrastructure de Vendas, DEPOIS
// do commit da transação que gravou a Venda — nunca dentro do mesmo escopo transacional, nunca
// como parte do método Concluir() do agregado. O fluxo real:
//
//   1. Application chama venda.Concluir(formaPagamento) → agregado valida FSM, muda estado,
//      acumula VendaConcluidaDomainEvent em AggregateRoot.DomainEvents (nada é publicado ainda).
//   2. Application persiste a Venda (commit da transação local).
//   3. SÓ APÓS o commit confirmado, Application itera venda.DomainEvents, chama
//      ParaEventoDeIntegracao() em cada um, e publica via IIntegrationEventBus.PublishAsync().
//   4. venda.ClearDomainEvents() encerra o ciclo.
//
// Ver docs/arquitetura/ARCHITECTURE.md §5 para o diagrama de sequência completo (venda.concluida
// → Financeiro cria ContaAReceber/MovimentoFinanceiro) e docs/arquitetura/COMO-CRIAR-UM-MODULO.md
// para o passo a passo de como repetir este padrão num módulo novo.
//
// EVOLUÇÃO PROPOSTA (documentada, NÃO implementada — Modules.Abstractions é fora do escopo deste
// módulo): hoje Venda comporta split de pagamento (Pagamentos[]) e desconto por item, mas
// VendaConcluida (Modules.Abstractions/IntegrationEvents.cs) só carrega TotalCentavos +
// FormaPagamento (o método PRINCIPAL — ver Venda.FormaPagamento). Quando o Estoque nascer como
// assinante do mesmo fato, o contrato deveria evoluir ADITIVAMENTE para:
//   • Pagamentos: IReadOnlyList<(string Metodo, long ValorCentavos, long? ParcelasQtd)>
//   • LojaId, TerminalId, OperadorId — dimensões de conciliação/BI.
// FormaPagamento (string) permanece preenchido com o método principal para retrocompatibilidade
// do assinante atual (Financeiro). Ver plano de arquitetura do PDV (scratchpad/design/pdv-plano.md
// §7.1) para o racional completo — quem tocar Abstractions decide o formato final junto com quem
// mantém o Financeiro.
//
// FECHADO NESTA REVISÃO (gap que o item acima ainda listava como "Itens" no contrato proposto):
// VendaConcluidaDomainEvent agora carrega Itens (ver ItemVendaParaEstoque abaixo) e sabe traduzir
// para o companion VendaItensMovimentados via ParaVendaItensMovimentados() — o mesmo gesto de par
// "um fato, dois eventos de integração publicados lado a lado" que
// NotaDeCompraRecebidaDomainEvent (Compras) já demonstra com ParaCompraRecebida()/
// ParaCompraItensRecebidos(). Quem publica os dois é ConcluirVendaUseCase, sempre pós-commit.
// ─────────────────────────────────────────────────────────────────────────────────────────────

/// <summary>Linha de venda já convertida para o formato pobre e estável que <see cref="ItemMovimentado"/>
/// (Modules.Abstractions) exige — ponte entre o mundo rico do agregado (<see cref="ItemDeVenda"/>)
/// e o contrato que Estoque/Fiscal consomem. <see cref="QuantidadeMilesimos"/> é sempre múltiplo de
/// 1000 aqui: Vendas só vende unidades inteiras hoje (<see cref="ItemDeVenda.Quantidade"/> é
/// <c>int</c>) — fracionário (KG/L, pesagem no PDV) fica para quando o agregado precisar.</summary>
public sealed record ItemVendaParaEstoque(
    string ItemId, string ProdutoId, string Descricao, long QuantidadeMilesimos,
    long PrecoUnitarioCentavos, long DescontoCentavos);

/// <summary>Linha de pagamento já convertida para o formato pobre e estável que
/// <see cref="PagamentoIntegracao"/> (Modules.Abstractions) exige — ponte entre
/// <see cref="PagamentoDeVenda"/> (rico, com <c>Id</c>/<c>ValorRecebido</c>/<c>Troco</c>) e o
/// contrato de integração (P2-2, docs/financeiro/revisao-domain-fit-cnpj.md).</summary>
public sealed record ItemPagamentoParaIntegracao(string Metodo, long ValorCentavos);

/// <summary>Evento de DOMÍNIO: uma <see cref="Venda"/> foi concluída (pagamento definido, FSM em
/// <see cref="StatusVenda.Concluida"/>). Privado ao módulo Vendas.
///
/// <see cref="ClienteId"/> — FECHADO NESTA REVISÃO o gap que <c>VendaConcluida.ClienteId</c>
/// (Modules.Abstractions) documentava: o agregado <see cref="Venda"/> agora captura o cliente do
/// carrinho (<see cref="Venda.DefinirCliente"/>) e propaga aqui; continua opcional (PDV de balcão
/// não exige cliente identificado).
///
/// <see cref="Pagamentos"/> — FECHADO NESTA REVISÃO (P2-2, docs/financeiro/revisao-domain-fit-cnpj.md)
/// o gap que este arquivo documentava como "evolução proposta, não implementada": a lista completa
/// de <see cref="PagamentoDeVenda"/> da venda (split de pagamento), traduzida para
/// <see cref="VendaConcluida.Pagamentos"/> — só o MÉTODO+VALOR de cada linha, nunca o
/// <c>ValorRecebido</c>/troco (irrelevante para o Financeiro resolver MDR/lag).</summary>
public sealed record VendaConcluidaDomainEvent(
    string VendaId, string TenantId, Money Total, string FormaPagamento,
    IReadOnlyList<ItemVendaParaEstoque> Itens, string? ClienteId = null,
    IReadOnlyList<ItemPagamentoParaIntegracao>? Pagamentos = null) : DomainEvent
{
    /// <summary>Traduz para o evento de INTEGRAÇÃO que o Financeiro assina. Note que os dois
    /// tipos carregam a mesma informação de negócio, mas <see cref="VendaConcluida"/> é o
    /// contrato ESTÁVEL e VERSIONADO em Modules.Abstractions — mudar o agregado Venda
    /// internamente não pode quebrar quem assina o evento de integração.</summary>
    public VendaConcluida ParaEventoDeIntegracao() => new(
        VendaId: VendaId,
        TenantId: TenantId,
        TotalCentavos: Total.Centavos,
        FormaPagamento: FormaPagamento,
        OcorridoEm: OccurredOn,
        ClienteId: ClienteId,
        Pagamentos: Pagamentos?.Select(p => new PagamentoIntegracao(p.Metodo, p.ValorCentavos)).ToArray());

    /// <summary>Traduz para o companion que Estoque e Fiscal assinam (handlers já existem e estão
    /// testados dos dois lados — só faltava esta torneira do lado de Vendas). Mesmo racional de
    /// <c>NotaDeCompraRecebidaDomainEvent.ParaCompraItensRecebidos()</c> em Compras.</summary>
    public VendaItensMovimentados ParaVendaItensMovimentados() => new(
        VendaId: VendaId,
        TenantId: TenantId,
        Itens: Itens.Select(i => new ItemMovimentado(
            ProdutoId: i.ProdutoId,
            Descricao: i.Descricao,
            QuantidadeMilesimos: i.QuantidadeMilesimos,
            PrecoUnitarioCentavos: i.PrecoUnitarioCentavos,
            DescontoCentavos: i.DescontoCentavos,
            ItemId: i.ItemId)).ToArray(),
        OcorridoEm: OccurredOn);
}

/// <summary>Evento de DOMÍNIO: uma <see cref="Venda"/> concluída foi estornada.</summary>
public sealed record VendaEstornadaDomainEvent(
    string VendaId, string TenantId, Money Total) : DomainEvent
{
    public VendaEstornada ParaEventoDeIntegracao() => new(
        VendaId: VendaId,
        TenantId: TenantId,
        TotalCentavos: Total.Centavos,
        OcorridoEm: OccurredOn);
}
