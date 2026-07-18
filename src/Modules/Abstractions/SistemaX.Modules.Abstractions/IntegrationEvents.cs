namespace SistemaX.Modules.Abstractions;

/// <summary>
/// Evento de INTEGRAÇÃO: fato cross-módulo publicado por um módulo e consumido por outros.
/// É o mecanismo pelo qual "TUDO ALIMENTA O FINANCEIRO": cada módulo (Vendas, Compras, OS,
/// Agenda…) emite; o Financeiro é o assinante-mestre que vira fato de negócio em fato
/// financeiro (recebível, pagável, receita, custo).
///
/// Os contratos vivem AQUI (kernel compartilhado) para que emissor e consumidor referenciem
/// o MESMO tipo sem que um módulo dependa do outro (Financeiro não conhece Vendas).
///
/// IDEMPOTÊNCIA (regra dura): todo evento carrega uma <see cref="ChaveIdempotencia"/> estável.
/// Reprocessar o mesmo evento NÃO pode duplicar lançamento financeiro. A chave é derivada do
/// fato de origem (id da venda/compra/OS), NUNCA de timestamp — a lição do bug de granularidade
/// de 1 segundo encontrado no Supermarket-OS (ver docs/robustez).
///
/// <see cref="TenantId"/> (F0 do plano de inteligência do Financeiro, ver
/// docs/financeiro/inteligencia-arquitetura.md e ADR-0005): todo evento do catálogo abaixo já
/// carregava um campo <c>TenantId</c> próprio — promovido ao contrato porque o ledger append-only
/// (<c>IIntegrationEventLedgerStore</c>, <c>Runtime/InProcessIntegrationEventBus</c>) precisa dele
/// de forma genérica, sem reflection, pra gravar cada fato já particionável por tenant. Nenhum
/// record do catálogo mudou: cada um já expõe <c>TenantId</c> com o mesmo nome/tipo, então a
/// interface só nomeia o que já existia.
/// </summary>
public interface IIntegrationEvent
{
    string ChaveIdempotencia { get; }
    DateTimeOffset OcorridoEm { get; }
    string TenantId { get; }
}

// ───────────────────────────────────────────────────────────────────────────────
// Catálogo inicial (deriva de docs/financeiro/financeiro-datamodel.md).
// Valores SEMPRE em centavos-inteiros. Expandir conforme os módulos nascem.
// ───────────────────────────────────────────────────────────────────────────────

/// <summary>Venda finalizada no PDV → gera RECEITA (competência) + ENTRADA (caixa).
/// <see cref="ClienteId"/> — companion aditivo da F0 do plano de inteligência do Financeiro
/// (docs/financeiro/inteligencia-arquitetura.md §3.3/ADR-0005): desbloqueia coorte/LTV/RFM/
/// inadimplência por cliente quando os folds da F1 chegarem. FECHADO NESTA REVISÃO: o agregado
/// <c>Venda</c> (módulo Vendas) agora captura cliente no carrinho (<c>Venda.DefinirCliente</c>) e
/// <c>ConcluirVendaUseCase</c> publica o valor real — continua opcional (PDV de balcão permite
/// venda sem cliente identificado, então <c>null</c> é um valor de negócio válido, não mais um
/// gap). Nenhum assinante atual (Financeiro) lê este campo ainda — folds da F1 o farão.</summary>
public sealed record VendaConcluida(
    string VendaId, string TenantId, long TotalCentavos, string FormaPagamento, DateTimeOffset OcorridoEm,
    string? ClienteId = null)
    : IIntegrationEvent
{
    public string ChaveIdempotencia => $"venda.concluida:{VendaId}";
}

/// <summary>Venda estornada → lança REVERSÃO imutável (nunca apaga o fato original).</summary>
public sealed record VendaEstornada(
    string VendaId, string TenantId, long TotalCentavos, DateTimeOffset OcorridoEm)
    : IIntegrationEvent
{
    public string ChaveIdempotencia => $"venda.estornada:{VendaId}";
}

/// <summary>Compra/NF-e de fornecedor recebida → gera CUSTO + CONTA A PAGAR.</summary>
public sealed record CompraRecebida(
    string CompraId, string TenantId, string FornecedorId, long TotalCentavos, DateTimeOffset OcorridoEm)
    : IIntegrationEvent
{
    public string ChaveIdempotencia => $"compra.recebida:{CompraId}";
}

