using SistemaX.Modules.Abstractions.Autorizacao;
using SistemaX.Modules.Identidade.Application.Ports;
using SistemaX.Modules.Identidade.Domain.Usuarios;

namespace SistemaX.Modules.Identidade.Tests.Contracts;

/// <summary>
/// Contract test do port <see cref="IUsuarioRepository"/> — roda o MESMO conjunto de casos contra
/// qualquer adapter (hoje: <c>InMemoryUsuarioRepository</c> e <c>SqliteUsuarioRepository</c>),
/// mesmo molde de <c>FornecedorRepositoryContractTests</c> (ver docs/persistencia/persistencia-sqlite.md).
/// </summary>
public abstract class UsuarioRepositoryContractTests
{
    protected const string TenantA = "loja-a";
    protected const string TenantB = "loja-b";

    protected abstract IUsuarioRepository CriarRepositorio();

    [Fact]
    public async Task Salvar_e_buscar_por_id_retorna_o_mesmo_usuario()
    {
        var repo = CriarRepositorio();
        var usuario = Usuario.Criar(TenantA, "Fulano", "fulano@loja.com", "1234", Papel.Manager).Valor;

        await repo.SalvarAsync(usuario);
        var lido = await repo.ObterPorIdAsync(usuario.Id);

        Assert.NotNull(lido);
        Assert.Equal(usuario.Id, lido!.Id);
        Assert.Equal(usuario.BusinessId, lido.BusinessId);
        Assert.Equal(usuario.Nome, lido.Nome);
        Assert.Equal(usuario.Email, lido.Email);
        Assert.Equal(usuario.Papel, lido.Papel);
        Assert.Equal(usuario.Status, lido.Status);
        Assert.Equal(usuario.PinHash, lido.PinHash);
        Assert.Equal(usuario.PinSalt, lido.PinSalt);
        Assert.True(lido.VerificarPin("1234"));
        Assert.False(lido.PinProvisorio);
    }

    [Fact]
    public async Task Salvar_e_buscar_por_id_persiste_pin_provisorio_true()
    {
        var repo = CriarRepositorio();
        var usuario = Usuario.Criar(
            TenantA, "Founder Semeado", "founder-semeado@loja.com", "1234", Papel.Founder, pinProvisorio: true).Valor;

        await repo.SalvarAsync(usuario);
        var lido = await repo.ObterPorIdAsync(usuario.Id);

        Assert.True(lido!.PinProvisorio);
    }

    [Fact]
    public async Task Redefinir_pin_zera_pin_provisorio_apos_persistir()
    {
        var repo = CriarRepositorio();
        var usuario = Usuario.Criar(
            TenantA, "Founder Semeado", "founder-semeado2@loja.com", "1234", Papel.Founder, pinProvisorio: true).Valor;
        await repo.SalvarAsync(usuario);

        usuario.RedefinirPin("5678");
        await repo.SalvarAsync(usuario);

        var lido = await repo.ObterPorIdAsync(usuario.Id);
        Assert.False(lido!.PinProvisorio);
        Assert.True(lido.VerificarPin("5678"));
    }

    [Fact]
    public async Task Buscar_por_id_inexistente_retorna_null()
    {
        var repo = CriarRepositorio();

        Assert.Null(await repo.ObterPorIdAsync("usuario-que-nao-existe"));
    }

    [Fact]
    public async Task Listar_sem_incluir_inativos_nao_traz_usuario_desativado()
    {
        var repo = CriarRepositorio();
        var ativo = Usuario.Criar(TenantA, "Ativa", "ativa@loja.com", "1111", Papel.Operator).Valor;
        var inativo = Usuario.Criar(TenantA, "Inativa", "inativa@loja.com", "2222", Papel.Operator).Valor;
        inativo.Desativar();

        await repo.SalvarAsync(ativo);
        await repo.SalvarAsync(inativo);

        var lista = await repo.ListarAsync(TenantA, incluirInativos: false);

        Assert.Single(lista);
        Assert.Equal(ativo.Id, lista[0].Id);
    }

    [Fact]
    public async Task Listar_incluindo_inativos_traz_todos_do_tenant()
    {
        var repo = CriarRepositorio();
        var ativo = Usuario.Criar(TenantA, "Ativa", "ativa2@loja.com", "3333", Papel.Operator).Valor;
        var inativo = Usuario.Criar(TenantA, "Inativa", "inativa2@loja.com", "4444", Papel.Operator).Valor;
        inativo.Desativar();

        await repo.SalvarAsync(ativo);
        await repo.SalvarAsync(inativo);

        var lista = await repo.ListarAsync(TenantA, incluirInativos: true);

        Assert.Equal(2, lista.Count);
    }

    [Fact]
    public async Task Listar_de_outro_tenant_nao_retorna_usuarios_do_tenant_a()
    {
        var repo = CriarRepositorio();
        var usuario = Usuario.Criar(TenantA, "Fulano", "fulano2@loja.com", "5555", Papel.Viewer).Valor;
        await repo.SalvarAsync(usuario);

        var lista = await repo.ListarAsync(TenantB, incluirInativos: true);

        Assert.Empty(lista);
    }

    [Fact]
    public async Task Salvar_de_novo_apos_mudanca_de_papel_e_status_reflete_o_novo_estado()
    {
        var repo = CriarRepositorio();
        var usuario = Usuario.Criar(TenantA, "Fulano", "fulano3@loja.com", "6666", Papel.Viewer).Valor;
        await repo.SalvarAsync(usuario);

        usuario.TrocarPapel(Papel.Admin);
        usuario.Desativar();
        await repo.SalvarAsync(usuario);

        var lido = await repo.ObterPorIdAsync(usuario.Id);
        Assert.Equal(Papel.Admin, lido!.Papel);
        Assert.Equal(StatusUsuario.Inativo, lido.Status);
    }

    [Fact]
    public async Task Redefinir_pin_persiste_o_novo_hash_e_invalida_o_pin_antigo()
    {
        var repo = CriarRepositorio();
        var usuario = Usuario.Criar(TenantA, "Fulano", "fulano4@loja.com", "7777", Papel.Manager).Valor;
        await repo.SalvarAsync(usuario);

        usuario.RedefinirPin("8888");
        await repo.SalvarAsync(usuario);

        var lido = await repo.ObterPorIdAsync(usuario.Id);
        Assert.False(lido!.VerificarPin("7777"));
        Assert.True(lido.VerificarPin("8888"));
    }
}
