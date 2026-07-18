using SistemaX.Modules.Financeiro.Application.CasosDeUso;
using SistemaX.Modules.Financeiro.Application.Mrr;
using SistemaX.Modules.Financeiro.Application.ReadModels;
using SistemaX.Modules.Financeiro.Domain.Recorrencia;
using SistemaX.Modules.Financeiro.Infrastructure.InMemory;
using SistemaX.Modules.Financeiro.Tests.Fakes;
using SistemaX.SharedKernel;
using Xunit;

namespace SistemaX.Modules.Financeiro.Tests.Mrr;

/// <summary>
/// P1-4 (docs/financeiro/revisao-domain-fit-cnpj.md) — a invariante central do painel de
/// movimentos, exercitada ponta-a-ponta pelos USE CASES reais (não construindo <see cref="MovimentoMrr"/>
/// à mão): <c>MRR_fim = MRR_início + Novo + Expansão − Contração − Churn + Reativação</c>, e essa
/// soma bate com o MRR "de verdade" (<see cref="ReceitaRecorrenteService"/>, que soma
/// <c>Assinatura.Mrr</c> das assinaturas ativas/inadimplentes no mesmo instante) — prova que o
/// ledger de movimentos nunca diverge da fonte.
/// </summary>
public class PainelDeMovimentosMrrServiceTests
{
    private const string Biz = "loja-1";

    private static (InMemoryAssinaturaRepository Assinaturas, InMemoryMovimentoMrrRepository Movimentos, FakeRelogio Relogio) NovoAmbiente(DateTimeOffset agora)
        => (new InMemoryAssinaturaRepository(), new InMemoryMovimentoMrrRepository(), new FakeRelogio(agora));

    [Fact]
    public async Task Identidade_de_movimentos_bate_com_mrr_real_apos_novo_expansao_contracao_pausa_reativacao_e_churn()
    {
        var mes1 = new DateTimeOffset(2026, 6, 10, 0, 0, 0, TimeSpan.Zero);
        var mes2 = new DateTimeOffset(2026, 7, 10, 0, 0, 0, TimeSpan.Zero);
        var (assinaturas, movimentos, relogio) = NovoAmbiente(mes1);

        var criar = new CriarAssinaturaUseCase(assinaturas, movimentos);
        var alterar = new AlterarValorAssinaturaUseCase(assinaturas, movimentos, relogio);
        var pausarReativar = new PausarReativarAssinaturaUseCase(assinaturas, movimentos, relogio);
        var cancelar = new CancelarAssinaturaUseCase(assinaturas, movimentos, relogio);

        // Mês 1: duas assinaturas novas (MRR 500 + MRR 300).
        var a1 = await criar.ExecutarAsync(new CriarAssinaturaComando(
            Biz, "cli-1", "Cliente 1", "srv-a", "Serviço A", Money.DeReais(500), FrequenciaRecorrencia.Mensal, 5, mes1));
        var a2 = await criar.ExecutarAsync(new CriarAssinaturaComando(
            Biz, "cli-2", "Cliente 2", "srv-b", "Serviço B", Money.DeReais(300), FrequenciaRecorrencia.Mensal, 5, mes1));
        Assert.True(a1.Sucesso);
        Assert.True(a2.Sucesso);

        // Mês 2: a1 expande (500 -> 700), uma terceira nasce e É CANCELADA no mesmo mês (o caso do
        // viés do audit), a2 é pausada.
        relogio.Momento = mes2;
        var a3 = await criar.ExecutarAsync(new CriarAssinaturaComando(
            Biz, "cli-3", "Cliente 3", "srv-c", "Serviço C", Money.DeReais(200), FrequenciaRecorrencia.Mensal, 5, mes2));
        Assert.True(a3.Sucesso);

        Assert.True((await alterar.ExecutarAsync(Biz, a1.Valor.Id, Money.DeReais(700))).Sucesso);
        Assert.True((await pausarReativar.PausarAsync(Biz, a2.Valor.Id)).Sucesso);
        Assert.True((await cancelar.ExecutarAsync(Biz, a3.Valor.Id, "não gostou")).Sucesso);

        var painel = new PainelDeMovimentosMrrService(movimentos);
        var resumo = await painel.CalcularAsync(Biz, new DateOnly(mes2.Year, mes2.Month, 1));

        // MRR_início do mês 2 = soma do que existia no início: 500 (a1) + 300 (a2) = 800.
        Assert.Equal(80000, resumo.MrrInicio.Centavos);
        Assert.Equal(20000, resumo.Novo.Centavos);       // a3
        Assert.Equal(20000, resumo.Expansao.Centavos);   // a1: 700-500
        Assert.Equal(30000, resumo.Contracao.Centavos);  // a2 pausada: MRR cheio retirado
        Assert.Equal(20000, resumo.Churn.Centavos);       // a3 nasceu-e-morreu no mês
        Assert.Equal(0, resumo.Reativacao.Centavos);

        // A IDENTIDADE testada explicitamente.
        var mrrFimEsperado = resumo.MrrInicio.Centavos + resumo.Novo.Centavos + resumo.Expansao.Centavos
                              - resumo.Contracao.Centavos - resumo.Churn.Centavos + resumo.Reativacao.Centavos;
        Assert.Equal(mrrFimEsperado, resumo.MrrFim.Centavos);

        // MRR "de verdade" no fim do mês 2: a1 ativa (700) + a2 pausada (0, fora da soma) + a3 cancelada (0).
        var receitaRecorrente = new ReceitaRecorrenteService(assinaturas);
        var real = await receitaRecorrente.CalcularAsync(Biz, mes2);
        Assert.Equal(real.Mrr.Centavos, resumo.MrrFim.Centavos);
        Assert.Equal(70000, resumo.MrrFim.Centavos); // só a1, expandida
    }