/// <summary>
/// Ordem de Serviço faturada (assistência) → RECEITA de serviço + peças; pode gerar A RECEBER.
///
/// CAMPOS ADITIVOS (P1-7, docs/financeiro/revisao-domain-fit-cnpj.md): <see cref="FormaPagamento"/>,
/// <see cref="ClienteId"/>, <see cref="TecnicoId"/> e <see cref="NumeroOs"/> fecham o gap descoberto
/// na auditoria — sem eles o <c>OsFaturadaHandler</c> não sabia se a OS foi paga na entrega (a
/// Assistência não tem "a prazo": <c>OrdemDeServico.Entregar</c> exige forma de pagamento) e todo
/// recebível de OS nascia com vencimento igual à emissão sem nunca liquidar, virando "atrasado"
/// fantasma no dia seguinte. <c>FormaPagamento</c> nulo é o único caso legítimo de "a prazo": a taxa
/// de diagnóstico de <c>OrdemDeServico.DevolverSemReparo</c> não carrega forma de pagamento.
/// Retrocompatível: todos os 4 são opcionais, nenhum call site existente quebra.
/// </summary>
public sealed record OsFaturada(
    string OrdemServicoId, string TenantId, long ValorServicoCentavos, long ValorPecasCentavos, DateTimeOffset OcorridoEm,
    string? FormaPagamento = null, string? ClienteId = null, string? TecnicoId = null, string? NumeroOs = null)
    : IIntegrationEvent
{
    public string ChaveIdempotencia => $"os.faturada:{OrdemServicoId}";
}

/// <summary>Pedido (delivery/balcão) pago → ENTRADA em caixa (com forma de pagamento).</summary>
public sealed record PedidoPago(
    string PedidoId, string TenantId, long TotalCentavos, string FormaPagamento, DateTimeOffset OcorridoEm)
    : IIntegrationEvent
{
    public string ChaveIdempotencia => $"pedido.pago:{PedidoId}";
}

/// <summary>
/// Parcela (a pagar/receber) venceu → dispara ALERTA e move visão de caixa.
///
/// <see cref="ContaId"/> (P1-4, docs/financeiro/revisao-domain-fit-cnpj.md) — a conta (ContaAReceber/
/// ContaAPagar) DONA da parcela: fecha o gap de <c>DunningAssinaturaHandler</c> não conseguir
/// resolver "essa parcela vencida é de qual assinatura?" sem ele (o evento de domínio
/// <c>ParcelaMarcadaVencida</c> sempre carregou o id da conta — só a tradução pra este evento de
/// integração o descartava).
/// </summary>
public sealed record ParcelaVencida(
    string ContaId, string ParcelaId, string TenantId, long ValorCentavos, bool EhAPagar, DateTimeOffset OcorridoEm)
    : IIntegrationEvent
{
    public string ChaveIdempotencia => $"parcela.vencida:{ParcelaId}";
}

/// <summary>Folha/pró-labore lançado → CUSTO (competência) + CONTA A PAGAR.</summary>
public sealed record FolhaLancada(
    string LancamentoId, string TenantId, string Competencia, long TotalCentavos, DateTimeOffset OcorridoEm)
    : IIntegrationEvent
{
    public string ChaveIdempotencia => $"folha.lancada:{LancamentoId}";
}

// ───────────────────────────────────────────────────────────────────────────────
// ESTOQUE (módulo novo) — catálogo adicionado quando o módulo nasceu (ver
// src/Modules/Estoque/README.md). Regra de evolução do projeto: nenhum campo dos
// eventos ACIMA mudou — tudo aqui é ADITIVO (records novos), nunca alteração de
// assinatura existente.
// ───────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Linha de item com efeito de estoque — companion de <see cref="VendaConcluida"/> e
/// <see cref="CompraRecebida"/> (que continuam só com totais; o Estoque precisa do detalhe por
/// produto). Quantidade em MILÉSIMOS-INTEIROS — mesmo espírito de centavos-inteiros do
/// <c>Money</c>: nunca <c>double</c> tocando saldo. Ver
/// <c>SistemaX.Modules.Estoque.Domain.Comum.Quantidade</c>.
/// </summary>
public sealed record ItemMovimentado(
    string ProdutoId,
    string Descricao,
    long QuantidadeMilesimos,
    long PrecoUnitarioCentavos,
    long DescontoCentavos = 0,
    string? ItemId = null,
    string? LoteNumero = null,
    DateOnly? Validade = null);

