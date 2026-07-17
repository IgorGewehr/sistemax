using SistemaX.Modules.Abstractions.Autorizacao;
using SistemaX.Modules.Identidade.Application.CasosDeUso;
using SistemaX.Modules.Identidade.Domain.Usuarios;
using SistemaX.Modules.Identidade.Infrastructure.InMemory;

namespace SistemaX.Modules.Identidade.Tests.CasosDeUso;

/// <summary>
/// Prova, no SERVIDOR (não só na UI), a invariante do ADR-0003 §6: founder é intocável — nunca se
/// pode rebaixar/desativar o ÚLTIMO founder ativo de uma instalação.
/// </summary>
public sealed class AlterarUsuarioUseCaseTests
{
    private const string TenantA = "loja-a";

    [Fact]
    public async Task Rebaixar_o_unico_founder_ativo_falha_com_founder_intocavel()
    {
        var repo = new InMemoryUsuarioRepository();
        var founder = Usuario.Criar(TenantA, "Founder", "founder@loja.com", "1111", Papel.Founder).Valor;
        await repo.SalvarAsync(founder);

        var useCase = new AlterarUsuarioUseCase(repo);
        var resultado = await useCase.ExecutarAsync(TenantA, founder.Id, novoPapel: Papel.Admin, novoAtivo: null, novoPin: null);

        Assert.True(resultado.Falha);
        Assert.Equal("usuario.founder_intocavel", resultado.Erro.Codigo);
    }

    [Fact]
    public async Task Desativar_o_unico_founder_ativo_falha_com_founder_intocavel()
    {
        var repo = new InMemoryUsuarioRepository();
        var founder = Usuario.Criar(TenantA, "Founder", "founder2@loja.com", "2222", Papel.Founder).Valor;
        await repo.SalvarAsync(founder);

        var useCase = new AlterarUsuarioUseCase(repo);
        var resultado = await useCase.ExecutarAsync(TenantA, founder.Id, novoPapel: null, novoAtivo: false, novoPin: null);

        Assert.True(resultado.Falha);
        Assert.Equal("usuario.founder_intocavel", resultado.Erro.Codigo);
    }

    [Fact]
    public async Task Rebaixar_um_founder_quando_ha_outro_founder_ativo_e_permitido()
    {
        var repo = new InMemoryUsuarioRepository();
        var founder1 = Usuario.Criar(TenantA, "Founder 1", "founder3@loja.com", "3333", Papel.Founder).Valor;
        var founder2 = Usuario.Criar(TenantA, "Founder 2", "founder4@loja.com", "4444", Papel.Founder).Valor;
        await repo.SalvarAsync(founder1);
        await repo.SalvarAsync(founder2);

        var useCase = new AlterarUsuarioUseCase(repo);
        var resultado = await useCase.ExecutarAsync(TenantA, founder1.Id, novoPapel: Papel.Admin, novoAtivo: null, novoPin: null);

        Assert.True(resultado.Sucesso);
        Assert.Equal(Papel.Admin, resultado.Valor.Papel);
    }

    [Fact]
    public async Task Alterar_papel_de_usuario_nao_founder_nunca_verifica_invariante()
    {
        var repo = new InMemoryUsuarioRepository();
        var operador = Usuario.Criar(TenantA, "Operador", "operador@loja.com", "5555", Papel.Operator).Valor;
        await repo.SalvarAsync(operador);

        var useCase = new AlterarUsuarioUseCase(repo);
        var resultado = await useCase.ExecutarAsync(TenantA, operador.Id, novoPapel: Papel.Manager, novoAtivo: false, novoPin: null);

        Assert.True(resultado.Sucesso);
        Assert.Equal(Papel.Manager, resultado.Valor.Papel);
        Assert.Equal(StatusUsuario.Inativo, resultado.Valor.Status);
    }

    [Fact]
    public async Task Usuario_inexistente_falha_com_usuario_nao_encontrado()
    {
        var repo = new InMemoryUsuarioRepository();
        var useCase = new AlterarUsuarioUseCase(repo);

        var resultado = await useCase.ExecutarAsync(TenantA, "id-que-nao-existe", Papel.Admin, null, null);

        Assert.True(resultado.Falha);
        Assert.Equal("usuario.nao_encontrado", resultado.Erro.Codigo);
    }

    [Fact]
    public async Task Redefinir_pin_para_um_ja_usado_por_outro_ativo_falha_com_pin_duplicado()
    {
        var repo = new InMemoryUsuarioRepository();
        var a = Usuario.Criar(TenantA, "A", "a@loja.com", "1010", Papel.Operator).Valor;
        var b = Usuario.Criar(TenantA, "B", "b@loja.com", "2020", Papel.Operator).Valor;
        await repo.SalvarAsync(a);
        await repo.SalvarAsync(b);

        var useCase = new AlterarUsuarioUseCase(repo);
        var resultado = await useCase.ExecutarAsync(TenantA, b.Id, novoPapel: null, novoAtivo: null, novoPin: "1010");

        Assert.True(resultado.Falha);
        Assert.Equal("usuario.pin_duplicado", resultado.Erro.Codigo);
    }
}
