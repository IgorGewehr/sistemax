using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Financeiro.Application.CasosDeUso;
using SistemaX.Modules.Financeiro.Domain.Assinaturas;
using SistemaX.Modules.Financeiro.Domain.Recorrencia;
using SistemaX.Modules.Financeiro.Infrastructure.InMemory;
using SistemaX.Modules.Financeiro.Tests.Fakes;
using SistemaX.SharedKernel;
using Xunit;
using RecorrenciaAgg = SistemaX.Modules.Financeiro.Domain.Recorrencia.Recorrencia;

namespace SistemaX.Modules.Financeiro.Tests;

public class GeracaoRecorrenteTests
{
    private const string Biz = "loja-1";

    [Fact]
    public async Task Recorrencia_gera_contas_com_catchup_e_e_idempotente()
    {
        var recRepo = new InMemoryRecorrenciaRepository();
        var apagar = new InMemoryContaAPagarRepository();
        var areceber = new InMemoryContaAReceberRepository();
        var lancamentos = new InMemoryLancamentoContabilRepository();
        var uc = new GerarContasRecorrentesUseCase(recRepo, apagar, areceber, lancamentos);

        var rec = RecorrenciaAgg.Criar(
            Biz, "Aluguel", TipoContaRecorrente.APagar, new Money(250000), "aluguel",
            FrequenciaRecorrencia.Mensal, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), diaFixo: 5).Valor;
        await recRepo.SalvarAsync(rec);

        var ate = new DateTimeOffset(2026, 7, 15, 0, 0, 0, TimeSpan.Zero);

        var geradas = await uc.ExecutarAsync(Biz, ate);
        Assert.Equal(6, geradas);   // fev, mar, abr, mai, jun, jul (dia 5)