/// <summary>
/// Companion ADITIVO de <see cref="VendaConcluida"/> com as linhas da venda, para o Estoque baixar
/// físico por produto (expandindo ficha técnica/BOM quando aplicável) sem que
/// <see cref="VendaConcluida"/> precise mudar (o Financeiro continua lendo só o total).
///
/// GAP DOCUMENTADO: o módulo Vendas (fora do escopo da tarefa que criou o Estoque — regra de
/// execução proíbe tocar em outro módulo) ainda não publica este evento. O gesto de wiring, quando
/// alguém tocar Vendas, é o MESMO já demonstrado por <c>VendaConcluidaDomainEvent.ParaEventoDeIntegracao()</c>:
/// a Application de Vendas publica os dois eventos de integração (o existente + este) lado a lado,
/// pós-commit, a partir do mesmo <c>VendaConcluidaDomainEvent</c> (que já tem os itens da venda).
/// Até lá, o Estoque tem o handler pronto e testado — só falta a torneira do lado de Vendas.
/// </summary>
public sealed record VendaItensMovimentados(
    string VendaId, string TenantId, IReadOnlyList<ItemMovimentado> Itens, DateTimeOffset OcorridoEm)
    : IIntegrationEvent
{
    public string ChaveIdempotencia => $"venda.itens:{VendaId}";
}

/// <summary>
/// Companion ADITIVO de <see cref="CompraRecebida"/> com as linhas da nota — o Estoque consome
/// para dar Entrada por item e recalcular custo médio. GAP DOCUMENTADO: não existe módulo Compras
/// no repo ainda para publicá-lo (fora do escopo desta tarefa); mesmo racional de wiring de
/// <see cref="VendaItensMovimentados"/> acima.
/// </summary>
public sealed record CompraItensRecebidos(
    string CompraId, string TenantId, string FornecedorId, IReadOnlyList<ItemMovimentado> Itens, DateTimeOffset OcorridoEm)
    : IIntegrationEvent
{
    public string ChaveIdempotencia => $"compra.itens:{CompraId}";
}

// ───────────────────────────────────────────────────────────────────────────────
// PROMOÇÃO dos 4 eventos de peça da OS (Assistência) de evento de DOMÍNIO para evento de
// INTEGRAÇÃO — o próprio vertical já documentava este gap em OrdemDeServicoDomainEvents.cs
// ("no dia em que o módulo Estoque nascer..."). Payload e chave são os já fixados no plano da OS.
//
// GAP DOCUMENTADO (mesma régua acima): a regra de execução desta tarefa proíbe tocar no vertical
// Assistência, então `ParaEventoDeIntegracao()` nos 4 DomainEvents de lá (o mesmo gesto de 5
// linhas que `OsFaturadaDomainEvent` já faz) e a publicação pós-commit via outbox ficam como
// follow-up de quem mantiver a Assistência. O Estoque já assina os 4 e está testado.
// ───────────────────────────────────────────────────────────────────────────────

/// <summary>OS aprovada reservou peça de catálogo. Disponível ↓, físico intacto. Sem saldo ⇒
/// reserva descoberta (nunca bloqueia a OS — política já firmada no plano da OS).</summary>
public sealed record PecaReservada(
    string OrdemServicoId, string TenantId, string LinhaId, string ProdutoId,
    long QuantidadeMilesimos, DateTimeOffset OcorridoEm) : IIntegrationEvent
{
    public string ChaveIdempotencia => $"os.reserva:{OrdemServicoId}:{LinhaId}";
}

/// <summary>Peça aplicada no equipamento: consome a reserva da linha (se houver) e baixa o
/// físico. <see cref="PrecoUnitarioCentavos"/> é preço de VENDA (para análise de margem); o custo
/// da baixa vem do próprio Estoque (custo médio vigente), nunca deste campo.</summary>
public sealed record PecaConsumida(
    string OrdemServicoId, string TenantId, string LinhaId, string ProdutoId,
    long QuantidadeMilesimos, long PrecoUnitarioCentavos, DateTimeOffset OcorridoEm) : IIntegrationEvent
{
    public string ChaveIdempotencia => $"os.baixa:{OrdemServicoId}:{LinhaId}";
}

