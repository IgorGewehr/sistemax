using SistemaX.Modules.Fiscal.Application.Ports;
using SistemaX.Modules.Fiscal.Domain.Operacoes;
using SistemaX.Modules.Fiscal.Domain.Produtos;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Fiscal.Application.Cfop;

/// <inheritdoc cref="IResolvedorDeCfop"/>
public sealed class ResolvedorDeCfop(IDadosFiscaisProdutoCacheRepository cacheProduto, IRegraCfopRepository regras) : IResolvedorDeCfop
{
    public async Task<Result<string>> ResolverAsync(
        string tenantId, OperacaoFiscal operacao, string produtoId, string? cfopDaEmissao, CancellationToken ct = default)
    {
        // 1) EMISSÃO — override explícito no momento de emitir vence tudo.
        if (!string.IsNullOrWhiteSpace(cfopDaEmissao))
            return Result.Ok(cfopDaEmissao);

        var cache = await cacheProduto.ObterAsync(tenantId, produtoId, ct);

        // 2) PRODUTO — override cadastrado no Estoque (DadosFiscaisProduto.CfopOverride).
        if (!string.IsNullOrWhiteSpace(cache?.CfopOverride))
            return Result.Ok(cache.CfopOverride);

        // 3) PADRÃO-CONFIG — tabela seedável/editável (Settings→Fiscal), nunca hardcode.
        var natureza = cache?.NaturezaOperacao ?? NaturezaOperacaoProduto.RevendaDeTerceiros;
        var regra = await regras.ResolverAsync(
            tenantId, operacao.Tipo, operacao.EhInterestadual, operacao.DestinatarioContribuinteIcms, natureza, ct);

        if (regra is null)
            return Result.Falhar<string>(new Error(
                "fiscal.cfop.nao_encontrado",
                $"Nenhuma RegraCfop configurada para operação '{operacao.Tipo}' (interestadual={operacao.EhInterestadual}, natureza={natureza}) — configure o CFOP padrão em Settings→Fiscal."));

        return Result.Ok(regra.Cfop);
    }
}
