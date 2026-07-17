using SistemaX.Modules.Financeiro.Application.CasosDeUso;
using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Domain.Caixa;
using SistemaX.Modules.Financeiro.Infrastructure.InMemory;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Tests.CasosDeUso;

/// <summary>
/// Casos de uso do ritual de caixa físico (docs/wiring/financeiro-telas-restantes.md §4) — a
/// invariante "não abrir 2 sessões simultâneas para o mesmo caixa" vive AQUI (não no agregado
/// <see cref="SistemaX.Modules.Financeiro.Domain.Caixa.SessaoCaixa"/>) porque depende de consultar
/// outras instâncias persistidas via <see cref="ISessaoCaixaRepository"/>.
/// </summary>
public sealed class SessaoCaixaUseCasesTests
{
    private const string BusinessId = "biz-1";
    private const string ContaCaixaId = "conta-caixa-padrao";
    private static readonly DateTimeOffset Agora = new(2026, 7, 17, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task AbrirSessaoCaixaUseCase_primeira_abertura_da_conta_e_aceita()
    {
        var repo = new InMemorySessaoCaixaRepository();
        var useCase = new AbrirSessaoCaixaUseCase(repo);

        var resultado = await useCase.ExecutarAsync(BusinessId, ContaCaixaId, "op-1", "Ana", new Money(20_000), Agora);

        Assert.True(resultado.Sucesso);
        var persistida = await repo.ObterPorIdAsync(BusinessId, resultado.Valor.Id);
        Assert.NotNull(persistida);
    }

    [Fact]
    public async Task AbrirSessaoCaixaUseCase_rejeita_segunda_abertura_enquanto_a_primeira_esta_aberta()
    {
        var repo = new InMemorySessaoCaixaRepository();
        var useCase = new AbrirSessaoCaixaUseCase(repo);

        var primeira = await useCase.ExecutarAsync(BusinessId, ContaCaixaId, "op-1", "Ana", new Money(20_000), Agora);
        Assert.True(primeira.Sucesso);

        var segunda = await useCase.ExecutarAsync(BusinessId, ContaCaixaId, "op-2", "Bia", new Money(15_000), Agora.AddMinutes(5));

        Assert.True(segunda.Falha);
        Assert.Equal("financeiro.sessao_caixa.ja_aberta", segunda.Erro.Codigo);
    }

    [Fact]
    public async Task AbrirSessaoCaixaUseCase_permite_nova_abertura_depois_que_a_anterior_fechou()
    {
        var repo = new InMemorySessaoCaixaRepository();
        var abrir = new AbrirSessaoCaixaUseCase(repo);
        var fechar = new FecharSessaoCaixaUseCase(repo);

        var primeira = await abrir.ExecutarAsync(BusinessId, ContaCaixaId, "op-1", "Ana", new Money(20_000), Agora);
        await fechar.ExecutarAsync(BusinessId, primeira.Valor.Id, new Money(20_000), Agora.AddHours(8));

        var segunda = await abrir.ExecutarAsync(BusinessId, ContaCaixaId, "op-2", "Bia", new Money(15_000), Agora.AddHours(9));

        Assert.True(segunda.Sucesso);
    }

    [Fact]
    public async Task AbrirSessaoCaixaUseCase_mesma_conta_em_businesses_diferentes_nao_colide_R1()
    {
        var repo = new InMemorySessaoCaixaRepository();
        var useCase = new AbrirSessaoCaixaUseCase(repo);

        var daLojaA = await useCase.ExecutarAsync("biz-a", ContaCaixaId, "op-1", "Ana", new Money(20_000), Agora);
        var daLojaB = await useCase.ExecutarAsync("biz-b", ContaCaixaId, "op-2", "Bia", new Money(10_000), Agora);

        Assert.True(daLojaA.Sucesso);
        Assert.True(daLojaB.Sucesso);
    }

    [Fact]
    public async Task MovimentarSessaoCaixaUseCase_registra_suprimento_e_sangria_na_sessao_aberta()
    {
        var repo = new InMemorySessaoCaixaRepository();
        var abrir = new AbrirSessaoCaixaUseCase(repo);
        var movimentar = new MovimentarSessaoCaixaUseCase(repo);

        var sessao = (await abrir.ExecutarAsync(BusinessId, ContaCaixaId, "op-1", "Ana", new Money(20_000), Agora)).Valor;

        var suprimento = await movimentar.RegistrarSuprimentoAsync(BusinessId, sessao.Id, new Money(5_000), "reforço", Agora, "op-1", "Ana");
        var sangria = await movimentar.RegistrarSangriaAsync(BusinessId, sessao.Id, new Money(3_000), "depósito", Agora, "op-1", "Ana");

        Assert.True(suprimento.Sucesso);
        Assert.True(sangria.Sucesso);

        var persistida = await repo.ObterPorIdAsync(BusinessId, sessao.Id);
        Assert.Equal(2, persistida!.Movimentos.Count);
        Assert.Equal(new Money(22_000), persistida.SaldoEsperado); // 200 + 50 - 30
    }

    [Fact]
    public async Task MovimentarSessaoCaixaUseCase_sangria_que_excede_saldo_e_rejeitada_e_nao_persiste()
    {
        var repo = new InMemorySessaoCaixaRepository();
        var abrir = new AbrirSessaoCaixaUseCase(repo);
        var movimentar = new MovimentarSessaoCaixaUseCase(repo);

        var sessao = (await abrir.ExecutarAsync(BusinessId, ContaCaixaId, "op-1", "Ana", new Money(10_000), Agora)).Valor;

        var resultado = await movimentar.RegistrarSangriaAsync(BusinessId, sessao.Id, new Money(10_001), "saque indevido", Agora, "op-1", "Ana");

        Assert.True(resultado.Falha);
        Assert.Equal("financeiro.sessao_caixa.sangria_excede_saldo", resultado.Erro.Codigo);

        var persistida = await repo.ObterPorIdAsync(BusinessId, sessao.Id);
        Assert.Empty(persistida!.Movimentos);
    }

    [Fact]
    public async Task MovimentarSessaoCaixaUseCase_nao_movimenta_sessao_ja_fechada()
    {
        var repo = new InMemorySessaoCaixaRepository();
        var abrir = new AbrirSessaoCaixaUseCase(repo);
        var fechar = new FecharSessaoCaixaUseCase(repo);
        var movimentar = new MovimentarSessaoCaixaUseCase(repo);

        var sessao = (await abrir.ExecutarAsync(BusinessId, ContaCaixaId, "op-1", "Ana", new Money(10_000), Agora)).Valor;
        await fechar.ExecutarAsync(BusinessId, sessao.Id, new Money(10_000), Agora.AddHours(8));

        var resultado = await movimentar.RegistrarSuprimentoAsync(BusinessId, sessao.Id, new Money(1_000), "tarde demais", Agora.AddHours(9), "op-1", "Ana");

        Assert.True(resultado.Falha);
        Assert.Equal("financeiro.sessao_caixa.status_invalido", resultado.Erro.Codigo);
    }

    [Fact]
    public async Task MovimentarSessaoCaixaUseCase_sessao_inexistente_falha_com_erro_dedicado()
    {
        var repo = new InMemorySessaoCaixaRepository();
        var movimentar = new MovimentarSessaoCaixaUseCase(repo);

        var resultado = await movimentar.RegistrarSuprimentoAsync(BusinessId, "sessao-que-nao-existe", new Money(1_000), "motivo", Agora, "op-1", "Ana");

        Assert.True(resultado.Falha);
        Assert.Equal("financeiro.sessao_caixa.nao_encontrada", resultado.Erro.Codigo);
    }

    [Fact]
    public async Task FecharSessaoCaixaUseCase_calcula_diferenca_e_persiste_a_sessao_fechada()
    {
        var repo = new InMemorySessaoCaixaRepository();
        var abrir = new AbrirSessaoCaixaUseCase(repo);
        var movimentar = new MovimentarSessaoCaixaUseCase(repo);
        var fechar = new FecharSessaoCaixaUseCase(repo);

        var sessao = (await abrir.ExecutarAsync(BusinessId, ContaCaixaId, "op-1", "Ana", new Money(20_000), Agora)).Valor;
        await movimentar.RegistrarSuprimentoAsync(BusinessId, sessao.Id, new Money(5_000), "reforço", Agora, "op-1", "Ana");

        var resultado = await fechar.ExecutarAsync(BusinessId, sessao.Id, new Money(24_000), Agora.AddHours(8));

        Assert.True(resultado.Sucesso);
        Assert.Equal(new Money(-1_000), resultado.Valor.Diferenca); // esperado 25_000, contado 24_000
        var persistida = await repo.ObterPorIdAsync(BusinessId, sessao.Id);
        Assert.Equal(StatusSessaoCaixa.Fechada, persistida!.Status);
    }
}