/// <summary>Peça prevista e reservada, nunca aplicada (sobra de orçamento / cancelamento) — devolve
/// ao disponível.</summary>
public sealed record ReservaLiberada(
    string OrdemServicoId, string TenantId, string LinhaId, string ProdutoId,
    long QuantidadeMilesimos, DateTimeOffset OcorridoEm) : IIntegrationEvent
{
    public string ChaveIdempotencia => $"os.libera:{OrdemServicoId}:{LinhaId}";
}

/// <summary>Baixa já feita volta ao físico (cancelamento em execução). MVP: estorno integral;
/// "peça inutilizada" vira gesto manual de Perda depois.</summary>
public sealed record ConsumoEstornado(
    string OrdemServicoId, string TenantId, string LinhaId, string ProdutoId,
    long QuantidadeMilesimos, DateTimeOffset OcorridoEm) : IIntegrationEvent
{
    public string ChaveIdempotencia => $"os.estorno:{OrdemServicoId}:{LinhaId}";
}

// ───────────────────────────────────────────────────────────────────────────────
// Eventos NOVOS publicados PELO Estoque (assinantes futuros: Alertas/UI, OS, Compras, Financeiro).
// ───────────────────────────────────────────────────────────────────────────────

/// <summary>Disponível cruzou o mínimo PARA BAIXO (só na transição — estava acima, cruzou agora;
/// vendas subsequentes com saldo já baixo não re-alertam). Chave inclui o <c>MovimentoId</c> que
/// causou o cruzamento.</summary>
public sealed record EstoqueAbaixoDoMinimo(
    string ProdutoId, string TenantId, string ProdutoNome, long DisponivelMilesimos, long MinimoMilesimos,
    string MovimentoId, DateTimeOffset OcorridoEm) : IIntegrationEvent
{
    public string ChaveIdempotencia => $"estoque.minimo:{ProdutoId}:{MovimentoId}";
}

/// <summary>Reserva de OS ficou com <c>Reservado > Físico</c> — a OS não é bloqueada, mas ganha
/// aviso de peça em falta.</summary>
public sealed record ReservaDescoberta(
    string OrdemServicoId, string TenantId, string LinhaId, string ProdutoId, long FaltamMilesimos,
    DateTimeOffset OcorridoEm) : IIntegrationEvent
{
    public string ChaveIdempotencia => $"estoque.descoberta:{OrdemServicoId}:{LinhaId}";
}

/// <summary>Perda manual registrada (quebra/validade/furto/outro) — o Financeiro pode lançar o
/// custo da perda no DRE.</summary>
public sealed record PerdaRegistrada(
    string MovimentoId, string TenantId, string ProdutoId, long QuantidadeMilesimos,
    long CustoTotalCentavos, string Motivo, DateTimeOffset OcorridoEm) : IIntegrationEvent
{
    public string ChaveIdempotencia => $"estoque.perda:{MovimentoId}";
}

// ───────────────────────────────────────────────────────────────────────────────
// COMPRAS (módulo novo) — evento ADITIVO do catálogo do plano de Compras
// (scratchpad/design/compras-plano.md §8.3). `CompraRecebida`/`CompraItensRecebidos` ACIMA já
// cobriam o caminho feliz (Financeiro cria ContaAPagar; Estoque credita por item); faltava o
// caminho de erro: estornar um recebimento já confirmado (nota lançada por engano, CNPJ errado
// etc.) — mesmo racional de `VendaEstornada` acima. Nenhuma assinatura já existente mudou.
// ───────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Recebimento de compra ESTORNADO — nunca edita/apaga o fato original, só lança a reversão
/// (mesmo racional de <see cref="VendaEstornada"/>). Carrega os MESMOS itens de
/// <see cref="CompraItensRecebidos"/> para o Estoque debitar de volta a quantidade exata que
/// havia creditado; o Financeiro decide se cancela a ContaAPagar (ainda aberta) ou lança um fato
/// de reversão (já com parcela liquidada) — nunca edição do lançamento original.
///
/// GAP DOCUMENTADO (mesmo padrão já usado pelo Estoque para `CompraItensRecebidos`/
/// `VendaItensMovimentados`): nem Financeiro nem Estoque têm handler para este evento ainda —
/// regra de execução desta tarefa proíbe tocar nos dois módulos. O Compras já publica; falta a
/// torneira do lado de quem assina, no mesmo gesto de wiring que os handlers de
/// <see cref="CompraRecebida"/>/<see cref="CompraItensRecebidos"/> já demonstram.
/// </summary>
public sealed record CompraEstornada(
    string CompraId, string TenantId, string FornecedorId, IReadOnlyList<ItemMovimentado> Itens,
    long TotalCentavos, DateTimeOffset OcorridoEm) : IIntegrationEvent
{
    public string ChaveIdempotencia => $"compra.estornada:{CompraId}";
}

