using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Application.ReadModels;
using SistemaX.Modules.Financeiro.Domain.Assinaturas;
using SistemaX.Modules.Financeiro.Domain.Recorrencia;
using SistemaX.SharedKernel;
using Xunit;

namespace SistemaX.Modules.Financeiro.Tests;

public class AssinaturaTests
{
    private const string Biz = "loja-1";
    private static readonly DateTimeOffset Ref = new(2026, 7, 15, 0, 0, 0, TimeSpan.Zero);

    private static Assinatura Nova(Money valor, FrequenciaRecorrencia ciclo, DateTimeOffset inicio, string servico = "srv-a", string servicoNome = "Serviço A", string cliente = "cli-1")
        => Assinatura.Criar(Biz, cliente, "Cliente", servico, servicoNome, valor, ciclo, diaCobranca: 5, inicio).Valor;

    [Theory]
    [InlineData(50000, FrequenciaRecorrencia.Mensal, 50000)]      // R$500/mês  -> MRR 500
    [InlineData(120000, FrequenciaRecorrencia.Anual, 10000)]      // R$1200/ano -> MRR 100
    [InlineData(30000, FrequenciaRecorrencia.Trimestral, 10000)]  // R$300/tri  -> MRR 100
    [InlineData(60000, FrequenciaRecorrencia.Semestral, 10000)]   // R$600/sem  -> MRR 100
    public void Mrr_normaliza_qualquer_ciclo_para_mensal(long valorCentavos, FrequenciaRecorrencia ciclo, long mrrEsperado)
    {
        var a = Nova(new Money(valorCentavos), ciclo, Ref);
        Assert.Equal(mrrEsperado, a.Mrr.Centavos);
    }

    [Fact]
    public void Cancelar_marca_churn_com_data_motivo_e_evento()
    {
        var a = Nova(new Money(50000), FrequenciaRecorrencia.Mensal, Ref);
        a.ClearDomainEvents();

        var r = a.Cancelar("cliente fechou as portas", Ref);

        Assert.True(r.Sucesso);
        Assert.Equal(StatusAssinatura.Cancelada, a.Status);
        Assert.Equal(Ref, a.CanceladaEm);
        Assert.Contains(a.DomainEvents, e => e is AssinaturaCancelada);
        Assert.True(a.Cancelar("de novo", Ref).Falha); // idempotência de FSM: não cancela 2x
    }

    [Fact]
    public void Fsm_pausar_reativar_respeita_transicoes()
    {
        var a = Nova(new Money(20000), FrequenciaRecorrencia.Mensal, Ref);

        Assert.True(a.Pausar().Sucesso);
        Assert.Equal(StatusAssinatura.Pausada, a.Status);
        Assert.True(a.Pausar().Falha);          // já pausada
        Assert.True(a.Reativar().Sucesso);
        Assert.Equal(StatusAssinatura.Ativa, a.Status);
        Assert.True(a.Reativar().Falha);         // já ativa
    }

    /// <summary>
    /// Idempotência de <c>SourceRef</c> por competência — testada sobre DUAS instâncias
    /// independentes reidratadas com o MESMO estado (o cenário real: cada rodada do cron recarrega
    /// a assinatura do repositório do zero; a Application decide não persistir de novo checando
    /// <c>BuscarPorOrigemAsync</c>). Não reusa a mesma instância mutada entre as duas chamadas —
    /// depois de gerar com sucesso, <c>UltimaCobrancaGeradaEm</c> avança e a PRÓXIMA competência
    /// devida também avança (P0-3): reusar o objeto faria a segunda chamada falhar por "ciclo ainda
    /// não vencido", o que é correto, mas não é o que este teste quer provar.
    /// </summary>
    [Fact]
    public void GerarCobranca_e_idempotente_por_periodo()
    {
        Assinatura Fresh() => Assinatura.Reconstituir(
            "assinatura-fixa", Biz, "cli-1", "Cliente", "srv-a", "Serviço A",
            new Money(50000), FrequenciaRecorrencia.Mensal, diaCobranca: 5, StatusAssinatura.Ativa,
            dataInicio: Ref, canceladaEm: null, motivoCancelamento: null, ultimaCobrancaGeradaEm: null);

        var devida = Fresh().ProximaCompetenciaDevida; // primeira competência devida (Ref + 1 mês)

        var c1 = Fresh().GerarCobranca(devida, "servicos");
        var c2 = Fresh().GerarCobranca(devida, "servicos");

        Assert.True(c1.Sucesso);
        Assert.True(c2.Sucesso);
        // mesma competência -> mesma SourceRef determinística (o repositório deduplica na gravação)
        Assert.Equal(c1.Valor.SourceRef.Chave, c2.Valor.SourceRef.Chave);
    }

