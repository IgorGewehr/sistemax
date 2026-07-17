using SistemaX.Modules.Fiscal.Domain.Comum;
using SistemaX.Modules.Fiscal.Domain.Motor;
using SistemaX.Modules.Fiscal.Domain.Ncm;
using SistemaX.Modules.Fiscal.Domain.Operacoes;
using SistemaX.Modules.Fiscal.Domain.Produtos;
using SistemaX.Modules.Fiscal.Domain.Regimes;
using SistemaX.Modules.Fiscal.Domain.Regras;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Fiscal.Tests;

/// <summary>
/// Cobre a "regra de ouro" do motor (docs/fiscal/arquitetura.md §3): toda ausência de
/// configuração é <c>Result.Falhar</c> nomeado, nunca um CSOSN/CFOP inventado — exatamente o
/// defeito que motivou este design (CSOSN '400' hardcoded no gestao-raiz).
/// </summary>
public class MotorDeCalculoTributarioTests
{
    private static OperacaoFiscal OperacaoInterna() => new(
        TipoOperacaoFiscal.VendaMercadoria, "SP", "SP",
        DestinatarioConsumidorFinal: true, DestinatarioContribuinteIcms: false, OperacaoPresencial: true);

    private static RegraFiscalPorOperacao RegraSimplesNacional() => new(
        TenantId: null, Regime: RegimeTributario.SimplesNacional, TipoOperacao: TipoOperacaoFiscal.VendaMercadoria,
        UfOrigem: "SP", UfDestino: null, IndicadorSt: false,
        SituacaoIcms: SituacaoTributariaIcms.ParaCsosn(RegimeTributario.SimplesNacional, "102").Valor,
        AliquotaInterna: null, AliquotaInterestadual: null);

    private static PerfilFiscalNCM PerfilBasico(RegimeTributario regime) => PerfilFiscalNCM.Criar(
        "tenant-1", regime, "12345678", OrigemMercadoria.Nacional, exigeIcmsSt: false, cest: null,
        aliquotaIpi: null, cstOuCsosnPisCofins: "07", aliquotaPis: null, aliquotaCofins: null).Valor;

    [Fact]
    public void ResolverItem_SemPerfilNemOverride_Falha()
    {
        var input = new ResolverItemInput(
            "produto-1", "Produto 1", "12345678", Quantidade.DeInteiro(1), Money.DeReais(10), Money.Zero,
            RegimeTributario.SimplesNacional, OperacaoInterna(), "5102", Perfil: null, Override: null,
            RegraIcms: null, RegraIcmsDestino: null);

        var resultado = MotorDeCalculoTributario.ResolverItem(input);

        Assert.True(resultado.Falha);
        Assert.Equal("fiscal.ncm.sem_perfil", resultado.Erro.Codigo);
    }

    [Fact]
    public void ResolverItem_ComPerfilMasSemRegraIcms_Falha()
    {
        var input = new ResolverItemInput(
            "produto-1", "Produto 1", "12345678", Quantidade.DeInteiro(1), Money.DeReais(10), Money.Zero,
            RegimeTributario.SimplesNacional, OperacaoInterna(), "5102",
            Perfil: PerfilBasico(RegimeTributario.SimplesNacional), Override: null,
            RegraIcms: null, RegraIcmsDestino: null);

        var resultado = MotorDeCalculoTributario.ResolverItem(input);

        Assert.True(resultado.Falha);
        Assert.Equal("fiscal.regra_operacao.nao_encontrada", resultado.Erro.Codigo);
    }

    [Fact]
    public void ResolverItem_SimplesNacional_NaoDestacaPisCofins()
    {
        var input = new ResolverItemInput(
            "produto-1", "Produto 1", "12345678", Quantidade.DeInteiro(1), Money.DeReais(10), Money.Zero,
            RegimeTributario.SimplesNacional, OperacaoInterna(), "5102",
            Perfil: PerfilBasico(RegimeTributario.SimplesNacional), Override: null,
            RegraIcms: RegraSimplesNacional(), RegraIcmsDestino: null);

        var resultado = MotorDeCalculoTributario.ResolverItem(input);

        Assert.True(resultado.Sucesso);
        Assert.Contains(resultado.Valor.Tributos, t => t.Tipo == Domain.Documentos.TipoTributo.Icms);
        Assert.DoesNotContain(resultado.Valor.Tributos, t => t.Tipo == Domain.Documentos.TipoTributo.Pis);
        Assert.DoesNotContain(resultado.Valor.Tributos, t => t.Tipo == Domain.Documentos.TipoTributo.Cofins);
    }

    [Fact]
    public void ResolverItem_LucroPresumido_DestacaPisCofins()
    {
        var regraNormal = new RegraFiscalPorOperacao(
            null, RegimeTributario.LucroPresumido, TipoOperacaoFiscal.VendaMercadoria, "SP", null, false,
            SituacaoTributariaIcms.ParaCst(RegimeTributario.LucroPresumido, "00").Valor,
            Percentual.DePorcentagem(18), Percentual.DePorcentagem(12));

        var perfil = PerfilFiscalNCM.Criar(
            "tenant-1", RegimeTributario.LucroPresumido, "12345678", OrigemMercadoria.Nacional, false, null,
            null, "01", Percentual.DePorcentagem(0.65m), Percentual.DePorcentagem(3m)).Valor;

        var input = new ResolverItemInput(
            "produto-1", "Produto 1", "12345678", Quantidade.DeInteiro(1), Money.DeReais(100), Money.Zero,
            RegimeTributario.LucroPresumido, OperacaoInterna(), "5102",
            Perfil: perfil, Override: null, RegraIcms: regraNormal, RegraIcmsDestino: null);

        var resultado = MotorDeCalculoTributario.ResolverItem(input);

        Assert.True(resultado.Sucesso);
        Assert.Contains(resultado.Valor.Tributos, t => t.Tipo == Domain.Documentos.TipoTributo.Pis);
        Assert.Contains(resultado.Valor.Tributos, t => t.Tipo == Domain.Documentos.TipoTributo.Cofins);
    }