// ───────────────────────────────────────────────────────────────────────────────
// FISCAL (módulo novo) — catálogo do design em docs/fiscal/arquitetura.md §4/§6.
// Nenhum campo dos eventos ACIMA mudou — tudo aqui é ADITIVO (records novos).
// ───────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Estoque publica sempre que <c>Produto.Fiscal</c> (Ncm/Cest/NaturezaOperacao/CfopOverride)
/// muda — individual. <see cref="NaturezaOperacao"/> atravessa como STRING estável (não ordinal
/// de enum — cada módulo mantém sua PRÓPRIA cópia do enum
/// <c>NaturezaOperacaoProduto</c>/<c>NaturezaOperacaoProdutoExtensions</c>, mesma regra de
/// fronteira de <c>SourceRef</c>): <c>"producao_propria"</c> | <c>"revenda_terceiros"</c> |
/// <c>"importacao_propria"</c>. <see cref="CfopOverride"/> é o override de CFOP por produto
/// decidido por Igor (ADR-0002 do Fiscal) — vence sobre o CFOP padrão configurável, perde para um
/// override explícito na emissão.
/// </summary>
public sealed record ProdutoFiscalAtualizado(
    string ProdutoId, string TenantId, string? Ncm, string? Cest, string NaturezaOperacao,
    string? CfopOverride, DateTimeOffset OcorridoEm) : IIntegrationEvent
{
    public string ChaveIdempotencia => $"produto.fiscal:{ProdutoId}:{Ncm}:{Cest}:{NaturezaOperacao}:{CfopOverride}";
}

/// <summary>Companion do preenchimento em massa de NCM/CEST (Estoque) — um evento por LOTE, não
/// um por produto.</summary>
public sealed record ProdutoFiscalAtualizadoEmLote(
    string TenantId,
    IReadOnlyList<(string ProdutoId, string? Ncm, string? Cest, string NaturezaOperacao, string? CfopOverride)> Itens,
    DateTimeOffset OcorridoEm) : IIntegrationEvent
{
    public string ChaveIdempotencia => $"produto.fiscal.lote:{TenantId}:{OcorridoEm.ToUnixTimeMilliseconds()}";
}

/// <summary>Número fiscal alocado (ainda não transmitido) — auditoria/observabilidade, nunca
/// gatilho de outro fato de negócio.</summary>
public sealed record NumeroFiscalAlocado(
    string DocumentoFiscalId, string TenantId, string Tipo, string Serie, long Numero, DateTimeOffset OcorridoEm) : IIntegrationEvent
{
    public string ChaveIdempotencia => $"fiscal.numero_alocado:{DocumentoFiscalId}";
}

/// <summary>Documento fiscal autorizado pela SEFAZ — notificações (link do PDF/XML), futura
/// Contabilidade/SPED.</summary>
public sealed record DocumentoFiscalAutorizado(
    string DocumentoFiscalId, string TenantId, string Tipo, string ChaveDeAcesso, string Serie,
    long Numero, long TotalCentavos, DateTimeOffset AutorizadoEm, DateTimeOffset OcorridoEm) : IIntegrationEvent
{
    public string ChaveIdempotencia => $"fiscal.autorizado:{DocumentoFiscalId}";
}

