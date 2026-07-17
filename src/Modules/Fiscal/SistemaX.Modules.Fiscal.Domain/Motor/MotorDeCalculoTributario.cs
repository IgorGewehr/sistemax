using SistemaX.Modules.Fiscal.Domain.Comum;
using SistemaX.Modules.Fiscal.Domain.Documentos;
using SistemaX.Modules.Fiscal.Domain.Ncm;
using SistemaX.Modules.Fiscal.Domain.Operacoes;
using SistemaX.Modules.Fiscal.Domain.Produtos;
using SistemaX.Modules.Fiscal.Domain.Regimes;
using SistemaX.Modules.Fiscal.Domain.Regras;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Fiscal.Domain.Motor;

/// <summary>
/// Entrada já com TUDO buscado pela Application (perfis, overrides, regras resolvidas e
/// desempatadas por especificidade, CFOP já resolvido pela cadeia emissão &gt; produto &gt;
/// padrão-config) — o Motor em si não faz I/O (R2 do CLAUDE.md), só combina dados já em mãos.
/// </summary>
public sealed record ResolverItemInput(
    string ProdutoId,
    string Descricao,
    string Ncm,
    Quantidade Quantidade,
    Money PrecoUnitario,
    Money Desconto,
    RegimeTributario Regime,
    OperacaoFiscal Operacao,
    string Cfop,
    PerfilFiscalNCM? Perfil,
    TributacaoProduto? Override,
    /// <summary>Já resolvida e desempatada pela Application via <c>IRegraFiscalPorOperacaoRepository</c>
    /// (chave: regime/tipoOperação/UfOrigem/UfDestino/indicadorSt) — <c>null</c> quando nenhuma
    /// linha bate (o Motor então falha, nunca assume um CSOSN default).</summary>
    RegraFiscalPorOperacao? RegraIcms,
    /// <summary>Segunda resolução, chaveada pelo <see cref="OperacaoFiscal.UfDestino"/> — só
    /// relevante quando <see cref="OperacaoFiscal.GeraPartilhaDifal"/>; alimenta DIFAL/FCP.</summary>
    RegraFiscalPorOperacao? RegraIcmsDestino);