        var denovo = await uc.ExecutarAsync(Biz, ate);
        Assert.Equal(0, denovo);    // idempotente — não duplica no re-run
    }

    [Fact]
    public async Task Assinatura_gera_recebivel_do_mes_e_e_idempotente()
    {
        var assinaturas = new InMemoryAssinaturaRepository();
        var areceber = new InMemoryContaAReceberRepository();
        var lancamentos = new InMemoryLancamentoContabilRepository();
        var uc = new GerarCobrancasAssinaturasUseCase(assinaturas, areceber, lancamentos, new FakeIntegrationEventBus());

        var assinatura = Assinatura.Criar(
            Biz, "cli-1", "Mercado São João", "srv-servicepro", "ServicePro", new Money(34900),
            FrequenciaRecorrencia.Mensal, diaCobranca: 5, new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero)).Valor;
        await assinaturas.SalvarAsync(assinatura);

        var competencia = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);

        Assert.Equal(1, await uc.ExecutarAsync(Biz, competencia));
        Assert.Equal(0, await uc.ExecutarAsync(Biz, competencia)); // mesmo mês → idempotente

        var contas = await areceber.ListarPorCompetenciaAsync(Biz, competencia, competencia.AddMonths(1));
        Assert.Single(contas);
        Assert.Equal(34900, contas[0].ValorTotal.Centavos);
    }

    /// <summary>
    /// P0-3 — o bug era exatamente este: um gerador ingênuo chamado todo mês faturaria uma
    /// assinatura ANUAL 12x/ano pelo valor CHEIO. Aqui, rodando o use case uma vez por mês por um
    /// ano inteiro (simulando 12 execuções do cron), uma anual gera SÓ 1 cobrança, uma trimestral
    /// gera 4, e uma mensal gera 12 — cada uma no valor cheio do próprio ciclo (nunca fatiado).
    /// </summary>
    [Theory]
    [InlineData(FrequenciaRecorrencia.Mensal, 12)]
    [InlineData(FrequenciaRecorrencia.Trimestral, 4)]
    [InlineData(FrequenciaRecorrencia.Anual, 1)]
    public async Task Assinatura_com_cron_mensal_ingenuo_fatura_na_cadencia_certa_do_ciclo(FrequenciaRecorrencia ciclo, int esperadasNoAno)
    {
        var assinaturas = new InMemoryAssinaturaRepository();
        var areceber = new InMemoryContaAReceberRepository();
        var lancamentos = new InMemoryLancamentoContabilRepository();
        var uc = new GerarCobrancasAssinaturasUseCase(assinaturas, areceber, lancamentos, new FakeIntegrationEventBus());

        var dataInicio = new DateTimeOffset(2025, 12, 1, 0, 0, 0, TimeSpan.Zero); // 1 ciclo antes da janela
        var assinatura = Assinatura.Criar(
            Biz, "cli-1", "Cliente Anual", "srv-x", "Serviço X", new Money(120000),
            ciclo, diaCobranca: 5, dataInicio).Valor;
        await assinaturas.SalvarAsync(assinatura);

        var geradas = 0;
        for (var mes = 1; mes <= 12; mes++)
        {
            var ateDoMes = new DateTimeOffset(2026, mes, 1, 0, 0, 0, TimeSpan.Zero);
            geradas += await uc.ExecutarAsync(Biz, ateDoMes);
        }

        Assert.Equal(esperadasNoAno, geradas);

        var todasAsContas = await areceber.ListarPorCompetenciaAsync(
            Biz, new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero));
        Assert.All(todasAsContas, c => Assert.Equal(120000, c.ValorTotal.Centavos)); // valor CHEIO, nunca 1/12 do anual
    }

    [Fact]
    public async Task Assinatura_rodar_o_cron_2x_no_mesmo_periodo_nao_duplica()
    {
        var assinaturas = new InMemoryAssinaturaRepository();
        var areceber = new InMemoryContaAReceberRepository();
        var lancamentos = new InMemoryLancamentoContabilRepository();
        var uc = new GerarCobrancasAssinaturasUseCase(assinaturas, areceber, lancamentos, new FakeIntegrationEventBus());

        var assinatura = Assinatura.Criar(
            Biz, "cli-1", "Cliente Trimestral", "srv-y", "Serviço Y", new Money(30000),
            FrequenciaRecorrencia.Trimestral, diaCobranca: 10, new DateTimeOffset(2025, 10, 1, 0, 0, 0, TimeSpan.Zero)).Valor;
        await assinaturas.SalvarAsync(assinatura);

        var ate = new DateTimeOffset(2026, 6, 30, 0, 0, 0, TimeSpan.Zero);

        var primeiraRodada = await uc.ExecutarAsync(Biz, ate); // catch-up: jan/2026 e abr/2026 (2 ciclos vencidos)
        Assert.Equal(2, primeiraRodada);

        var segundaRodada = await uc.ExecutarAsync(Biz, ate); // mesmo "ate" de novo → nada novo devido
        Assert.Equal(0, segundaRodada);

        var contas = await areceber.ListarPorCompetenciaAsync(
            Biz, new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero));
        Assert.Equal(2, contas.Count); // nenhuma duplicata
    }

    /// <summary>
    /// P0-4 (docs/financeiro/revisao-domain-fit-cnpj.md) — "RBT12 deve incluir TODAS as
    /// correntes (hoje não inclui assinaturas)". O gerador de cobrança publica
    /// <see cref="CobrancaDeAssinaturaGerada"/> exatamente 1× por competência NOVA gerada — nunca
    /// em replay/idempotência — para <c>FatoReceitaDiariaProjection</c> foldar a receita recorrente
    /// em <c>fato_receita_diaria</c> (corrente Recorrente), o mesmo caminho que o Radar do Simples
    /// soma no RBT12.
    /// </summary>
    [Fact]
    public async Task Assinatura_gera_cobranca_publica_evento_de_integracao_uma_vez_por_competencia_nova()
    {
        var assinaturas = new InMemoryAssinaturaRepository();
        var areceber = new InMemoryContaAReceberRepository();
        var lancamentos = new InMemoryLancamentoContabilRepository();
        var barramento = new FakeIntegrationEventBus();
        var uc = new GerarCobrancasAssinaturasUseCase(assinaturas, areceber, lancamentos, barramento);

        var assinatura = Assinatura.Criar(
            Biz, "cli-1", "Mercado São João", "srv-servicepro", "ServicePro", new Money(34900),
            FrequenciaRecorrencia.Mensal, diaCobranca: 5, new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero)).Valor;
        await assinaturas.SalvarAsync(assinatura);

        var competencia = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);

        await uc.ExecutarAsync(Biz, competencia);
        var publicados = barramento.Publicados.OfType<CobrancaDeAssinaturaGerada>().ToList();
        Assert.Single(publicados);
        Assert.Equal(assinatura.Id, publicados[0].AssinaturaId);
        Assert.Equal(34900, publicados[0].ValorCentavos);

        // replay do mesmo período (idempotência já testada acima para a ContaAReceber) — o evento
        // de integração NÃO duplica junto.
        await uc.ExecutarAsync(Biz, competencia);
        Assert.Single(barramento.Publicados.OfType<CobrancaDeAssinaturaGerada>());
    }
}
