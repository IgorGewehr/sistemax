using SistemaX.Modules.Abstractions.Autorizacao;
using SistemaX.Modules.Identidade.Application.CasosDeUso;
using SistemaX.Modules.Identidade.Domain.Usuarios;
using SistemaX.Modules.Identidade.Infrastructure.InMemory;

namespace SistemaX.Modules.Identidade.Tests.CasosDeUso;

public sealed class CriarUsuarioUseCaseTests
{
    private const string TenantA = "loja-a";

    [Fact]
    public async Task Cria_usuario_com_pin_unico_com_sucesso()
    {
        var repo = new InMemoryUsuarioRepository();
        var useCase = new CriarUsuarioUseCase(repo);

        var resultado = await useCase.ExecutarAsync(TenantA, "Fulano", "fulano@loja.com", "1234", Papel.Operator);

        Assert.True(resultado.Sucesso);
        Assert.Equal(Papel.Operator, resultado.Valor.Papel);
    }

    [Fact]
    public async Task Pin_ja_usado_por_outro_usuario_ativo_do_mesmo_tenant_falha_com_pin_duplicado()
    {
        var repo = new InMemoryUsuarioRepository();
        var useCase = new CriarUsuarioUseCase(repo);
        await useCase.ExecutarAsync(TenantA, "Fulano", "fulano2@loja.com", "5555", Papel.Operator);

        var resultado = await useCase.ExecutarAsync(TenantA, "Sicrano", "sicrano@loja.com", "5555", Papel.Viewer);

        Assert.True(resultado.Falha);
        Assert.Equal("usuario.pin_duplicado", resultado.Erro.Codigo);
    }

    [Fact]
    public async Task Pin_de_usuario_inativo_pode_ser_reusado()
    {
        var repo = new InMemoryUsuarioRepository();
        var useCase = new CriarUsuarioUseCase(repo);
        var primeiro = await useCase.ExecutarAsync(TenantA, "Fulano", "fulano3@loja.com", "6666", Papel.Operator);
        primeiro.Valor.Desativar();
        await repo.SalvarAsync(primeiro.Valor);

        var resultado = await useCase.ExecutarAsync(TenantA, "Sicrano", "sicrano2@loja.com", "6666", Papel.Viewer);

        Assert.True(resultado.Sucesso);
    }

    [Fact]
    public async Task Mesmo_pin_em_tenants_diferentes_nao_colide()
    {
        var repo = new InMemoryUsuarioRepository();
        var useCase = new CriarUsuarioUseCase(repo);
        await useCase.ExecutarAsync(TenantA, "Fulano", "fulano4@loja.com", "7777", Papel.Operator);

        var resultado = await useCase.ExecutarAsync("loja-b", "Sicrano", "sicrano3@loja.com", "7777", Papel.Viewer);

        Assert.True(resultado.Sucesso);
    }
}
