using SistemaX.Modules.Fiscal.Domain.Operacoes;
using SistemaX.Modules.Fiscal.Domain.Regras;
using SistemaX.Modules.Fiscal.Domain.Regimes;

namespace SistemaX.Modules.Fiscal.Application.Ports;

/// <summary>
/// Port da matriz de decisão de CSOSN/CST (docs/fiscal/arquitetura.md §2.3). <see cref="ResolverAsync"/>
/// já aplica o desempate por <c>Especificidade</c> (tenant-específica vence default; UfDestino
/// exata vence "qualquer") — o chamador (Motor/Application) nunca decide entre duas linhas
/// candidatas na mão.
/// </summary>
public interface IRegraFiscalPorOperacaoRepository
{
    /// <summary>Resolve a MELHOR linha para a chave — <c>null</c> quando nenhuma linha bate (o
    /// Motor então falha via <c>Result.Falhar</c>, nunca assume um CSOSN default).</summary>
    Task<RegraFiscalPorOperacao?> ResolverAsync(
        string tenantId, RegimeTributario regime, TipoOperacaoFiscal tipoOperacao,
        string ufOrigem, string ufDestino, bool indicadorSt, CancellationToken ct = default);

    Task SalvarAsync(RegraFiscalPorOperacao regra, CancellationToken ct = default);

    Task<IReadOnlyList<RegraFiscalPorOperacao>> ListarAsync(string? tenantId, CancellationToken ct = default);
}