/// <summary>Documento fiscal cancelado — nunca dispara reversão financeira sozinho (a
/// <see cref="VendaEstornada"/> já cobre o lado financeiro; este evento é só o lado fiscal do
/// mesmo fato).</summary>
public sealed record DocumentoFiscalCancelado(
    string DocumentoFiscalId, string TenantId, long TotalCentavos, DateTimeOffset OcorridoEm) : IIntegrationEvent
{
    public string ChaveIdempotencia => $"fiscal.cancelado:{DocumentoFiscalId}";
}

/// <summary>Número fiscal alocado que nunca chegou a autorizar — alimenta o job de protocolo de
/// Inutilização de Numeração na SEFAZ dentro do prazo legal.</summary>
public sealed record NumeroFiscalInutilizado(
    string DocumentoFiscalId, string TenantId, string Tipo, string Serie, long Numero, string Motivo, DateTimeOffset OcorridoEm) : IIntegrationEvent
{
    public string ChaveIdempotencia => $"fiscal.numero_inutilizado:{DocumentoFiscalId}";
}

/// <summary>Documento entrou em contingência (NFC-e, <c>tpEmis=9</c>) — XML já assinado
/// localmente, DANFCE já impresso, aguardando rede para transmitir de verdade
/// (docs/fiscal/emissao-mapping.md §6.2, gap #8).</summary>
public sealed record DocumentoFiscalEmContingencia(
    string DocumentoFiscalId, string TenantId, string Tipo, string Serie, long Numero,
    DateTimeOffset DhCont, string Justificativa, DateTimeOffset OcorridoEm) : IIntegrationEvent
{
    public string ChaveIdempotencia => $"fiscal.em_contingencia:{DocumentoFiscalId}";
}

// ───────────────────────────────────────────────────────────────────────────────
// FINANCEIRO COMO CAMADA DE INTELIGÊNCIA (F0) — companion do catálogo do plano em
// docs/financeiro/inteligencia-arquitetura.md §3.3 / ADR-0005. Nenhum evento ACIMA mudou de
// assinatura — este é aditivo.
// ───────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Estoque baixou o custo de UMA venda inteira — soma do custo médio vigente no momento da baixa
/// de cada linha (mesmo quando a ficha técnica expande um produto composto em vários insumos).
/// Publicado pelo Estoque logo após processar <see cref="VendaItensMovimentados"/> (mesmo racional
/// de "um fato, dois eventos" de <see cref="CompraItensRecebidos"/>/<see cref="CompraRecebida"/>).
///
/// POR QUÊ ESTE EVENTO EXISTE: <c>DreGerencialService</c> hoje classifica <see cref="CompraRecebida"/>
/// como custo direto do mês — encher estoque aparece como custo, distorcendo margem (o custo de
/// verdade nasce quando o item SAI, na venda, não quando entra na compra). Este evento é o CMV
/// correto: soma de <c>custoMedio × quantidade</c> de cada linha baixada, no instante da venda.
///
/// GAP DOCUMENTADO (Fase 1 — ver roadmap no plano §6): nenhum handler consome ainda. A F0 só
/// garante que o fato nasce correto e fica no ledger; trocar a fonte do CMV na DRE (F1) é obra de
/// quem mantiver o Financeiro depois.
/// </summary>
public sealed record CustoBaixadoPorVenda(
    string VendaId, string TenantId, long CustoTotalCentavos, DateTimeOffset OcorridoEm) : IIntegrationEvent
{
    public string ChaveIdempotencia => $"venda.custo:{VendaId}";
}

