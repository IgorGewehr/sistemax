using SistemaX.Modules.Fiscal.Domain.Comum;
using SistemaX.Modules.Fiscal.Domain.Ncm;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Fiscal.Domain.Produtos;

/// <summary>
/// Override PONTUAL por produto, campo a campo — cada campo nulo herda de <c>PerfilFiscalNCM</c>/
/// <c>RegraFiscalPorOperacao</c>; campo preenchido vence. ProdutoId é o Id (ULID) do <c>Produto</c>
/// do módulo Estoque, referenciado só como STRING — Fiscal.Domain nunca importa um tipo de
/// Estoque.Domain (mesma regra de fronteira que já vale para <c>SourceRef</c> em cada módulo).
/// <see cref="Motivo"/> é OBRIGATÓRIO quando qualquer campo é preenchido — override sem
/// justificativa escrita é o tipo de coisa que 8 meses depois ninguém lembra por que existe.
///
/// <see cref="SituacaoTributariaIcmsOverride"/> (+ alíquota/redução/MVA de ICMS) é a peça que
/// fecha o gap mais grave da auditoria do gestao-raiz: sem ela, um produto com benefício fiscal
/// individual (não compartilhado por todo o NCM/tenant) não tinha como divergir da matriz de
/// <c>RegraFiscalPorOperacao</c> (docs/fiscal/arquitetura.md §2.5).
///
/// NÃO carrega override de CFOP — a decisão de Igor (ADR-0002) é que o override de CFOP por
/// produto vive no CADASTRO DO PRODUTO (Estoque, <c>DadosFiscaisProduto.CfopOverride</c>), não
/// aqui: CFOP correlaciona com um atributo intrínseco do produto (como ele é sourced — produção
/// própria/revenda/importação), então mora onde o produto é editado, chegando ao Fiscal pela
/// MESMA ponte de evento+cache já usada para NCM/CEST (<c>ProdutoFiscalAtualizado</c> →
/// <c>DadosFiscaisProdutoCache</c>), nunca duplicado aqui.
/// </summary>
public sealed record TributacaoProduto(
    string TenantId,
    string ProdutoId,
    OrigemMercadoria? OrigemOverride,
    bool? ExigeIcmsStOverride,
    string? CestOverride,
    string? SituacaoTributariaIcmsOverride,
    Percentual? AliquotaIcmsOverride,
    Percentual? ReducaoBaseCalculoOverride,
    Percentual? MvaOverride,
    Percentual? AliquotaIpiOverride,
    string? CstOuCsosnPisCofinsOverride,
    Percentual? AliquotaPisOverride,
    Percentual? AliquotaCofinsOverride,
    string Motivo,
    DateTimeOffset AtualizadoEm)
{
    public static Result<TributacaoProduto> Criar(
        string tenantId, string produtoId, string motivo,
        OrigemMercadoria? origem = null, bool? exigeIcmsSt = null, string? cest = null,
        string? situacaoTributariaIcms = null, Percentual? aliquotaIcms = null,
        Percentual? reducaoBaseCalculo = null, Percentual? mva = null, Percentual? aliquotaIpi = null,
        string? cstOuCsosnPisCofins = null, Percentual? aliquotaPis = null, Percentual? aliquotaCofins = null)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            return Result.Falhar<TributacaoProduto>(new Error("fiscal.tributacao_produto.tenant_obrigatorio", "TenantId é obrigatório."));

        if (string.IsNullOrWhiteSpace(produtoId))
            return Result.Falhar<TributacaoProduto>(new Error("fiscal.tributacao_produto.produto_obrigatorio", "ProdutoId é obrigatório."));

        var algumCampo = origem is not null || exigeIcmsSt is not null || cest is not null
            || situacaoTributariaIcms is not null || aliquotaIcms is not null || reducaoBaseCalculo is not null
            || mva is not null || aliquotaIpi is not null
            || cstOuCsosnPisCofins is not null || aliquotaPis is not null || aliquotaCofins is not null;

        if (algumCampo && string.IsNullOrWhiteSpace(motivo))
            return Result.Falhar<TributacaoProduto>(new Error(
                "fiscal.tributacao_produto.motivo_obrigatorio", "Override fiscal por produto exige motivo registrado."));

        return Result.Ok(new TributacaoProduto(tenantId, produtoId, origem, exigeIcmsSt, cest,
            situacaoTributariaIcms, aliquotaIcms, reducaoBaseCalculo, mva, aliquotaIpi,
            cstOuCsosnPisCofins, aliquotaPis, aliquotaCofins, motivo, DateTimeOffset.UtcNow));
    }
}
