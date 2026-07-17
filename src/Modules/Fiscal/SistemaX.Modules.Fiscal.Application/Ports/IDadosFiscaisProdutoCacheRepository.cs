using SistemaX.Modules.Fiscal.Domain.Produtos;

namespace SistemaX.Modules.Fiscal.Application.Ports;

/// <summary>
/// Cópia local denormalizada de <c>DadosFiscaisProduto</c> (Estoque) — Ncm/Cest/
/// <see cref="Produtos.NaturezaOperacaoProduto"/>/CfopOverride —, populada pelos handlers de
/// <c>ProdutoFiscalAtualizado</c>/<c>ProdutoFiscalAtualizadoEmLote</c> (eventos de integração).
/// Fiscal NUNCA faz chamada síncrona cross-módulo para ler o produto — lê esta cópia,
/// eventualmente consistente, o que também permite resolver tributação mesmo se o Estoque estiver
/// temporariamente indisponível (docs/fiscal/arquitetura.md §4).
///
/// <see cref="Gtin"/>/<see cref="UnidadeComercial"/> fecham o gap #6 de
/// docs/fiscal/emissao-mapping.md §4.3/§11 — dado CADASTRAL do produto (não tributário, mesma
/// fronteira de Ncm/Cest), nunca lido de <c>PerfilFiscalNCM</c>/<c>TributacaoProduto</c> (§1: o
/// mapper de payload de emissão consulta esta cópia, nunca a matriz de resolução tributária).
/// Nullable — quando ausente, o mapper cai no fallback documentado ("SEM GTIN"/"UN", mesmo default
/// do saas-erp) em vez de inventar um valor.
/// </summary>
public sealed record DadosFiscaisProdutoCache(
    string TenantId,
    string ProdutoId,
    string? Ncm,
    string? Cest,
    NaturezaOperacaoProduto NaturezaOperacao,
    string? CfopOverride,
    string? Gtin = null,
    string? UnidadeComercial = null);

public interface IDadosFiscaisProdutoCacheRepository
{
    Task<DadosFiscaisProdutoCache?> ObterAsync(string tenantId, string produtoId, CancellationToken ct = default);

    Task SalvarAsync(DadosFiscaisProdutoCache dados, CancellationToken ct = default);
}
