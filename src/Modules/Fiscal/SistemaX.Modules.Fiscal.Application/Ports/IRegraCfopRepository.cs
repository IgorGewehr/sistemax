using SistemaX.Modules.Fiscal.Domain.Operacoes;
using SistemaX.Modules.Fiscal.Domain.Produtos;
using SistemaX.Modules.Fiscal.Domain.Regras;

namespace SistemaX.Modules.Fiscal.Application.Ports;

/// <summary>
/// Port da camada "padrão-config" do CFOP (decisão de Igor, ADR-0002): configurável, nunca
/// hardcode. <see cref="ResolverAsync"/> aplica o mesmo desempate por especificidade de
/// <see cref="IRegraFiscalPorOperacaoRepository"/> (tenant-específica vence default).
/// </summary>
public interface IRegraCfopRepository
{
    Task<RegraCfop?> ResolverAsync(
        string tenantId, TipoOperacaoFiscal tipoOperacao, bool ehInterestadual,
        bool destinatarioContribuinteIcms, NaturezaOperacaoProduto natureza, CancellationToken ct = default);

    Task SalvarAsync(RegraCfop regra, CancellationToken ct = default);

    Task<IReadOnlyList<RegraCfop>> ListarAsync(string? tenantId, CancellationToken ct = default);
}