    [Fact]
    public void GerarCobranca_recusa_competencia_cujo_ciclo_ainda_nao_venceu()
    {
        // Assinatura ANUAL: a competência do mês seguinte ao início não é devida — era o bug do
        // P0-3 (um cron mensal ingênuo faturaria 12x/ano o valor cheio de um plano anual).
        var a = Nova(new Money(120000), FrequenciaRecorrencia.Anual, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

        var resultado = a.GerarCobranca(new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero), "servicos");

        Assert.True(resultado.Falha);
        Assert.Equal("financeiro.assinatura.ciclo_nao_vencido", resultado.Erro.Codigo);
    }

    [Theory]
    [InlineData(FrequenciaRecorrencia.Mensal, 12)]
    [InlineData(FrequenciaRecorrencia.Trimestral, 4)]
    [InlineData(FrequenciaRecorrencia.Semestral, 2)]
    [InlineData(FrequenciaRecorrencia.Anual, 1)]
    public void GerarCobranca_com_catchup_gera_a_cadencia_certa_por_ano(FrequenciaRecorrencia ciclo, int esperadasNoAno)
    {
        // DataInicio alinhada um ciclo ANTES da janela de um ano — estado estacionário: nenhum
        // "primeiro ano parcial" distorce a contagem (mensal=12/trimestral=4/semestral=2/anual=1).
        var dataInicio = new DateTimeOffset(2025, 12, 1, 0, 0, 0, TimeSpan.Zero);
        var ate = new DateTimeOffset(2026, 12, 31, 0, 0, 0, TimeSpan.Zero);
        var a = Nova(new Money(120000), ciclo, dataInicio);

        var geradas = 0;
        while (true)
        {
            var competencia = a.ProximaCompetenciaDevida;
            if (competencia > new DateTimeOffset(ate.Year, ate.Month, 1, 0, 0, 0, ate.Offset)) break;

            var conta = a.GerarCobranca(competencia, "servicos");
            Assert.True(conta.Sucesso);
            Assert.Equal(120000, conta.Valor.ValorTotal.Centavos); // valor CHEIO do ciclo, nunca fracionado
            geradas++;
        }

        Assert.Equal(esperadasNoAno, geradas);
    }

    [Fact]
    public async Task ReceitaRecorrenteService_calcula_mrr_churn_e_concentracao()
    {
        var ativaGrande = Nova(new Money(50000), FrequenciaRecorrencia.Mensal, Ref.AddMonths(-1), "srv-a", "Serviço A"); // MRR 500, mês passado
        var novaNoMes = Nova(new Money(120000), FrequenciaRecorrencia.Anual, new DateTimeOffset(2026, 7, 5, 0, 0, 0, TimeSpan.Zero), "srv-b", "Serviço B"); // MRR 100, novo no mês
        var churnada = Nova(new Money(30000), FrequenciaRecorrencia.Mensal, Ref.AddMonths(-2), "srv-a", "Serviço A"); // MRR 300
        churnada.Cancelar("saiu", new DateTimeOffset(2026, 7, 10, 0, 0, 0, TimeSpan.Zero)); // churn neste mês

        var repo = new FakeAssinaturaRepository(ativaGrande, novaNoMes, churnada);
        var service = new ReceitaRecorrenteService(repo);

        var r = await service.CalcularAsync(Biz, Ref);

        Assert.Equal(60000, r.Mrr.Centavos);               // 500 + 100 (só ativas)
        Assert.Equal(720000, r.Arr.Centavos);              // MRR x 12
        Assert.Equal(2, r.AssinaturasAtivas);
        Assert.Equal(10000, r.MrrNovoNoMes.Centavos);      // Serviço B, iniciada no mês
        Assert.Equal(30000, r.MrrChurnNoMes.Centavos);     // a churnada
        Assert.Equal(1, r.ClientesChurnNoMes);
        Assert.Equal(37.5m, r.ChurnPercent);               // 300 / (600 - 100 + 300)
        Assert.Equal("srv-a", r.MaiorConcentracao!.ServicoId); // Serviço A = 500 de 600 = maior
        Assert.Equal(83.3m, r.MaiorConcentracao.Percentual);
    }

    private sealed class FakeAssinaturaRepository(params Assinatura[] itens) : IAssinaturaRepository
    {
        private readonly List<Assinatura> _itens = [.. itens];
        public Task<IReadOnlyList<Assinatura>> ListarAsync(string businessId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Assinatura>>(_itens.Where(a => a.BusinessId == businessId).ToList());
        public Task<IReadOnlyList<Assinatura>> ListarAtivasAsync(string businessId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Assinatura>>(_itens.Where(a => a.BusinessId == businessId && a.Status == StatusAssinatura.Ativa).ToList());
        public Task<Assinatura?> BuscarAsync(string businessId, string assinaturaId, CancellationToken ct = default)
            => Task.FromResult(_itens.FirstOrDefault(a => a.BusinessId == businessId && a.Id == assinaturaId));
        public Task SalvarAsync(Assinatura assinatura, CancellationToken ct = default) { _itens.Add(assinatura); return Task.CompletedTask; }
    }
}
