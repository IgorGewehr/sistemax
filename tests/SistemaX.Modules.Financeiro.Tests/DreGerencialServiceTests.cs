using SistemaX.Modules.Financeiro.Application.Analitico;
using SistemaX.Modules.Financeiro.Application.Categorias;
using SistemaX.Modules.Financeiro.Application.ReadModels;
using SistemaX.Modules.Financeiro.Domain.Comum;
using SistemaX.Modules.Financeiro.Domain.ContasAPagarReceber;
using SistemaX.Modules.Financeiro.Infrastructure.InMemory;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Tests;

/// <summary>
/// Regressão do gap "CMV economicamente errado" (docs/financeiro/inteligencia-arquitetura.md §2
/// item 2 / §6 F1 pendência (a)): <c>CompraRecebida</c> gera <c>ContaAPagar</c> categorizada
/// <see cref="CategoriaFinanceiraPadrao.CustoMercadoriaVendida"/> no momento da COMPRA — isso é
/// balanço (troca de caixa/dívida por estoque), nunca resultado do período. O CMV de verdade só
/// nasce quando o produto é VENDIDO (<c>CustoBaixadoPorVenda</c>, foldado em
/// <c>fato_custo_diario</c>). Estes testes travam que o DRE usa a fact table, não a conta a pagar.
/// </summary>
public class DreGerencialServiceTests
{
    private static readonly DateTimeOffset Inicio = new(2026, 8, 1, 0, 0, 0, TimeSpan.FromHours(-3));
    private static readonly DateTimeOffset Fim = new(2026, 8, 31, 23, 59, 59, TimeSpan.FromHours(-3));

    [Fact]
    public async Task CalcularAsync_ComprasDoMesNaoEntramComoCmv_MesmoCategorizadasCmvFornecedor()
    {
        var contasAReceber = new InMemoryContaAReceberRepository();
        var contasAPagar = new InMemoryContaAPagarRepository();
        var fatoCustoDiario = new InMemoryFatoCustoDiarioRepository();
        var fatoRecebiveis = new InMemoryFatoRecebiveisRepository();

        // Encher estoque no mês: ContaAPagar categorizada CMV, mas SEM venda correspondente.
        var dataCompra = new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.FromHours(-3));
        var parcelasCompra = ContaFinanceiraBase.ParcelaUnica(Money.DeReais(10_000), dataCompra.AddDays(30));
        var compra = ContaAPagar.Criar(
            "business-1", new SourceRef("teste", "compra-1"), "Compra de estoque",
            CategoriaFinanceiraPadrao.CustoMercadoriaVendida, dataCompra, Money.DeReais(10_000), parcelasCompra).Valor;
        await contasAPagar.SalvarAsync(compra);

        var service = new DreGerencialService(contasAReceber, contasAPagar, fatoCustoDiario, fatoRecebiveis);
        var resultado = await service.CalcularAsync("business-1", Inicio, Fim);

