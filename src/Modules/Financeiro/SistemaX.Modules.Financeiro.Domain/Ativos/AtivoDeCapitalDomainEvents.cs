using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Domain.Ativos;

/// <summary>Eventos de domínio de <see cref="AtivoDeCapital"/> — privados ao módulo
/// (docs/financeiro/design-analise-por-projeto.md §6.1). O reconhecimento de competência TAMBÉM
/// vira um evento de INTEGRAÇÃO (<c>CustoAmortizadoReconhecido</c>, no catálogo compartilhado —
/// ver <c>Application.Ativos.ReconhecerAmortizacoesUseCase</c>), publicado depois do commit;
/// este aqui é só o rastro interno ao agregado.</summary>
public sealed record AtivoDeCapitalCriado(
    string AtivoId, string BusinessId, string? ProjetoId, NaturezaAtivo Natureza, long CustoAquisicaoCentavos, DateTimeOffset CriadoEm) : DomainEvent;

public sealed record AmortizacaoReconhecida(
    string AtivoId, string BusinessId, string? ProjetoId, string Competencia, long ValorCentavos, DateTimeOffset Quando) : DomainEvent;

public sealed record AtivoDeCapitalEncerrado(string AtivoId, string BusinessId, DateTimeOffset Quando) : DomainEvent;

public sealed record AtivoDeCapitalBaixadoAntecipadamente(
    string AtivoId, string BusinessId, string? ProjetoId, long ValorContabilCentavos, DateTimeOffset Quando) : DomainEvent;

/// <summary>Alienação do bem (fatia I4, docs/financeiro/design-imobilizado-roi.md §4.6) — o
/// contraponto vendido de <see cref="AtivoDeCapitalBaixadoAntecipadamente"/>: mesma mecânica de
/// remover o valor contábil restante do agregado, mas com preço de venda e resultado
/// (ganho/perda) informativos.</summary>
public sealed record AtivoDeCapitalVendido(
    string AtivoId, string BusinessId, string? ProjetoId, long ValorContabilCentavos, long ValorVendaCentavos, DateTimeOffset Quando) : DomainEvent;
