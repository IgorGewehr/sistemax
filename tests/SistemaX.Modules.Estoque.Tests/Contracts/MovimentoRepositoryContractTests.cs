using SistemaX.Modules.Estoque.Application.Ports;
using SistemaX.Modules.Estoque.Domain.Comum;
using SistemaX.Modules.Estoque.Domain.Razao;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Estoque.Tests.Contracts;

/// <summary>
/// Contract test do port <see cref="IMovimentoRepository"/> — o RAZÃO é append-only por invariante
/// de domínio (ver o próprio port): nenhum caso aqui exercita update/delete porque eles não
/// existem no contrato. Mesmo molde de <c>FornecedorRepositoryContractTests</c> (Compras).
/// </summary>
public abstract class MovimentoRepositoryContractTests
{
    protected const string TenantA = "loja-a";
    protected const string TenantB = "loja-b";
    private const string Deposito = "principal";

    protected abstract IMovimentoRepository CriarRepositorio();

    private static MovimentoDeEstoque MovimentoDeTeste(
        string tenantId, string produtoId, string chaveIdempotencia, string origemId, DateTimeOffset ocorridoEm, string depositoId = Deposito)
        => MovimentoDeEstoque.Registrar(
            tenantId, depositoId, produtoId, TipoMovimento.Entrada, Quantidade.DeInteiro(1), Money.DeReais(10m),
            new SourceRef("compras", origemId), chaveIdempotencia, "compra", "op-1", "Operador", ocorridoEm).Valor;

    [Fact]
    public async Task Salvar_e_existe_com_chave_roundtrip()
    {
        var repo = CriarRepositorio();
        var movimento = MovimentoDeTeste(TenantA, "produto-1", "chave-existente", "origem-1", DateTimeOffset.UtcNow);

        await repo.SalvarAsync(movimento);

        Assert.True(await repo.ExisteComChaveAsync("chave-existente"));
        Assert.False(await repo.ExisteComChaveAsync("chave-nao-relacionada"));
    }

    [Fact]
    public async Task ListarPorOrigem_filtra_por_tenant_e_chave_de_origem()
    {
        var repo = CriarRepositorio();
        var daOrigem = MovimentoDeTeste(TenantA, "produto-1", "chave-1", "venda-77", DateTimeOffset.UtcNow);
        var deOutraOrigem = MovimentoDeTeste(TenantA, "produto-1", "chave-2", "venda-78", DateTimeOffset.UtcNow);
        var deOutroTenant = MovimentoDeTeste(TenantB, "produto-1", "chave-3", "venda-77", DateTimeOffset.UtcNow);

        await repo.SalvarAsync(daOrigem);
        await repo.SalvarAsync(deOutraOrigem);
        await repo.SalvarAsync(deOutroTenant);

        var resultado = await repo.ListarPorOrigemAsync(TenantA, "compras:venda-77");

        Assert.Single(resultado);
        Assert.Equal(daOrigem.Id, resultado[0].Id);
    }

    [Fact]
    public async Task ListarPorProduto_filtra_por_tenant_produto_deposito_e_preserva_ordem_cronologica()
    {
        var repo = CriarRepositorio();
        var primeiro = MovimentoDeTeste(TenantA, "produto-1", "chave-1", "origem-1", DateTimeOffset.UtcNow.AddMinutes(-10));
        await repo.SalvarAsync(primeiro);

        // ULID não é estritamente monotônico dentro do mesmo milissegundo (a parte aleatória pode
        // ordenar antes ou depois) — o pequeno delay garante milissegundos distintos, condição sob
        // a qual a ordenação lexicográfica do ULID É cronológica (mesma premissa de
        // InMemoryMovimentoRepository.OrderBy(m => m.Id, StringComparer.Ordinal)).
        await Task.Delay(15);
        var segundo = MovimentoDeTeste(TenantA, "produto-1", "chave-2", "origem-2", DateTimeOffset.UtcNow);
        await repo.SalvarAsync(segundo);

        var deOutroDeposito = MovimentoDeTeste(TenantA, "produto-1", "chave-3", "origem-3", DateTimeOffset.UtcNow, depositoId: "outro-deposito");
        await repo.SalvarAsync(deOutroDeposito);

        var resultado = await repo.ListarPorProdutoAsync(TenantA, "produto-1", Deposito);

        Assert.Equal(2, resultado.Count);
        Assert.Equal(primeiro.Id, resultado[0].Id);
        Assert.Equal(segundo.Id, resultado[1].Id);
    }

    [Fact]
    public async Task ListarPorPeriodo_filtra_por_intervalo_de_datas()
    {
        var repo = CriarRepositorio();
        var referencia = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var dentro = MovimentoDeTeste(TenantA, "produto-1", "chave-1", "origem-1", referencia);
        var foraAntes = MovimentoDeTeste(TenantA, "produto-1", "chave-2", "origem-2", referencia.AddDays(-10));
        var foraDepois = MovimentoDeTeste(TenantA, "produto-1", "chave-3", "origem-3", referencia.AddDays(10));

        await repo.SalvarAsync(dentro);
        await repo.SalvarAsync(foraAntes);
        await repo.SalvarAsync(foraDepois);

        var resultado = await repo.ListarPorPeriodoAsync(TenantA, referencia.AddDays(-1), referencia.AddDays(1));

        Assert.Single(resultado);
        Assert.Equal(dentro.Id, resultado[0].Id);
    }
}