    /// <summary>
    /// O viés que o audit apontou (P1-4): a fórmula ANTIGA de <c>ReceitaRecorrenteService</c>
    /// deriva <c>mrrInicioMes = mrr − novo + churn</c> por ÁLGEBRA sobre o snapshot atual — uma
    /// assinatura nascida-E-cancelada no MESMO mês some do <c>mrr</c> corrente E do <c>novo</c>
    /// (ela não é mais "ativa"), mas o <c>churn</c> a soma de volta, INFLANDO o denominador
    /// derivado (ele conta como se ela tivesse existido desde o dia 1). <c>PainelDeMovimentosMrrService</c>
    /// não deriva nada por álgebra: <c>MrrInicio</c> é a soma REAL dos movimentos anteriores ao
    /// mês — nunca inclui a efêmera. Resultado: o churn% NOVO é MAIOR (mais honesto) que o antigo,
    /// porque o denominador antigo estava artificialmente inflado (nunca menor, como se poderia
    /// supor à primeira vista).
    /// </summary>
    [Fact]
    public async Task ChurnPercent_usa_denominador_correto_quando_ha_assinatura_nascida_e_cancelada_no_mesmo_mes()
    {
        var mes1 = new DateTimeOffset(2026, 6, 10, 0, 0, 0, TimeSpan.Zero);
        var mes2 = new DateTimeOffset(2026, 7, 10, 0, 0, 0, TimeSpan.Zero);
        var (assinaturas, movimentos, relogio) = NovoAmbiente(mes1);

        var criar = new CriarAssinaturaUseCase(assinaturas, movimentos);
        var cancelar = new CancelarAssinaturaUseCase(assinaturas, movimentos, relogio);

        // Base estabelecida ANTES do mês 2 — R$1.000 + R$200 = R$1.200. É ESSE o MrrInicio real.
        var estabelecida1 = await criar.ExecutarAsync(new CriarAssinaturaComando(
            Biz, "cli-1", "Cliente 1", "srv-a", "Serviço A", Money.DeReais(1000), FrequenciaRecorrencia.Mensal, 5, mes1));
        var estabelecida2 = await criar.ExecutarAsync(new CriarAssinaturaComando(
            Biz, "cli-2", "Cliente 2", "srv-b", "Serviço B", Money.DeReais(200), FrequenciaRecorrencia.Mensal, 5, mes1));
        Assert.True(estabelecida1.Sucesso);
        Assert.True(estabelecida2.Sucesso);

        relogio.Momento = mes2;
        // Nasce e morre no mesmo mês — R$500.
        var efemera = await criar.ExecutarAsync(new CriarAssinaturaComando(
            Biz, "cli-3", "Cliente 3", "srv-c", "Serviço C", Money.DeReais(500), FrequenciaRecorrencia.Mensal, 5, mes2));
        Assert.True(efemera.Sucesso);
        Assert.True((await cancelar.ExecutarAsync(Biz, efemera.Valor.Id, "arrependeu")).Sucesso);
        // Churn de verdade, da base estabelecida — R$200.
        Assert.True((await cancelar.ExecutarAsync(Biz, estabelecida2.Valor.Id, "cliente saiu")).Sucesso);

        var painel = new PainelDeMovimentosMrrService(movimentos);
        var resumo = await painel.CalcularAsync(Biz, new DateOnly(mes2.Year, mes2.Month, 1));

        Assert.Equal(120000, resumo.MrrInicio.Centavos); // 1.000 + 200 — NUNCA inclui a efêmera
        Assert.Equal(50000, resumo.Novo.Centavos);
        Assert.Equal(70000, resumo.Churn.Centavos);       // efêmera (500) + estabelecida2 (200)
        Assert.Equal(58.3m, resumo.ChurnPercent);         // 700 / 1.200 — o número honesto
        Assert.Equal(100000, resumo.MrrFim.Centavos);     // só estabelecida1 sobra
    }
}
