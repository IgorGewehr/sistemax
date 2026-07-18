using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Financeiro.Application.CasosDeUso;
using SistemaX.Modules.Financeiro.Domain.Assinaturas;
using SistemaX.Modules.Financeiro.Domain.Caixa;
using SistemaX.Modules.Financeiro.Domain.Comum;
using SistemaX.Modules.Financeiro.Domain.ContasAPagarReceber;
using SistemaX.Modules.Financeiro.Domain.Recorrencia;
using SistemaX.Modules.Financeiro.Infrastructure.InMemory;
using SistemaX.Modules.Financeiro.Tests.Fakes;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Tests.Projetos;

/// <summary>
/// Tagging aditivo do design (docs/financeiro/design-analise-por-projeto.md §3.2): <c>ProjetoId</c>
/// nullable em ContaAReceber/ContaAPagar/MovimentoFinanceiro/Assinatura/Recorrencia, propagado
/// (nunca re-inferido) rio abaixo. Invariantes travadas aqui: (1) retrocompat — omitir
/// <c>projetoId</c> continua produzindo exatamente o mesmo <c>null</c> de sempre; (2) herança em
/// estorno; (3) <c>BaixarParcelaUseCase</c> propaga <c>conta.ProjetoId</c> (e <c>conta.Corrente</c>,
/// fechando o gap documentado no design §3.2) para o <c>MovimentoFinanceiro</c> da baixa;
/// (4) <c>Assinatura.GerarCobranca</c> e <c>GerarContasRecorrentesUseCase</c> copiam o projeto do
/// template/assinatura para a conta gerada.
/// </summary>
public sealed class ProjetoTaggingTests
{
    private const string Biz = "loja-1";
    private static readonly DateTimeOffset Agora = new(2026, 7, 17, 12, 0, 0, TimeSpan.FromHours(-3));

    [Fact]
    public void ContaAReceber_Criar_SemProjetoId_NasceComProjetoIdNulo()
    {
        var conta = ContaAReceber.Criar(
            Biz, new SourceRef("teste", "sale-1"), "Venda", "servicos", Agora, Money.DeReais(100),
            ContaFinanceiraBase.ParcelaUnica(Money.DeReais(100), Agora.AddDays(5))).Valor;

        Assert.Null(conta.ProjetoId);
    }

    [Fact]
    public void ContaAPagar_Criar_SemProjetoId_NasceComProjetoIdNulo()
    {
        var conta = ContaAPagar.Criar(
            Biz, new SourceRef("teste", "compra-1"), "Compra", "cmv-fornecedor", Agora, Money.DeReais(100),
            ContaFinanceiraBase.ParcelaUnica(Money.DeReais(100), Agora.AddDays(5))).Valor;

        Assert.Null(conta.ProjetoId);
    }

    [Fact]
    public void MovimentoFinanceiro_GerarEstorno_HerdaProjetoIdEcorrente()
    {
        var origem = new SourceRef("financeiro-baixa", "baixa-1");
        var movimento = MovimentoFinanceiro.Registrar(
            Biz, "conta-caixa-1", "pix", "parcela-1", "conta-1", TipoMovimentoFinanceiro.Entrada,
            Money.DeReais(280), Agora, origem, CorrenteDeReceita.Recorrente, "projeto-digisat").Valor;

        var estorno = movimento.GerarEstorno(Agora.AddDays(1), new SourceRef("estorno", "estorno-1")).Valor;

        Assert.Equal("projeto-digisat", estorno.ProjetoId);
        Assert.Equal(CorrenteDeReceita.Recorrente, estorno.Corrente);
        Assert.Equal(TipoMovimentoFinanceiro.Saida, estorno.Tipo);
    }

