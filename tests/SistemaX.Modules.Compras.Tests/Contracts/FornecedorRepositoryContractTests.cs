using SistemaX.Modules.Compras.Application.Ports;
using SistemaX.Modules.Compras.Domain.Fornecedores;

namespace SistemaX.Modules.Compras.Tests.Contracts;

/// <summary>
/// Contract test do port <see cref="IFornecedorRepository"/> — roda EXATAMENTE o mesmo conjunto de
/// casos contra QUALQUER adapter (hoje: <c>InMemoryFornecedorRepository</c> e
/// <c>SqliteFornecedorRepository</c>). Um port só está "pronto para SQLite" quando os mesmos casos
/// passam nos dois adapters.
///
/// MOLDE PARA A F1: para portar qualquer um dos outros 12 ports in-memory, copie esta classe
/// (trocando o port/agregado), mantendo os NOMES dos métodos — eles documentam o CONTRATO do
/// port, não a implementação de um adapter específico.
/// </summary>
public abstract class FornecedorRepositoryContractTests
{
    protected const string TenantA = "loja-a";
    protected const string TenantB = "loja-b";

    /// <summary>Cada subclasse decide o adapter (e, no caso do SQLite, prepara um banco/arquivo
    /// isolado por teste — xUnit cria uma instância nova da classe de teste por [Fact]).</summary>
    protected abstract IFornecedorRepository CriarRepositorio();

    [Fact]
    public async Task Salvar_e_buscar_por_id_retorna_o_mesmo_fornecedor()
    {
        var repo = CriarRepositorio();
        var fornecedor = Fornecedor.Cadastrar(TenantA, "Fornecedor Teste", "12345678000199", "Apelido").Valor;

        await repo.SalvarAsync(fornecedor);
        var lido = await repo.ObterPorIdAsync(fornecedor.Id);

        Assert.NotNull(lido);
        Assert.Equal(fornecedor.Id, lido!.Id);
        Assert.Equal(fornecedor.TenantId, lido.TenantId);
        Assert.Equal(fornecedor.RazaoSocial, lido.RazaoSocial);
        Assert.Equal(fornecedor.Documento, lido.Documento);
        Assert.Equal(fornecedor.NomeFantasia, lido.NomeFantasia);
        Assert.Equal(fornecedor.Status, lido.Status);
    }

    [Fact]
    public async Task Buscar_por_id_inexistente_retorna_null()
    {
        var repo = CriarRepositorio();

        Assert.Null(await repo.ObterPorIdAsync("fornecedor-que-nao-existe"));
    }

    [Fact]
    public async Task Salvar_e_buscar_por_documento_retorna_o_fornecedor()
    {
        var repo = CriarRepositorio();
        var fornecedor = Fornecedor.Cadastrar(TenantA, "Fornecedor Teste", "98765432000100").Valor;
        await repo.SalvarAsync(fornecedor);

        var lido = await repo.ObterPorDocumentoAsync(TenantA, "98765432000100");

        Assert.NotNull(lido);
        Assert.Equal(fornecedor.Id, lido!.Id);
    }

    [Fact]
    public async Task Buscar_por_documento_de_outro_tenant_nao_retorna()
    {
        var repo = CriarRepositorio();
        var fornecedor = Fornecedor.Cadastrar(TenantA, "Fornecedor Teste", "11122233000144").Valor;
        await repo.SalvarAsync(fornecedor);

        Assert.Null(await repo.ObterPorDocumentoAsync(TenantB, "11122233000144"));
    }

    [Fact]
    public async Task Salvar_de_novo_apos_mudanca_de_estado_reflete_o_novo_estado()
    {
        var repo = CriarRepositorio();
        var fornecedor = Fornecedor.Cadastrar(TenantA, "Fornecedor Teste").Valor;
        await repo.SalvarAsync(fornecedor);

        fornecedor.Bloquear();
        await repo.SalvarAsync(fornecedor);

        var lido = await repo.ObterPorIdAsync(fornecedor.Id);
        Assert.Equal(StatusFornecedor.Bloqueado, lido!.Status);
    }

    [Fact]
    public async Task Dois_fornecedores_sem_documento_persistem_como_registros_distintos()
    {
        // Espelha a lição real do gestao-raiz (ver Fornecedor.cs): dois fornecedores SEM
        // documento nunca podem colidir/se fundir na persistência.
        var repo = CriarRepositorio();
        var a = Fornecedor.Cadastrar(TenantA, "Produtor Rural A").Valor;
        var b = Fornecedor.Cadastrar(TenantA, "Produtor Rural B").Valor;

        await repo.SalvarAsync(a);
        await repo.SalvarAsync(b);

        var lidoA = await repo.ObterPorIdAsync(a.Id);
        var lidoB = await repo.ObterPorIdAsync(b.Id);

        Assert.NotNull(lidoA);
        Assert.NotNull(lidoB);
        Assert.Null(lidoA!.Documento);
        Assert.Null(lidoB!.Documento);
        Assert.NotEqual(lidoA.Id, lidoB.Id);
    }

    /// <summary>Read-model da tela de Fornecedores (achado de auditoria — ver
    /// <c>ComprasEndpointsModule</c>): sem <c>ListarAsync</c> o front não tinha como enumerar o
    /// cadastro, só resolvê-lo já sabendo id/documento.</summary>
    [Fact]
    public async Task Listar_retorna_so_fornecedores_do_tenant_ordenados_por_razao_social()
    {
        var repo = CriarRepositorio();
        await repo.SalvarAsync(Fornecedor.Cadastrar(TenantA, "Zeta Distribuidora").Valor);
        await repo.SalvarAsync(Fornecedor.Cadastrar(TenantA, "Alfa Comércio").Valor);
        await repo.SalvarAsync(Fornecedor.Cadastrar(TenantB, "Outro Tenant Ltda").Valor);

        var lista = await repo.ListarAsync(TenantA);

        Assert.Equal(2, lista.Count);
        Assert.Equal("Alfa Comércio", lista[0].RazaoSocial);
        Assert.Equal("Zeta Distribuidora", lista[1].RazaoSocial);
        Assert.All(lista, f => Assert.Equal(TenantA, f.TenantId));
    }
}
