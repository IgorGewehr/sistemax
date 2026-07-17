using SistemaX.Modules.Estoque.Application.Ports;
using SistemaX.Modules.Estoque.Domain.Comum;
using SistemaX.Modules.Estoque.Domain.Razao;
using SistemaX.Modules.Estoque.Domain.Saldos;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Estoque.Tests.Contracts;

/// <summary>
/// Contract test do port <see cref="ISaldoRepository"/> — mesmo molde de
/// <c>ProdutoRepositoryContractTests</c>/<c>FornecedorRepositoryContractTests</c> (Compras): os
/// NOMES dos métodos de teste documentam o CONTRATO do port, não a implementação de um adapter
/// específico.
/// </summary>
public abstract class SaldoRepositoryContractTests
{
    protected const string TenantA = "loja-a";
    protected const string TenantB = "loja-b";
    private const string Deposito = "principal";

    protected abstract ISaldoRepository CriarRepositorio();

    private static MovimentoDeEstoque MovimentoDeTeste(string tenantId, string produtoId, TipoMovimento tipo, Quantidade quantidade, Money custoUnitario, string chave)
        => MovimentoDeEstoque.Registrar(
            tenantId, Deposito, produtoId, tipo, quantidade, custoUnitario,
            new SourceRef("manual", chave), chave, "teste", "op-1", "Operador", DateTimeOffset.UtcNow).Valor;

    [Fact]
    public async Task Obter_de_chave_inexistente_retorna_null()
    {
        var repo = CriarRepositorio();

        Assert.Null(await repo.ObterAsync(TenantA, "produto-x", Deposito));
    }

    [Fact]
    public async Task ObterOuCriar_de_chave_inexistente_retorna_vazio_sem_persistir()
    {
        var repo = CriarRepositorio();

        var vazio = await repo.ObterOuCriarAsync(TenantA, "produto-x", Deposito);

        Assert.Equal(Quantidade.Zero, vazio.Fisico);
        Assert.Equal(Quantidade.Zero, vazio.Reservado);
        Assert.Null(vazio.UltimoMovimentoId);
        // O "ou criar" NUNCA persiste sozinho — só quem chama SalvarAsync explicitamente grava.
        Assert.Null(await repo.ObterAsync(TenantA, "produto-x", Deposito));
    }

    [Fact]
    public async Task Salvar_e_buscar_retorna_o_mesmo_saldo()
    {
        var repo = CriarRepositorio();
        var movimento = MovimentoDeTeste(TenantA, "produto-1", TipoMovimento.Entrada, Quantidade.DeInteiro(10), Money.DeReais(5m), "chave-1");
        var saldo = SaldoDeItem.Vazio(TenantA, "produto-1", Deposito);
        saldo.AplicarMovimento(movimento);

        await repo.SalvarAsync(saldo);
        var lido = await repo.ObterAsync(TenantA, "produto-1", Deposito);

        Assert.NotNull(lido);
        Assert.Equal(saldo.TenantId, lido!.TenantId);
        Assert.Equal(saldo.ProdutoId, lido.ProdutoId);
        Assert.Equal(saldo.DepositoId, lido.DepositoId);
        Assert.Equal(saldo.Fisico, lido.Fisico);
        Assert.Equal(saldo.Reservado, lido.Reservado);
        Assert.Equal(saldo.CustoMedio, lido.CustoMedio);
        Assert.Equal(saldo.UltimoMovimentoId, lido.UltimoMovimentoId);
    }

    [Fact]
    public async Task Salvar_de_novo_apos_segundo_movimento_reflete_os_novos_valores()
    {
        var repo = CriarRepositorio();
        var primeiro = MovimentoDeTeste(TenantA, "produto-1", TipoMovimento.Entrada, Quantidade.DeInteiro(10), Money.DeReais(5m), "chave-1");
        var saldo = SaldoDeItem.Vazio(TenantA, "produto-1", Deposito);
        saldo.AplicarMovimento(primeiro);
        await repo.SalvarAsync(saldo);

        var segundo = MovimentoDeTeste(TenantA, "produto-1", TipoMovimento.Saida, Quantidade.DeInteiro(4), Money.DeReais(5m), "chave-2");
        saldo.AplicarMovimento(segundo);
        await repo.SalvarAsync(saldo);

        var lido = await repo.ObterAsync(TenantA, "produto-1", Deposito);
        Assert.Equal(Quantidade.DeInteiro(6), lido!.Fisico);
        Assert.Equal(segundo.Id, lido.UltimoMovimentoId);
    }

    [Fact]
    public async Task Listar_retorna_apenas_saldos_do_tenant()
    {
        var repo = CriarRepositorio();
        var movimentoA = MovimentoDeTeste(TenantA, "produto-1", TipoMovimento.Entrada, Quantidade.DeInteiro(10), Money.DeReais(5m), "chave-a");
        var saldoA = SaldoDeItem.Vazio(TenantA, "produto-1", Deposito);
        saldoA.AplicarMovimento(movimentoA);

        var movimentoB = MovimentoDeTeste(TenantB, "produto-2", TipoMovimento.Entrada, Quantidade.DeInteiro(3), Money.DeReais(2m), "chave-b");
        var saldoB = SaldoDeItem.Vazio(TenantB, "produto-2", Deposito);
        saldoB.AplicarMovimento(movimentoB);

        await repo.SalvarAsync(saldoA);
        await repo.SalvarAsync(saldoB);

        var listaA = await repo.ListarAsync(TenantA);

        Assert.Single(listaA);
        Assert.Equal("produto-1", listaA[0].ProdutoId);
    }
}