    [Fact]
    public async Task BaixarParcelaUseCase_PropagaProjetoIdEcorrenteDaContaParaOMovimento()
    {
        var contasAReceber = new InMemoryContaAReceberRepository();
        var contasAPagar = new InMemoryContaAPagarRepository();
        var movimentos = new InMemoryMovimentoFinanceiroRepository();
        var lancamentos = new InMemoryLancamentoContabilRepository();
        var formasDePagamento = new InMemoryFormaDePagamentoRepository();
        var bus = new FakeIntegrationEventBus();

        var vencimento = Agora.AddDays(5);
        var conta = ContaAReceber.Criar(
            Biz, new SourceRef("assinatura", "assinatura-1:202608"), "Plano DigiSat", "receita-recorrente", Agora,
            Money.DeReais(280), ContaFinanceiraBase.ParcelaUnica(Money.DeReais(280), vencimento),
            corrente: CorrenteDeReceita.Recorrente, projetoId: "projeto-digisat").Valor;
        await contasAReceber.SalvarAsync(conta);

        var useCase = new BaixarParcelaUseCase(contasAReceber, contasAPagar, movimentos, lancamentos, formasDePagamento, bus);
        var comando = new BaixarParcelaComando(conta.Id, conta.Parcelas[0].Id, Money.DeReais(280), vencimento, "conta-caixa-1", "pix", "baixa-digisat-1");

        var resultado = await useCase.BaixarParcelaDeContaAReceberAsync(comando);

        Assert.True(resultado.Sucesso);
        Assert.Equal("projeto-digisat", resultado.Valor.ProjetoId);
        Assert.Equal(CorrenteDeReceita.Recorrente, resultado.Valor.Corrente);
    }

    [Fact]
    public void Assinatura_GerarCobranca_PropagaProjetoIdParaAContaGerada()
    {
        var assinatura = Assinatura.Criar(
            Biz, "cliente-1", "Empresa X", "servico-digisat", "Licença DigiSat", Money.DeReais(280),
            FrequenciaRecorrencia.Mensal, diaCobranca: 10, dataInicio: new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.FromHours(-3)),
            projetoId: "projeto-digisat").Valor;

        // Ciclo mensal: a partir de DataInicio (jun/26), a próxima competência devida é jul/26.
        var competencia = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.FromHours(-3));
        var conta = assinatura.GerarCobranca(competencia, "receita-recorrente").Valor;

        Assert.Equal("projeto-digisat", conta.ProjetoId);
        Assert.Equal(CorrenteDeReceita.Recorrente, conta.Corrente);
    }

    [Fact]
    public void Assinatura_VincularProjeto_PermiteRetagueamento()
    {
        var assinatura = Assinatura.Criar(
            Biz, "cliente-1", "Empresa X", "servico-1", "Plano", Money.DeReais(150),
            FrequenciaRecorrencia.Mensal, diaCobranca: 10, dataInicio: Agora).Valor;
        Assert.Null(assinatura.ProjetoId);

        var vincular = assinatura.VincularProjeto("projeto-aevo", Agora);
        Assert.True(vincular.Sucesso);
        Assert.Equal("projeto-aevo", assinatura.ProjetoId);

        var desvincular = assinatura.VincularProjeto(null, Agora.AddDays(1));
        Assert.True(desvincular.Sucesso);
        Assert.Null(assinatura.ProjetoId);
    }

    [Fact]
    public async Task GerarContasRecorrentesUseCase_PropagaProjetoIdDoTemplateParaAContaGerada()
    {
        var recorrencias = new InMemoryRecorrenciaRepository();
        var contasAPagar = new InMemoryContaAPagarRepository();
        var contasAReceber = new InMemoryContaAReceberRepository();
        var lancamentos = new InMemoryLancamentoContabilRepository();

        // Custo recorrente de IA do caso Aevo (design §3.2) — tagueado UMA vez no template.
        var recorrencia = Recorrencia.Criar(
            Biz, "Custo de IA — Aevo", TipoContaRecorrente.APagar, Money.DeReais(200), "outras-despesas",
            FrequenciaRecorrencia.Mensal, new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.FromHours(-3)),
            projetoId: "projeto-aevo").Valor;
        await recorrencias.SalvarAsync(recorrencia);

        var useCase = new GerarContasRecorrentesUseCase(recorrencias, contasAPagar, contasAReceber, lancamentos);
        var geradas = await useCase.ExecutarAsync(Biz, new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.FromHours(-3)));

        Assert.True(geradas > 0);
        var todas = await contasAPagar.ListarPorCompetenciaAsync(
            Biz, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero));
        Assert.NotEmpty(todas);
        Assert.All(todas, c => Assert.Equal("projeto-aevo", c.ProjetoId));
    }
}
