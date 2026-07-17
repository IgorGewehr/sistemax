using SistemaX.Modules.Abstractions.Autorizacao;
using SistemaX.Modules.Identidade.Application.CasosDeUso;
using SistemaX.Modules.Identidade.Domain.Usuarios;
using SistemaX.Modules.Identidade.Infrastructure.InMemory;

namespace SistemaX.Modules.Identidade.Tests.CasosDeUso;

public sealed class AutenticarPorPinUseCaseTests
{
    private const string TenantA = "loja-a";

    [Fact]
    public async Task Pin_de_usuario_ativo_autentica_e_devolve_o_usuario_com_seu_papel_real()
    {
        var repo = new InMemoryUsuarioRepository();
        var manager = Usuario.Criar(TenantA, "Gerente", "gerente@loja.com", "2468", Papel.Manager).Valor;
        await repo.SalvarAsync(manager);

        var useCase = new AutenticarPorPinUseCase(repo);
        var resultado = await useCase.ExecutarAsync(TenantA, "2468");

        Assert.True(resultado.Sucesso);
        Assert.Equal(manager.Id, resultado.Valor.Id);
        Assert.Equal(Papel.Manager, resultado.Valor.Papel);
    }

    [Fact]
    public async Task Pin_registra_ultimo_acesso_apos_login_bem_sucedido()
    {
        var repo = new InMemoryUsuarioRepository();
        var usuario = Usuario.Criar(TenantA, "Fulano", "fulano@loja.com", "1357", Papel.Viewer).Valor;
        await repo.SalvarAsync(usuario);

        var useCase = new AutenticarPorPinUseCase(repo);
        await useCase.ExecutarAsync(TenantA, "1357");

        var lido = await repo.ObterPorIdAsync(usuario.Id);
        Assert.NotNull(lido!.UltimoAcessoEm);
    }

    [Fact]
    public async Task Pin_invalido_falha_com_codigo_generico_sem_revelar_qual_usuario_quase_bateu()
    {
        var repo = new InMemoryUsuarioRepository();
        var usuario = Usuario.Criar(TenantA, "Fulano", "fulano2@loja.com", "9999", Papel.Admin).Valor;
        await repo.SalvarAsync(usuario);

        var useCase = new AutenticarPorPinUseCase(repo);
        var resultado = await useCase.ExecutarAsync(TenantA, "0000");

        Assert.True(resultado.Falha);
        Assert.Equal("auth.pin_invalido", resultado.Erro.Codigo);
    }

    [Fact]
    public async Task Pin_de_usuario_desativado_nao_autentica()
    {
        var repo = new InMemoryUsuarioRepository();
        var usuario = Usuario.Criar(TenantA, "Desativado", "desativado@loja.com", "4321", Papel.Operator).Valor;
        usuario.Desativar();
        await repo.SalvarAsync(usuario);

        var useCase = new AutenticarPorPinUseCase(repo);
        var resultado = await useCase.ExecutarAsync(TenantA, "4321");

        Assert.True(resultado.Falha);
        Assert.Equal("auth.pin_invalido", resultado.Erro.Codigo);
    }

    [Fact]
    public async Task Pin_de_usuario_de_outro_tenant_nao_autentica()
    {
        var repo = new InMemoryUsuarioRepository();
        var usuario = Usuario.Criar("loja-b", "Fulano", "fulano3@loja.com", "1122", Papel.Founder).Valor;
        await repo.SalvarAsync(usuario);

        var useCase = new AutenticarPorPinUseCase(repo);
        var resultado = await useCase.ExecutarAsync(TenantA, "1122");

        Assert.True(resultado.Falha);
    }
}
