using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Financeiro.Application.Categorias;
using SistemaX.Modules.Financeiro.Application.CasosDeUso;
using SistemaX.Modules.Financeiro.Application.EventosDeIntegracao.Handlers;
using SistemaX.Modules.Financeiro.Domain.Assinaturas;
using SistemaX.Modules.Financeiro.Domain.Comum;
using SistemaX.Modules.Financeiro.Domain.ContasAPagarReceber;
using SistemaX.Modules.Financeiro.Domain.Recorrencia;
using SistemaX.Modules.Financeiro.Infrastructure.InMemory;
using SistemaX.SharedKernel;
using Xunit;

namespace SistemaX.Modules.Financeiro.Tests.Mrr;

/// <summary>
/// P1-4 (docs/financeiro/revisao-domain-fit-cnpj.md) — dunning ponta-a-ponta: cobrança de
/// assinatura vence sem pagamento → <see cref="DunningAssinaturaHandler"/> marca
/// <see cref="StatusAssinatura.Inadimplente"/>; se liquidada dentro da graça, regulariza; se a
/// graça expira, <see cref="AvaliarDunningAssinaturasUseCase"/> cancela (churn).
/// </summary>
public class DunningAssinaturaTests
{
    private const string Biz = "loja-1";
    private static readonly DateTimeOffset Inicio = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static (Assinatura Assinatura, ContaAReceber Conta) NovaAssinaturaComCobranca()
    {
        var assinatura = Assinatura.Criar(
            Biz, "cli-1", "Cliente 1", "srv-a", "Serviço A", new Money(50000), FrequenciaRecorrencia.Mensal, 5, Inicio).Valor;
        var competencia = assinatura.ProximaCompetenciaDevida;
        var conta = assinatura.GerarCobranca(competencia, CategoriaFinanceiraPadrao.ReceitaRecorrente).Valor;
        return (assinatura, conta);
    }

    [Fact]
    public async Task ParcelaVencida_de_cobranca_de_assinatura_marca_inadimplente()
    {
        var (assinatura, conta) = NovaAssinaturaComCobranca();
        var assinaturas = new InMemoryAssinaturaRepository();
        var contasAReceber = new InMemoryContaAReceberRepository();
        await assinaturas.SalvarAsync(assinatura);
        await contasAReceber.SalvarAsync(conta);

        var handler = new DunningAssinaturaHandler(contasAReceber, assinaturas);
        var evento = new ParcelaVencida(conta.Id, conta.Parcelas[0].Id, Biz, conta.ValorTotal.Centavos, EhAPagar: false, conta.DataCompetencia);

        await handler.HandleAsync(evento);

        var lida = await assinaturas.BuscarAsync(Biz, assinatura.Id);
        Assert.Equal(StatusAssinatura.Inadimplente, lida!.Status);
        Assert.Equal(conta.DataCompetencia, lida.InadimplenteDesde);
    }

    [Fact]
    public async Task ParcelaVencida_e_idempotente_reentrega_nao_falha_nem_duplica_efeito()
    {
        var (assinatura, conta) = NovaAssinaturaComCobranca();
        var assinaturas = new InMemoryAssinaturaRepository();
        var contasAReceber = new InMemoryContaAReceberRepository();
        await assinaturas.SalvarAsync(assinatura);
        await contasAReceber.SalvarAsync(conta);

        var handler = new DunningAssinaturaHandler(contasAReceber, assinaturas);
        var evento = new ParcelaVencida(conta.Id, conta.Parcelas[0].Id, Biz, conta.ValorTotal.Centavos, EhAPagar: false, conta.DataCompetencia);

        await handler.HandleAsync(evento);
        await handler.HandleAsync(evento); // reentrega — nunca lança, estado já é Inadimplente

        var lida = await assinaturas.BuscarAsync(Biz, assinatura.Id);
        Assert.Equal(StatusAssinatura.Inadimplente, lida!.Status);
    }