        Assert.Equal(Money.Zero, resultado.CustoDireto);
        Assert.Equal(Money.Zero, resultado.DespesaOperacional);
        Assert.Equal(Money.Zero, resultado.ResultadoOperacional);
    }

    /// <summary>
    /// P0-1 (docs/financeiro/revisao-domain-fit-cnpj.md) — primeiro corte da dimensão "corrente de
    /// receita": assinatura (categoria <c>ReceitaRecorrente</c>) some separada de venda avulsa no
    /// DRE, e a soma das duas sempre bate com <c>ReceitaBruta</c> (invariante aditiva — nenhum
    /// consumidor existente que só olha os 4 campos originais quebra).
    /// </summary>
    [Fact]
    public async Task CalcularAsync_SeparaReceitaRecorrenteDeOperacionalESomaBateComReceitaBruta()
    {
        var contasAReceber = new InMemoryContaAReceberRepository();
        var contasAPagar = new InMemoryContaAPagarRepository();
        var fatoCustoDiario = new InMemoryFatoCustoDiarioRepository();
        var fatoRecebiveis = new InMemoryFatoRecebiveisRepository();

        var dataVenda = new DateTimeOffset(2026, 8, 15, 12, 0, 0, TimeSpan.FromHours(-3));
        var venda = ContaAReceber.Criar(
            "business-1", new SourceRef("teste", "venda-1"), "Venda", CategoriaFinanceiraPadrao.Servicos,
            dataVenda, Money.DeReais(500), ContaFinanceiraBase.ParcelaUnica(Money.DeReais(500), dataVenda)).Valor;
        await contasAReceber.SalvarAsync(venda);

        var dataAssinatura = new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.FromHours(-3));
        var assinatura = ContaAReceber.Criar(
            "business-1", new SourceRef("assinatura", "as1:202608"), "Plano Mensal", CategoriaFinanceiraPadrao.ReceitaRecorrente,
            dataAssinatura, Money.DeReais(150), ContaFinanceiraBase.ParcelaUnica(Money.DeReais(150), dataAssinatura)).Valor;
        await contasAReceber.SalvarAsync(assinatura);

        var service = new DreGerencialService(contasAReceber, contasAPagar, fatoCustoDiario, fatoRecebiveis);
        var resultado = await service.CalcularAsync("business-1", Inicio, Fim);

        Assert.Equal(Money.DeReais(650), resultado.ReceitaBruta);
        Assert.Equal(Money.DeReais(150), resultado.ReceitaRecorrente);
        Assert.Equal(Money.DeReais(500), resultado.ReceitaOperacional);
        Assert.Equal(resultado.ReceitaBruta, resultado.ReceitaRecorrente + resultado.ReceitaOperacional);
    }

    [Fact]
    public async Task CalcularAsync_CmvVemDoFatoCustoDiarioRealizadoPorVenda_NaoDaComprasDoPeriodo()
    {
        var contasAReceber = new InMemoryContaAReceberRepository();
        var contasAPagar = new InMemoryContaAPagarRepository();
        var fatoCustoDiario = new InMemoryFatoCustoDiarioRepository();
        var fatoRecebiveis = new InMemoryFatoRecebiveisRepository();

        // Venda concluída no mês, receita reconhecida.
        var dataVenda = new DateTimeOffset(2026, 8, 15, 12, 0, 0, TimeSpan.FromHours(-3));
        var parcelasVenda = ContaFinanceiraBase.ParcelaUnica(Money.DeReais(500), dataVenda);
        var venda = ContaAReceber.Criar(
            "business-1", new SourceRef("teste", "venda-1"), "Venda", CategoriaFinanceiraPadrao.Servicos,
            dataVenda, Money.DeReais(500), parcelasVenda).Valor;
        await contasAReceber.SalvarAsync(venda);

        // CMV real dessa venda, já foldado no fato_custo_diario (CustoBaixadoPorVenda).
        await fatoCustoDiario.AcumularAsync("business-1", new DateOnly(2026, 8, 15), CorrenteDeReceita.Comercio, Money.DeReais(300).Centavos);

        // Compra de estoque no MESMO mês (nada a ver com a venda acima) — não pode contaminar o CMV.
        var dataCompra = new DateTimeOffset(2026, 8, 20, 12, 0, 0, TimeSpan.FromHours(-3));
        var parcelasCompra = ContaFinanceiraBase.ParcelaUnica(Money.DeReais(50_000), dataCompra.AddDays(30));
        var compra = ContaAPagar.Criar(
            "business-1", new SourceRef("teste", "compra-2"), "Reposição grande de estoque",
            CategoriaFinanceiraPadrao.CustoMercadoriaVendida, dataCompra, Money.DeReais(50_000), parcelasCompra).Valor;
        await contasAPagar.SalvarAsync(compra);

        var service = new DreGerencialService(contasAReceber, contasAPagar, fatoCustoDiario, fatoRecebiveis);
        var resultado = await service.CalcularAsync("business-1", Inicio, Fim);

        Assert.Equal(Money.DeReais(500), resultado.ReceitaBruta);
        Assert.Equal(Money.DeReais(300), resultado.CustoDireto);
        Assert.Equal(Money.DeReais(200), resultado.ResultadoOperacional);
    }

    [Fact]
    public async Task CalcularAsync_ComissoesContinuamClassificadasComoCustoDiretoViaContaAPagar()
    {
        var contasAReceber = new InMemoryContaAReceberRepository();
        var contasAPagar = new InMemoryContaAPagarRepository();
        var fatoCustoDiario = new InMemoryFatoCustoDiarioRepository();
        var fatoRecebiveis = new InMemoryFatoRecebiveisRepository();

        var data = new DateTimeOffset(2026, 8, 5, 12, 0, 0, TimeSpan.FromHours(-3));
        var parcelas = ContaFinanceiraBase.ParcelaUnica(Money.DeReais(80), data);
        var comissao = ContaAPagar.Criar(
            "business-1", new SourceRef("teste", "comissao-1"), "Comissão do vendedor",
            CategoriaFinanceiraPadrao.Comissoes, data, Money.DeReais(80), parcelas).Valor;
        await contasAPagar.SalvarAsync(comissao);

        var service = new DreGerencialService(contasAReceber, contasAPagar, fatoCustoDiario, fatoRecebiveis);
        var resultado = await service.CalcularAsync("business-1", Inicio, Fim);

        Assert.Equal(Money.DeReais(80), resultado.CustoDireto);
        Assert.Equal(Money.Zero, resultado.DespesaOperacional);
    }

    [Fact]
    public async Task CalcularAsync_DespesaOperacionalIgnoraCategoriasDeCustoDireto()
    {
        var contasAReceber = new InMemoryContaAReceberRepository();
        var contasAPagar = new InMemoryContaAPagarRepository();
        var fatoCustoDiario = new InMemoryFatoCustoDiarioRepository();
        var fatoRecebiveis = new InMemoryFatoRecebiveisRepository();

        var data = new DateTimeOffset(2026, 8, 5, 12, 0, 0, TimeSpan.FromHours(-3));
        var parcelas = ContaFinanceiraBase.ParcelaUnica(Money.DeReais(120), data);
        var aluguel = ContaAPagar.Criar(
            "business-1", new SourceRef("teste", "aluguel-1"), "Aluguel",
            CategoriaFinanceiraPadrao.DespesaComPessoal, data, Money.DeReais(120), parcelas).Valor;
        await contasAPagar.SalvarAsync(aluguel);

        var service = new DreGerencialService(contasAReceber, contasAPagar, fatoCustoDiario, fatoRecebiveis);
        var resultado = await service.CalcularAsync("business-1", Inicio, Fim);

        Assert.Equal(Money.Zero, resultado.CustoDireto);
        Assert.Equal(Money.DeReais(120), resultado.DespesaOperacional);
    }

    /// <summary>
    /// P0-1 completo (docs/financeiro/revisao-domain-fit-cnpj.md) — DRE com receita nas 3
    /// correntes (Recorrente/Servico/Comercio) reparte certo: cada corrente devolve sua própria
    /// receita bruta, custo direto e margem, a soma das 3 bate com o total, e a margem de cada
    /// corrente reflete a economia unitária esperada (recorrente ~100%, serviço alta com comissão,
    /// comércio mais fina com CMV real).
    /// </summary>
    [Fact]
    public async Task CalcularAsync_ComReceitaNasTresCorrentes_RepartaReceitaCustoEMargemCorretamente()
    {
        var contasAReceber = new InMemoryContaAReceberRepository();
        var contasAPagar = new InMemoryContaAPagarRepository();
        var fatoCustoDiario = new InMemoryFatoCustoDiarioRepository();
        var fatoRecebiveis = new InMemoryFatoRecebiveisRepository();

        // Recorrente: assinatura de R$150, sem CMV, sem comissão — MC ~100%.
        var dataAssinatura = new DateTimeOffset(2026, 8, 5, 12, 0, 0, TimeSpan.FromHours(-3));
        var assinatura = ContaAReceber.Criar(
            "business-1", new SourceRef("assinatura", "as1:202608"), "Plano Mensal", CategoriaFinanceiraPadrao.ReceitaRecorrente,
            dataAssinatura, Money.DeReais(150), ContaFinanceiraBase.ParcelaUnica(Money.DeReais(150), dataAssinatura),
            corrente: CorrenteDeReceita.Recorrente).Valor;
        await contasAReceber.SalvarAsync(assinatura);

        // Servico: OS de R$400 (mão de obra + peça aplicada), com comissão de R$40 do técnico.
        var dataOs = new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.FromHours(-3));
        var os = ContaAReceber.Criar(
            "business-1", new SourceRef("appointment", "os-1"), "OS-0001", CategoriaFinanceiraPadrao.Servicos,
            dataOs, Money.DeReais(400), ContaFinanceiraBase.ParcelaUnica(Money.DeReais(400), dataOs),
            corrente: CorrenteDeReceita.Servico).Valor;
        await contasAReceber.SalvarAsync(os);

        var comissaoTecnico = ContaAPagar.Criar(
            "business-1", new SourceRef("teste", "comissao-os-1"), "Comissão técnico OS-0001",
            CategoriaFinanceiraPadrao.Comissoes, dataOs, Money.DeReais(40), ContaFinanceiraBase.ParcelaUnica(Money.DeReais(40), dataOs),
            corrente: CorrenteDeReceita.Servico).Valor;
        await contasAPagar.SalvarAsync(comissaoTecnico);

        // Comercio: venda de peça avulsa de R$500, com CMV real de R$300 já foldado.
        var dataVenda = new DateTimeOffset(2026, 8, 15, 12, 0, 0, TimeSpan.FromHours(-3));
        var venda = ContaAReceber.Criar(
            "business-1", new SourceRef("sale", "venda-1"), "Venda", CategoriaFinanceiraPadrao.Servicos,
            dataVenda, Money.DeReais(500), ContaFinanceiraBase.ParcelaUnica(Money.DeReais(500), dataVenda),
            corrente: CorrenteDeReceita.Comercio).Valor;
        await contasAReceber.SalvarAsync(venda);
        await fatoCustoDiario.AcumularAsync("business-1", new DateOnly(2026, 8, 15), CorrenteDeReceita.Comercio, Money.DeReais(300).Centavos);

        var service = new DreGerencialService(contasAReceber, contasAPagar, fatoCustoDiario, fatoRecebiveis);
        var resultado = await service.CalcularAsync("business-1", Inicio, Fim);

        Assert.Equal(3, resultado.PorCorrente.Count);

        var recorrente = resultado.PorCorrente.Single(p => p.Corrente == CorrenteDeReceita.Recorrente);
        Assert.Equal(Money.DeReais(150), recorrente.ReceitaBruta);
        Assert.Equal(Money.Zero, recorrente.CustoDireto);
        Assert.Equal(Money.DeReais(150), recorrente.Margem);

        var servico = resultado.PorCorrente.Single(p => p.Corrente == CorrenteDeReceita.Servico);
        Assert.Equal(Money.DeReais(400), servico.ReceitaBruta);
        Assert.Equal(Money.DeReais(40), servico.CustoDireto);
        Assert.Equal(Money.DeReais(360), servico.Margem);

        var comercio = resultado.PorCorrente.Single(p => p.Corrente == CorrenteDeReceita.Comercio);
        Assert.Equal(Money.DeReais(500), comercio.ReceitaBruta);
        Assert.Equal(Money.DeReais(300), comercio.CustoDireto);
        Assert.Equal(Money.DeReais(200), comercio.Margem);

        // Invariante: soma das correntes bate com o total do DRE (toda receita/custo está tagueada).
        var somaReceitaPorCorrente = resultado.PorCorrente.Aggregate(Money.Zero, (acc, p) => acc + p.ReceitaBruta);
        var somaCustoPorCorrente = resultado.PorCorrente.Aggregate(Money.Zero, (acc, p) => acc + p.CustoDireto);
        Assert.Equal(resultado.ReceitaBruta, somaReceitaPorCorrente);
        Assert.Equal(resultado.CustoDireto, somaCustoPorCorrente);
    }

    /// <summary>
    /// P1-6 (docs/financeiro/revisao-domain-fit-cnpj.md) — MDR de uma venda no cartão vira despesa
    /// financeira no DRE, derivada AO VIVO de <c>fato_recebiveis</c> (nunca recomputada em paralelo:
    /// a diferença bruto−líquido já foi calculada por <c>FatoRecebiveisProjection</c> contra o lar
    /// único <c>FormaDePagamento</c>). Reduz <c>ResultadoOperacional</c> sem contaminar <c>CustoDireto</c>.
    /// </summary>
    [Fact]
    public async Task CalcularAsync_VendaNoCartaoComMdrEmFatoRecebiveis_GeraDespesaFinanceiraEReduzResultado()
    {
        var contasAReceber = new InMemoryContaAReceberRepository();
        var contasAPagar = new InMemoryContaAPagarRepository();
        var fatoCustoDiario = new InMemoryFatoCustoDiarioRepository();
        var fatoRecebiveis = new InMemoryFatoRecebiveisRepository();

        var dataVenda = new DateTimeOffset(2026, 8, 15, 12, 0, 0, TimeSpan.FromHours(-3));
        var venda = ContaAReceber.Criar(
            "business-1", new SourceRef("sale", "venda-cartao-1"), "Venda no cartão", CategoriaFinanceiraPadrao.Servicos,
            dataVenda, Money.DeReais(1_000), ContaFinanceiraBase.ParcelaUnica(Money.DeReais(1_000), dataVenda)).Valor;
        await contasAReceber.SalvarAsync(venda);

        // fato_recebiveis já resolveu o MDR (3,49%) — o DRE só soma bruto-líquido, nunca recalcula.
        var diaVenda = new DateOnly(2026, 8, 15);
        await fatoRecebiveis.AdicionarAsync(new FatoRecebivel(
            "business-1", "sale:venda-cartao-1", diaVenda, diaVenda.AddDays(30), "cartao_credito", 0.0349m,
            100_000, 96_510, DateTimeOffset.UtcNow));

        var service = new DreGerencialService(contasAReceber, contasAPagar, fatoCustoDiario, fatoRecebiveis);
        var resultado = await service.CalcularAsync("business-1", Inicio, Fim);

        Assert.Equal(Money.DeReais(1_000), resultado.ReceitaBruta);
        Assert.Equal(new Money(3_490), resultado.DespesaFinanceira); // 1.000 - 965,10 = 34,90
        Assert.Equal(Money.DeReais(1_000) - new Money(3_490), resultado.ResultadoOperacional);
    }

    /// <summary>Dinheiro/PIX não tem MDR — nenhum recebível sem taxa gera despesa financeira.</summary>
    [Fact]
    public async Task CalcularAsync_VendaEmDinheiroSemMdr_NaoGeraDespesaFinanceira()
    {
        var contasAReceber = new InMemoryContaAReceberRepository();
        var contasAPagar = new InMemoryContaAPagarRepository();
        var fatoCustoDiario = new InMemoryFatoCustoDiarioRepository();
        var fatoRecebiveis = new InMemoryFatoRecebiveisRepository();

        var dataVenda = new DateTimeOffset(2026, 8, 15, 12, 0, 0, TimeSpan.FromHours(-3));
        var venda = ContaAReceber.Criar(
            "business-1", new SourceRef("sale", "venda-dinheiro-1"), "Venda em dinheiro", CategoriaFinanceiraPadrao.Servicos,
            dataVenda, Money.DeReais(500), ContaFinanceiraBase.ParcelaUnica(Money.DeReais(500), dataVenda)).Valor;
        await contasAReceber.SalvarAsync(venda);

        var diaVenda = new DateOnly(2026, 8, 15);
        await fatoRecebiveis.AdicionarAsync(new FatoRecebivel(
            "business-1", "sale:venda-dinheiro-1", diaVenda, diaVenda, "dinheiro", 0m, 50_000, 50_000, DateTimeOffset.UtcNow));

        var service = new DreGerencialService(contasAReceber, contasAPagar, fatoCustoDiario, fatoRecebiveis);
        var resultado = await service.CalcularAsync("business-1", Inicio, Fim);

        Assert.Equal(Money.Zero, resultado.DespesaFinanceira);
        Assert.Equal(Money.DeReais(500), resultado.ResultadoOperacional);
    }

    /// <summary>
    /// P1-5 (docs/financeiro/revisao-domain-fit-cnpj.md) — cobrança ANUAL de assinatura: o
    /// RECEBÍVEL (<c>ContaAReceber</c>) nasce íntegro no valor cheio na competência da cobrança
    /// (é o CAIXA — nunca fracionado), mas o DRE reconhece só 1/12 em cada uma das 12 competências
    /// seguintes (CronogramaLinear/Hamilton). Somando as 12 competências, a receita reconhecida
    /// total bate exatamente com o valor cheio da cobrança (conservação de centavos).
    /// </summary>
    [Fact]
    public async Task CalcularAsync_CobrancaAnualDeAssinatura_ReconheceUmDozeAvosPorMesE12Competencias()
    {
        var contasAReceber = new InMemoryContaAReceberRepository();
        var contasAPagar = new InMemoryContaAPagarRepository();
        var fatoCustoDiario = new InMemoryFatoCustoDiarioRepository();
        var fatoRecebiveis = new InMemoryFatoRecebiveisRepository();

        // Cobrança anual de R$1.200,00, competência de agosto/2026 — o "caixa" nasce aqui, íntegro.
        var dataCobranca = new DateTimeOffset(2026, 8, 5, 12, 0, 0, TimeSpan.FromHours(-3));
        var cobranca = ContaAReceber.Criar(
            "business-1", new SourceRef("assinatura", "as1:202608"), "Plano Anual", CategoriaFinanceiraPadrao.ReceitaRecorrente,
            dataCobranca, Money.DeReais(1_200), ContaFinanceiraBase.ParcelaUnica(Money.DeReais(1_200), dataCobranca),
            corrente: CorrenteDeReceita.Recorrente, mesesDeReconhecimento: 12).Valor;
        await contasAReceber.SalvarAsync(cobranca);

        // O CAIXA (o recebível) continua íntegro no valor cheio, na data da cobrança — nunca fracionado.
        Assert.Equal(Money.DeReais(1_200), cobranca.ValorTotal);
        Assert.Equal(dataCobranca, cobranca.DataCompetencia);

        var service = new DreGerencialService(contasAReceber, contasAPagar, fatoCustoDiario, fatoRecebiveis);

        long totalReconhecido = 0;
        for (var i = 0; i < 12; i++)
        {
            var mes = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.FromHours(-3)).AddMonths(i);
            var fimDoMes = mes.AddMonths(1).AddSeconds(-1);

            var resultado = await service.CalcularAsync("business-1", mes, fimDoMes);

            // 1.200,00 ÷ 12 = 100,00 exato em cada mês (Hamilton sem resto aqui).
            Assert.Equal(Money.DeReais(100), resultado.ReceitaBruta);
            Assert.Equal(Money.DeReais(100), resultado.ReceitaRecorrente);
            totalReconhecido += resultado.ReceitaBruta.Centavos;
        }

        Assert.Equal(120_000, totalReconhecido); // Σ das 12 competências = o valor cheio da cobrança

        // Fora da janela de 12 meses (mês 13), nada mais é reconhecido — o cronograma acabou.
        var mesFora = new DateTimeOffset(2027, 8, 1, 0, 0, 0, TimeSpan.FromHours(-3));
        var resultadoFora = await service.CalcularAsync("business-1", mesFora, mesFora.AddMonths(1).AddSeconds(-1));
        Assert.Equal(Money.Zero, resultadoFora.ReceitaBruta);
    }
}
