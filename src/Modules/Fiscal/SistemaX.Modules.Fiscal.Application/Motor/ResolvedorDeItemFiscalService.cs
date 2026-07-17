using SistemaX.Modules.Fiscal.Application.Ports;
using SistemaX.Modules.Fiscal.Domain.Comum;
using SistemaX.Modules.Fiscal.Domain.Documentos;
using SistemaX.Modules.Fiscal.Domain.Motor;
using SistemaX.Modules.Fiscal.Domain.Operacoes;
using SistemaX.Modules.Fiscal.Domain.Regimes;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Fiscal.Application.Motor;

/// <summary>
/// Orquestra o passo 1-3 do fluxo de resolução (docs/fiscal/arquitetura.md §3): busca
/// <c>PerfilFiscalNCM</c>/<c>TributacaoProduto</c>/CFOP resolvido/<c>RegraFiscalPorOperacao</c>
/// (via os ports — isto É I/O, por isso vive em Application, não em <c>Fiscal.Domain</c>) e
/// entrega tudo já resolvido para o <see cref="MotorDeCalculoTributario"/> (função pura). É o
/// ÚNICO caminho de resolução — reusado tanto pela emissão real (<c>VendaItensMovimentadosHandler</c>/
/// <c>EmitirDocumentoFiscalUseCase</c>) quanto por qualquer preview de UI, fechando o defeito dos
/// "dois motores divergentes" do gestao-raiz.
/// </summary>
public sealed class ResolvedorDeItemFiscalService(
    IPerfilFiscalNcmRepository perfis,
    ITributacaoProdutoRepository overridesDeProduto,
    IRegraFiscalPorOperacaoRepository regras,
    IResolvedorDeCfop resolvedorDeCfop)
{
    public async Task<Result<ItemDocumentoFiscal>> ResolverAsync(
        string tenantId, RegimeTributario regime, OperacaoFiscal operacao,
        string produtoId, string descricao, string ncm, Quantidade quantidade,
        Money precoUnitario, Money desconto, string? cfopDaEmissao, CancellationToken ct = default)
    {
        var perfil = await perfis.ObterAsync(tenantId, regime, ncm, ct);
        var overrideProduto = await overridesDeProduto.ObterAsync(tenantId, produtoId, ct);

        var cfopResult = await resolvedorDeCfop.ResolverAsync(tenantId, operacao, produtoId, cfopDaEmissao, ct);
        if (cfopResult.Falha)
            return Result.Falhar<ItemDocumentoFiscal>(cfopResult.Erro);

        // Override de ICMS por produto (§2.5) dispensa a resolução por RegraFiscalPorOperacao —
        // é o escape hatch por-SKU, resolvido ANTES da matriz dentro do próprio Motor. Só
        // buscamos a regra quando não há override, para não gastar uma leitura à toa.
        var indicadorSt = overrideProduto?.ExigeIcmsStOverride ?? perfil?.ExigeIcmsSt ?? false;
        var regraIcms = overrideProduto?.SituacaoTributariaIcmsOverride is not null
            ? null
            : await regras.ResolverAsync(tenantId, regime, operacao.Tipo, operacao.UfOrigem, operacao.UfDestino, indicadorSt, ct);

        // DIFAL/FCP (§2.2/§2.6): segunda resolução chaveada pela alíquota INTERNA do UF de
        // destino — consultada como se fosse uma operação interna naquele UF (UfOrigem =
        // UfDestino = destino), único jeito de reaproveitar a MESMA tabela sem um campo dedicado.
        var regraIcmsDestino = operacao.GeraPartilhaDifal
            ? await regras.ResolverAsync(tenantId, regime, operacao.Tipo, operacao.UfDestino, operacao.UfDestino, false, ct)
            : null;

        var input = new ResolverItemInput(
            produtoId, descricao, ncm, quantidade, precoUnitario, desconto, regime, operacao,
            cfopResult.Valor, perfil, overrideProduto, regraIcms, regraIcmsDestino);

        return MotorDeCalculoTributario.ResolverItem(input);
    }
}
