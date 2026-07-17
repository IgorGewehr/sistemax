using SistemaX.Modules.Abstractions;
using SistemaX.SharedKernel;

namespace SistemaX.Verticals.Assistencia;

/// <summary>
/// Evento de DOMÍNIO: uma <see cref="OrdemDeServico"/> foi faturada (na entrega, ou como taxa de
/// diagnóstico numa devolução sem reparo). Privado ao vertical Assistência — só sai daqui
/// traduzido para o evento de INTEGRAÇÃO <see cref="OsFaturada"/> (já catalogado em
/// Modules.Abstractions, ao lado de <c>VendaConcluida</c>).
///
/// Mesmo padrão do módulo base Vendas (ver VendaDomainEvents.cs): um vertical reaproveita
/// exatamente o mesmo mecanismo "domínio → integração" que um módulo core usa.
///
/// P0-2/P1-7 FECHADO (docs/financeiro/revisao-domain-fit-cnpj.md): <see cref="OsFaturada"/> ganhou
/// os 4 campos aditivos (<c>FormaPagamento</c>/<c>ClienteId</c>/<c>TecnicoId</c>/<c>NumeroOs</c>) —
/// <see cref="ParaEventoDeIntegracao"/> agora os preenche. <see cref="FormaPagamento"/> atravessa
/// como STRING estável (mesma regra de fronteira de <c>SourceRef</c>/<c>NaturezaOperacao</c>: cada
/// módulo mantém sua PRÓPRIA cópia do enum) já normalizada para o mesmo vocabulário que
/// <c>FinanceiroBootstrapSeeder</c> semeia (<c>"dinheiro"</c>/<c>"pix"</c>/<c>"debito"</c>/
/// <c>"credito"</c>) — é o que permite ao <c>OsFaturadaHandler</c> liquidar a parcela na hora, sem
/// que a Assistência precise conhecer o Financeiro.
/// </summary>
public sealed record OsFaturadaDomainEvent(
    string OrdemServicoId,
    string TenantId,
    Money ValorServico,
    Money ValorPecas,
    string? ClienteId,
    string? ClienteNome,
    string? NumeroOs,
    FormaPagamento? FormaPagamento,
    string? TecnicoId) : DomainEvent
{
    public OsFaturada ParaEventoDeIntegracao() => new(
        OrdemServicoId: OrdemServicoId,
        TenantId: TenantId,
        ValorServicoCentavos: ValorServico.Centavos,
        ValorPecasCentavos: ValorPecas.Centavos,
        OcorridoEm: OccurredOn,
        FormaPagamento: FormaPagamento?.ParaChaveFinanceira(),
        ClienteId: ClienteId,
        TecnicoId: TecnicoId,
        NumeroOs: NumeroOs);
}

// ───────────────────────────────────────────────────────────────────────────────────────────
// Contrato de ESTOQUE (plano §6) — PROMOVIDO a evento de INTEGRAÇÃO (P0-2): PecaReservada/
// PecaConsumida/ReservaLiberada/ConsumoEstornado já estão catalogados em Modules.Abstractions e
// o Estoque já assina os 4 (EstoqueModule). Cada evento de DOMÍNIO abaixo ganha
// `ParaEventoDeIntegracao()` — mesmo gesto que `OsFaturadaDomainEvent` já fazia. Conversão de
// unidade: <see cref="Quantidade"/> aqui é INTEIRO de peças (1 peça = 1 unidade); o Estoque
// trabalha em MILÉSIMOS-INTEIROS (<c>Quantidade</c> de <c>Modules.Estoque.Domain</c>) — a
// multiplicação por 1000 é feita AQUI, na fronteira, sem que este vertical precise referenciar o
// módulo Estoque (regra de que verticais não dependem de módulos além de Abstractions/SharedKernel).
// ───────────────────────────────────────────────────────────────────────────────────────────

/// <summary>Peça prevista com produto de catálogo reservada — levantado em
/// <see cref="OrdemDeServico.RegistrarAprovacao"/>. Peça "sob encomenda" (<c>ProdutoId</c> nulo
/// no orçamento) nunca gera este evento — não há o que reservar.</summary>
public sealed record PecaReservadaDomainEvent(
    string OrdemServicoId, string TenantId, string LinhaId, string ProdutoId, int Quantidade) : DomainEvent
{
    public string ChaveIdempotencia => $"os.reserva:{OrdemServicoId}:{LinhaId}";

    public PecaReservada ParaEventoDeIntegracao() => new(
        OrdemServicoId, TenantId, LinhaId, ProdutoId, Quantidade * 1000L, OccurredOn);
}

/// <summary>Peça baixada do estoque físico — levantado peça a peça em
/// <see cref="OrdemDeServico.AplicarPeca"/>/<see cref="OrdemDeServico.AdicionarPecaExtra"/>, no
/// exato momento da aplicação (não em lote no fim: auditável e o cancelamento no meio da
/// execução sabe exatamente o que estornar).</summary>
public sealed record PecaConsumidaDomainEvent(
    string OrdemServicoId, string TenantId, string LinhaId, string ProdutoId, int Quantidade, Money PrecoUnitario) : DomainEvent
{
    public string ChaveIdempotencia => $"os.baixa:{OrdemServicoId}:{LinhaId}";

    public PecaConsumida ParaEventoDeIntegracao() => new(
        OrdemServicoId, TenantId, LinhaId, ProdutoId, Quantidade * 1000L, PrecoUnitario.Centavos, OccurredOn);
}

/// <summary>Peça prevista e reservada, mas nunca aplicada — devolvida ao disponível. Levantado
/// em <see cref="OrdemDeServico.ConcluirExecucao"/> (sobra do orçamento) e em
/// <see cref="OrdemDeServico.Cancelar"/> (cancelamento com orçamento já aprovado).</summary>
public sealed record ReservaLiberadaDomainEvent(
    string OrdemServicoId, string TenantId, string LinhaId, string ProdutoId, int Quantidade) : DomainEvent
{
    public string ChaveIdempotencia => $"os.libera:{OrdemServicoId}:{LinhaId}";

    public ReservaLiberada ParaEventoDeIntegracao() => new(
        OrdemServicoId, TenantId, LinhaId, ProdutoId, Quantidade * 1000L, OccurredOn);
}

/// <summary>Baixa já feita precisa voltar (equipamento desmontado num cancelamento em
/// execução) — levantado em <see cref="OrdemDeServico.Cancelar"/> para cada peça já aplicada.
/// MVP: estorno integral; o ajuste fino de "peça inutilizada, não volta pro físico" é gesto
/// manual de inventário no futuro módulo Estoque (plano §6.4).</summary>
public sealed record ConsumoEstornadoDomainEvent(
    string OrdemServicoId, string TenantId, string LinhaId, string ProdutoId, int Quantidade) : DomainEvent
{
    public string ChaveIdempotencia => $"os.estorno:{OrdemServicoId}:{LinhaId}";

    public ConsumoEstornado ParaEventoDeIntegracao() => new(
        OrdemServicoId, TenantId, LinhaId, ProdutoId, Quantidade * 1000L, OccurredOn);
}