    [Fact]
    public void ResolverItem_OverrideDeProduto_VenceSobreAMatriz()
    {
        var overrideProduto = TributacaoProduto.Criar(
            "tenant-1", "produto-1", "benefício fiscal individual (programa estadual)",
            situacaoTributariaIcms: "102", aliquotaIcms: Percentual.Zero).Valor;

        var input = new ResolverItemInput(
            "produto-1", "Produto 1", "12345678", Quantidade.DeInteiro(1), Money.DeReais(10), Money.Zero,
            RegimeTributario.SimplesNacional, OperacaoInterna(), "5102",
            Perfil: PerfilBasico(RegimeTributario.SimplesNacional), Override: overrideProduto,
            RegraIcms: null, RegraIcmsDestino: null); // sem RegraIcms — prova que o override dispensa a matriz

        var resultado = MotorDeCalculoTributario.ResolverItem(input);

        Assert.True(resultado.Sucesso);
        var icms = resultado.Valor.Tributos.Single(t => t.Tipo == Domain.Documentos.TipoTributo.Icms);
        Assert.Equal("102", icms.SituacaoTributaria);
        Assert.True(icms.Valor.EhZero);
    }

    [Fact]
    public void ResolverItem_VendaInterestadualConsumidorFinalNaoContribuinte_GeraDifal()
    {
        var operacaoInterestadual = new OperacaoFiscal(
            TipoOperacaoFiscal.VendaMercadoria, "SP", "RJ",
            DestinatarioConsumidorFinal: true, DestinatarioContribuinteIcms: false, OperacaoPresencial: false);

        var regraOrigem = new RegraFiscalPorOperacao(
            null, RegimeTributario.LucroPresumido, TipoOperacaoFiscal.VendaMercadoria, "SP", null, false,
            SituacaoTributariaIcms.ParaCst(RegimeTributario.LucroPresumido, "00").Valor,
            Percentual.DePorcentagem(18), Percentual.DePorcentagem(12));

        var regraDestino = new RegraFiscalPorOperacao(
            null, RegimeTributario.LucroPresumido, TipoOperacaoFiscal.VendaMercadoria, "RJ", "RJ", false,
            SituacaoTributariaIcms.ParaCst(RegimeTributario.LucroPresumido, "00").Valor,
            Percentual.DePorcentagem(20), null, AliquotaFcp: Percentual.DePorcentagem(2));

        var perfil = PerfilBasico(RegimeTributario.LucroPresumido);

        var input = new ResolverItemInput(
            "produto-1", "Produto 1", "12345678", Quantidade.DeInteiro(1), Money.DeReais(100), Money.Zero,
            RegimeTributario.LucroPresumido, operacaoInterestadual, "6102",
            Perfil: perfil, Override: null, RegraIcms: regraOrigem, RegraIcmsDestino: regraDestino);

        var resultado = MotorDeCalculoTributario.ResolverItem(input);

        Assert.True(resultado.Sucesso);
        Assert.Contains(resultado.Valor.Tributos, t => t.Tipo == Domain.Documentos.TipoTributo.IcmsDifal);
        Assert.Contains(resultado.Valor.Tributos, t => t.Tipo == Domain.Documentos.TipoTributo.Fcp);
    }

    [Fact]
    public void ResolverItem_OrigemImportada_ForcaAliquotaInterestadualDe4Porcento()
    {
        var operacaoInterestadual = new OperacaoFiscal(
            TipoOperacaoFiscal.VendaMercadoria, "SP", "RJ",
            DestinatarioConsumidorFinal: false, DestinatarioContribuinteIcms: true, OperacaoPresencial: false);

        var perfilImportado = PerfilFiscalNCM.Criar(
            "tenant-1", RegimeTributario.LucroPresumido, "12345678", OrigemMercadoria.EstrangeiraImportacaoDireta,
            false, null, null, "01", null, null).Valor;

        var regra = new RegraFiscalPorOperacao(
            null, RegimeTributario.LucroPresumido, TipoOperacaoFiscal.VendaMercadoria, "SP", null, false,
            SituacaoTributariaIcms.ParaCst(RegimeTributario.LucroPresumido, "00").Valor,
            Percentual.DePorcentagem(18), Percentual.DePorcentagem(12));

        var input = new ResolverItemInput(
            "produto-1", "Produto 1", "12345678", Quantidade.DeInteiro(1), Money.DeReais(100), Money.Zero,
            RegimeTributario.LucroPresumido, operacaoInterestadual, "6102",
            Perfil: perfilImportado, Override: null, RegraIcms: regra, RegraIcmsDestino: null);

        var resultado = MotorDeCalculoTributario.ResolverItem(input);

        Assert.True(resultado.Sucesso);
        var icms = resultado.Valor.Tributos.Single(t => t.Tipo == Domain.Documentos.TipoTributo.Icms);
        Assert.Equal(Percentual.DePorcentagem(4).Milionesimos, icms.Aliquota.Milionesimos);
    }
}