/// <summary>
/// P0-5 (docs/financeiro/revisao-domain-fit-cnpj.md) — o companion de <see cref="CustoBaixadoPorVenda"/>
/// para a corrente Serviço: <c>PecaConsumidaHandler</c> (Estoque) baixa a peça aplicada numa OS pelo
/// custo médio vigente mas, sem este evento, aquele CMV nunca chegava a <c>fato_custo_diario</c> —
/// margem por OS (mão de obra + peça vendida − CMV da peça) ficava incomputável mesmo depois de
/// <see cref="OsFaturada"/> ligar a receita. Publicado POR LINHA (não em lote como
/// <see cref="CustoBaixadoPorVenda"/>) porque <c>PecaConsumidaDomainEvent</c> já nasce por linha, no
/// exato instante de <c>OrdemDeServico.AplicarPeca</c>/<c>AdicionarPecaExtra</c> — mesmo racional de
/// auditoria já documentado nesses eventos.
///
/// <see cref="Estornado"/> só existe para dar ao estorno (<c>ConsumoEstornadoHandler</c>) uma
/// <see cref="ChaveIdempotencia"/> PRÓPRIA, distinta da baixa original — nunca edita/apaga o fato
/// original (mesmo racional de <see cref="VendaEstornada"/>). O SINAL de <see cref="CustoTotalCentavos"/>
/// carrega o efeito: negativo no estorno, positivo na baixa — o fold em <c>fato_custo_diario</c> só
/// soma, sem precisar saber qual dos dois casos gerou o valor.
/// </summary>
public sealed record CustoBaixadoPorOs(
    string OrdemServicoId, string TenantId, string LinhaId, long CustoTotalCentavos, DateTimeOffset OcorridoEm,
    bool Estornado = false) : IIntegrationEvent
{
    public string ChaveIdempotencia => Estornado
        ? $"os.custo.estorno:{OrdemServicoId}:{LinhaId}"
        : $"os.custo:{OrdemServicoId}:{LinhaId}";
}

/// <summary>
/// P0-4 (docs/financeiro/revisao-domain-fit-cnpj.md) — cobrança de assinatura (corrente
/// Recorrente) publicada por <c>GerarCobrancasAssinaturasUseCase</c> logo depois de criar a
/// <c>ContaAReceber</c> da competência (mesma condição de idempotência: só quando a conta É NOVA,
/// nunca em replay). Fecha o gap "RBT12 não inclui assinaturas": sem este evento,
/// <c>fato_receita_diaria</c> — foldada só de <see cref="VendaConcluida"/>/<see cref="PedidoPago"/>/
/// <see cref="OsFaturada"/> — nunca via receita recorrente, e o Radar do Simples subestimava o
/// RBT12 real (e portanto a faixa/alíquota efetiva) de qualquer tenant com assinaturas ativas.
/// </summary>
public sealed record CobrancaDeAssinaturaGerada(
    string AssinaturaId, string TenantId, long ValorCentavos, DateTimeOffset OcorridoEm) : IIntegrationEvent
{
    public string ChaveIdempotencia => $"assinatura.cobranca:{AssinaturaId}:{OcorridoEm:yyyyMM}";
}

/// <summary>
/// P1-3 (docs/financeiro/revisao-domain-fit-cnpj.md) — publicado por <c>BaixarParcelaUseCase</c>
/// logo após liquidar UMA parcela de <c>ContaAPagar</c>/<c>ContaAReceber</c>. Fecha o gap
/// "<c>fato_caixa_diario</c> unilateral": até este evento existir, só entradas à vista
/// (<c>VendaConcluida</c>/<c>PedidoPago</c>) e a reversão de <c>VendaEstornada</c> alimentavam o
/// caixa REALIZADO — toda SAÍDA de pagamento de conta (folha, compras, despesas fixas, comissão) e
/// toda ENTRADA a prazo liquidada depois (ex.: recebível de cartão em D+N) ficavam fora,
/// enviesando bandas de fluxo/burn EWMA/runway para "sem queima de caixa" mesmo com folha alta.
///
/// <see cref="ValorCaixaCentavos"/> é o valor que de fato muda de mãos na liquidação — para uma
/// ENTRADA de recebível com MDR (<c>FormaDePagamento.TaxaPercentual</c> &gt; 0), o publicador já
/// resolve o LÍQUIDO reusando <c>FormaDePagamento.CalcularValorLiquido</c> (o mesmo lar único de
/// MDR que <c>FatoRecebiveisProjection</c> usa — nunca recomputa a taxa numa fonte paralela);
/// para SAÍDA (<c>ContaAPagar</c>, sem MDR) é o valor pago integral. <see cref="EhAPagar"/> segue a
/// mesma convenção de <see cref="ParcelaVencida"/>.
/// </summary>
public sealed record ParcelaBaixada(
    string ContaId, string ParcelaId, string TenantId, bool EhAPagar, long ValorCaixaCentavos, DateTimeOffset OcorridoEm)
    : IIntegrationEvent
{
    public string ChaveIdempotencia => $"parcela.baixada:{ContaId}:{ParcelaId}";
}