    [Fact]
    public async Task ParcelaBaixada_regulariza_assinatura_inadimplente()
    {
        var (assinatura, conta) = NovaAssinaturaComCobranca();
        var assinaturas = new InMemoryAssinaturaRepository();
        var contasAReceber = new InMemoryContaAReceberRepository();
        var handler = new DunningAssinaturaHandler(contasAReceber, assinaturas);

        assinatura.MarcarInadimplente(conta.DataCompetencia);
        await assinaturas.SalvarAsync(assinatura);
        await contasAReceber.SalvarAsync(conta);

        var pagamento = new ParcelaBaixada(conta.Id, conta.Parcelas[0].Id, Biz, EhAPagar: false, conta.ValorTotal.Centavos, conta.DataCompetencia.AddDays(2));
        await handler.HandleAsync(pagamento);

        var lida = await assinaturas.BuscarAsync(Biz, assinatura.Id);
        Assert.Equal(StatusAssinatura.Ativa, lida!.Status);
        Assert.Null(lida.InadimplenteDesde);
    }

    [Fact]
    public async Task ParcelaVencida_de_conta_a_pagar_ou_de_outra_origem_e_ignorada()
    {
        var assinaturas = new InMemoryAssinaturaRepository();
        var contasAReceber = new InMemoryContaAReceberRepository();
        var handler = new DunningAssinaturaHandler(contasAReceber, assinaturas);

        // EhAPagar = true — dunning é só do lado a receber.
        await handler.HandleAsync(new ParcelaVencida("conta-x", "parcela-x", Biz, 1000, EhAPagar: true, Inicio));

        // Conta de origem que não é de assinatura nenhuma.
        var vendaAvulsa = ContaAReceber.Criar(
            Biz, new SourceRef("sale", "venda-1"), "Venda", "servicos", Inicio, new Money(1000),
            ContaFinanceiraBase.ParcelaUnica(new Money(1000), Inicio)).Valor;
        await contasAReceber.SalvarAsync(vendaAvulsa);
        await handler.HandleAsync(new ParcelaVencida(vendaAvulsa.Id, vendaAvulsa.Parcelas[0].Id, Biz, 1000, EhAPagar: false, Inicio));

        // Nada lançou exceção — o handler simplesmente não teve nada a fazer.
        Assert.Empty(await assinaturas.ListarAsync(Biz));
    }

    [Fact]
    public async Task AvaliarDunning_cancela_apos_graca_expirar_e_registra_churn()
    {
        var assinatura = Assinatura.Criar(
            Biz, "cli-1", "Cliente 1", "srv-a", "Serviço A", new Money(50000), FrequenciaRecorrencia.Mensal, 5, Inicio).Valor;
        assinatura.MarcarInadimplente(Inicio);
        var assinaturas = new InMemoryAssinaturaRepository();
        var movimentos = new InMemoryMovimentoMrrRepository();
        await assinaturas.SalvarAsync(assinatura);

        var useCase = new AvaliarDunningAssinaturasUseCase(assinaturas, movimentos);

        // Ainda dentro da graça (7 dias) — não cancela.
        var canceladasCedo = await useCase.ExecutarAsync(Biz, Inicio.AddDays(3), diasGraca: 7);
        Assert.Equal(0, canceladasCedo);
        Assert.Equal(StatusAssinatura.Inadimplente, (await assinaturas.BuscarAsync(Biz, assinatura.Id))!.Status);

        // Graça expirada — cancela.
        var canceladas = await useCase.ExecutarAsync(Biz, Inicio.AddDays(8), diasGraca: 7);
        Assert.Equal(1, canceladas);
        var lida = await assinaturas.BuscarAsync(Biz, assinatura.Id);
        Assert.Equal(StatusAssinatura.Cancelada, lida!.Status);

        var movimentosRegistrados = await movimentos.ListarAsync(Biz);
        Assert.Contains(movimentosRegistrados, m => m.Tipo == Application.Mrr.TipoMovimentoMrr.Churn && m.ValorCentavos == 50000);

        // Idempotente: rodar de novo não cancela (nem lança) outra vez.
        var reexecutada = await useCase.ExecutarAsync(Biz, Inicio.AddDays(9), diasGraca: 7);
        Assert.Equal(0, reexecutada);
    }
}
