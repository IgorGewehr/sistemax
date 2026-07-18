using Microsoft.Extensions.DependencyInjection;
using SistemaX.Modules.Abstractions.Autorizacao;
using SistemaX.Modules.Identidade.Application.CasosDeUso;
using SistemaX.Modules.Identidade.Application.Ports;
using SistemaX.Modules.Identidade.Domain.Usuarios;
using SistemaX.Modules.Identidade.Infrastructure.InMemory;
using SistemaX.Modules.Identidade.Infrastructure.Seed;

namespace SistemaX.Modules.Identidade.Tests.Seed;

/// <summary>
/// Wizard de 1º-boot (backend) — prova que o founder nasce com <c>PinProvisorio = true</c> e que
/// esse estado é encerrado (nunca reaberto) assim que o PIN é trocado, seja de fato pelo próprio
/// usuário (<c>TrocarPinUseCase</c>, o caminho que <c>POST /api/auth/trocar-pin</c> usa) ou por um
/// admin (<c>AlterarUsuarioUseCase</c>, <c>PATCH /usuarios/{id}</c>).
/// </summary>
public sealed class IdentidadeBootstrapSeederTests
{
    private const string BusinessId = "loja-teste";

    [Fact]
    public async Task Seed_cria_founder_com_pin_provisorio_true()
    {
        var repo = new InMemoryUsuarioRepository();
        var provider = ConstruirProvider(repo);

        await IdentidadeBootstrapSeeder.SemearFounderAsync(provider, BusinessId);

        var founder = (await repo.ListarAsync(BusinessId, incluirInativos: true)).Single();
        Assert.Equal(Papel.Founder, founder.Papel);
        Assert.True(founder.PinProvisorio);
        Assert.True(founder.VerificarPin("1234"));
    }

    [Fact]
    public async Task Seed_e_idempotente_nao_duplica_founder_em_boots_subsequentes()
    {
        var repo = new InMemoryUsuarioRepository();
        var provider = ConstruirProvider(repo);

        await IdentidadeBootstrapSeeder.SemearFounderAsync(provider, BusinessId);
        await IdentidadeBootstrapSeeder.SemearFounderAsync(provider, BusinessId);

        Assert.Single(await repo.ListarAsync(BusinessId, incluirInativos: true));
    }

    [Fact]
    public async Task Trocar_o_proprio_pin_do_founder_semeado_zera_pin_provisorio()
    {
        var repo = new InMemoryUsuarioRepository();
        var provider = ConstruirProvider(repo);
        await IdentidadeBootstrapSeeder.SemearFounderAsync(provider, BusinessId);
        var founder = (await repo.ListarAsync(BusinessId, incluirInativos: true)).Single();

        var trocar = new TrocarPinUseCase(repo);
        var resultado = await trocar.ExecutarAsync(BusinessId, founder.Id, pinAtual: "1234", pinNovo: "7391");

        Assert.True(resultado.Sucesso);
        Assert.False(resultado.Valor.PinProvisorio);
        Assert.True(resultado.Valor.VerificarPin("7391"));

        var lido = await repo.ObterPorIdAsync(founder.Id);
        Assert.False(lido!.PinProvisorio);
    }

    [Fact]
    public async Task Trocar_o_proprio_pin_com_pin_atual_errado_falha_e_nao_altera_a_flag()
    {
        var repo = new InMemoryUsuarioRepository();
        var provider = ConstruirProvider(repo);
        await IdentidadeBootstrapSeeder.SemearFounderAsync(provider, BusinessId);
        var founder = (await repo.ListarAsync(BusinessId, incluirInativos: true)).Single();

        var trocar = new TrocarPinUseCase(repo);
        var resultado = await trocar.ExecutarAsync(BusinessId, founder.Id, pinAtual: "0000", pinNovo: "9876");

        Assert.True(resultado.Falha);
        Assert.Equal("auth.pin_atual_incorreto", resultado.Erro.Codigo);

        var lido = await repo.ObterPorIdAsync(founder.Id);
        Assert.True(lido!.PinProvisorio);
        Assert.True(lido.VerificarPin("1234"));
    }

    [Fact]
    public async Task Admin_trocando_o_pin_de_outro_usuario_via_AlterarUsuarioUseCase_tambem_zera_pin_provisorio()
    {
        var repo = new InMemoryUsuarioRepository();
        var provider = ConstruirProvider(repo);
        await IdentidadeBootstrapSeeder.SemearFounderAsync(provider, BusinessId);
        var founder = (await repo.ListarAsync(BusinessId, incluirInativos: true)).Single();

        var alterar = new AlterarUsuarioUseCase(repo);
        var resultado = await alterar.ExecutarAsync(BusinessId, founder.Id, novoPapel: null, novoAtivo: null, novoPin: "4321");

        Assert.True(resultado.Sucesso);
        Assert.False(resultado.Valor.PinProvisorio);
    }

    private static ServiceProvider ConstruirProvider(IUsuarioRepository repo)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IUsuarioRepository>(repo);
        services.AddScoped<CriarUsuarioUseCase>();
        return services.BuildServiceProvider();
    }
}