/// <summary>
/// Único motor de cálculo tributário do sistema — substitui os DOIS motores divergentes do
/// gestao-raiz (um "correto" órfão, outro inline rodando de verdade) por um caminho só, chamado
/// tanto pela emissão real quanto por qualquer preview de UI (docs/fiscal/arquitetura.md §3).
///
/// Regra de ouro: todo passo que não encontra configuração retorna <see cref="Result.Falhar"/>
/// com um código de erro nomeado — NUNCA um valor-padrão mudo tipo <c>CSOSN = '400'</c>.
/// </summary>
public static class MotorDeCalculoTributario
{
    public static Result<ItemDocumentoFiscal> ResolverItem(ResolverItemInput input)
    {
        var origem = input.Override?.OrigemOverride ?? input.Perfil?.Origem;
        if (origem is not { } origemResolvida)
            return Falhar("fiscal.ncm.sem_perfil",
                $"NCM '{input.Ncm}' (produto '{input.ProdutoId}') sem PerfilFiscalNCM/override cadastrado para o regime '{input.Regime}' — configure antes de emitir.");

        var exigeSt = input.Override?.ExigeIcmsStOverride ?? input.Perfil?.ExigeIcmsSt;
        if (exigeSt is null)
            return Falhar("fiscal.ncm.sem_perfil",
                $"NCM '{input.Ncm}' (produto '{input.ProdutoId}') sem indicação de ICMS-ST cadastrada — configure PerfilFiscalNCM antes de emitir.");

        var resolucaoIcms = ResolverSituacaoIcms(input);
        if (resolucaoIcms.Falha) return Result.Falhar<ItemDocumentoFiscal>(resolucaoIcms.Erro);
        var (situacaoIcms, aliquotaInterna, aliquotaInterestadual, reducaoBase, mva) = resolucaoIcms.Valor;

        var aliquotaIcms = origemResolvida.ForcaAliquotaInterestadual4Pct() && input.Operacao.EhInterestadual
            ? Percentual.DePorcentagem(4)
            : (input.Operacao.EhInterestadual ? aliquotaInterestadual : aliquotaInterna) ?? Percentual.Zero;

        var cest = input.Override?.CestOverride ?? input.Perfil?.Cest;
        var subtotal = Money.DeReais(input.PrecoUnitario.EmReais * input.Quantidade.EmDecimal) - input.Desconto;
        var baseIcms = reducaoBase is { } reducao ? Money.DeReais(subtotal.EmReais * (1 - reducao.EmFracao)) : subtotal;
        var valorIcms = aliquotaIcms.AplicarSobre(baseIcms);

        var tributos = new List<TributoResolvidoItem>
        {
            new(TipoTributo.Icms, situacaoIcms.Codigo, baseIcms, aliquotaIcms, valorIcms, reducaoBase, mva)
        };

        if (input.Operacao.GeraPartilhaDifal)
        {
            if (input.RegraIcmsDestino is null)
                return Falhar("fiscal.regra_operacao.nao_encontrada",
                    $"Venda interestadual a consumidor final para UF '{input.Operacao.UfDestino}' sem RegraFiscalPorOperacao para calcular DIFAL/FCP.");

            var aliquotaDestino = input.RegraIcmsDestino.AliquotaInterna ?? Percentual.Zero;
            var diferencaMilionesimos = Math.Max(0, aliquotaDestino.Milionesimos - aliquotaIcms.Milionesimos);
            var aliquotaDifal = new Percentual(diferencaMilionesimos);
            var valorDifal = aliquotaDifal.AplicarSobre(subtotal);
            tributos.Add(new TributoResolvidoItem(TipoTributo.IcmsDifal, null, subtotal, aliquotaDifal, valorDifal));

            if (input.RegraIcmsDestino.AliquotaFcp is { Milionesimos: > 0 } aliquotaFcp)
            {
                var valorFcp = aliquotaFcp.AplicarSobre(subtotal);
                tributos.Add(new TributoResolvidoItem(TipoTributo.Fcp, null, subtotal, aliquotaFcp, valorFcp));
            }
        }

        var aliquotaIpi = input.Override?.AliquotaIpiOverride ?? input.Perfil?.AliquotaIpi;
        if (aliquotaIpi is { Milionesimos: > 0 } ipiPct)
        {
            tributos.Add(new TributoResolvidoItem(TipoTributo.Ipi, null, subtotal, ipiPct, ipiPct.AplicarSobre(subtotal)));
        }

        // Simples Nacional (pleno) NÃO destaca PIS/COFINS por item — está embutido no DAS
        // unificado (correção (1) de docs/fiscal/arquitetura.md §2.1): só Presumido/Real calculam
        // o valor da linha. SimplesNacionalSublimite também não (continua recolhendo
        // PIS/COFINS/IRPJ/CSLL/CPP unificados no DAS — só ICMS/ISS saem por fora).
        if (input.Regime is RegimeTributario.LucroPresumido or RegimeTributario.LucroReal)
        {
            var situacaoPisCofins = input.Override?.CstOuCsosnPisCofinsOverride ?? input.Perfil?.CstOuCsosnPisCofins;
            var aliquotaPis = input.Override?.AliquotaPisOverride ?? input.Perfil?.AliquotaPis;
            var aliquotaCofins = input.Override?.AliquotaCofinsOverride ?? input.Perfil?.AliquotaCofins;

            if (aliquotaPis is { } pisPct)
                tributos.Add(new TributoResolvidoItem(TipoTributo.Pis, situacaoPisCofins, subtotal, pisPct, pisPct.AplicarSobre(subtotal)));

            if (aliquotaCofins is { } cofinsPct)
                tributos.Add(new TributoResolvidoItem(TipoTributo.Cofins, situacaoPisCofins, subtotal, cofinsPct, cofinsPct.AplicarSobre(subtotal)));
        }

        var item = new ItemDocumentoFiscal(
            input.ProdutoId, input.Descricao, input.Ncm, cest, origemResolvida, input.Cfop,
            input.Quantidade, input.PrecoUnitario, input.Desconto, tributos);

        return Result.Ok(item);
    }

    private static Result<(SituacaoTributariaIcms Situacao, Percentual? Interna, Percentual? Interestadual, Percentual? Reducao, Percentual? Mva)>
        ResolverSituacaoIcms(ResolverItemInput input)
    {
        if (input.Override?.SituacaoTributariaIcmsOverride is { } situacaoOverrideCodigo)
        {
            var situacaoResult = input.Regime.UsaCsosn()
                ? SituacaoTributariaIcms.ParaCsosn(input.Regime, situacaoOverrideCodigo)
                : SituacaoTributariaIcms.ParaCst(input.Regime, situacaoOverrideCodigo);

            if (situacaoResult.Falha)
                return Result.Falhar<(SituacaoTributariaIcms, Percentual?, Percentual?, Percentual?, Percentual?)>(situacaoResult.Erro);

            return Result.Ok((situacaoResult.Valor, input.Override.AliquotaIcmsOverride, input.Override.AliquotaIcmsOverride,
                input.Override.ReducaoBaseCalculoOverride, input.Override.MvaOverride));
        }

        if (input.RegraIcms is null)
            return Result.Falhar<(SituacaoTributariaIcms, Percentual?, Percentual?, Percentual?, Percentual?)>(new Error(
                "fiscal.regra_operacao.nao_encontrada",
                $"Nenhuma RegraFiscalPorOperacao para regime '{input.Regime}'/operação '{input.Operacao.Tipo}' (UF {input.Operacao.UfOrigem}→{input.Operacao.UfDestino}) — configure antes de emitir."));

        var regra = input.RegraIcms;
        return Result.Ok((regra.SituacaoIcms, regra.AliquotaInterna, regra.AliquotaInterestadual, regra.ReducaoBaseCalculo, regra.Mva));
    }

    private static Result<ItemDocumentoFiscal> Falhar(string codigo, string mensagem) =>
        Result.Falhar<ItemDocumentoFiscal>(new Error(codigo, mensagem));
}
