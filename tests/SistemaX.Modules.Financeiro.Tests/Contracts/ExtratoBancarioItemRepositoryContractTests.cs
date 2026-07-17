using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Domain.Caixa;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Tests.Contracts;

/// <summary>
/// Contract test do port <see cref="IExtratoBancarioItemRepository"/>.
/// <see cref="IExtratoBancarioItemRepository.ListarNaoConciliadosAsync"/> REPLICA o comportamento
/// (surpreendente) do adapter in-memory hoje: não filtra por status de conciliação nenhum, só
/// por business_id + conta — ver nota em <c>InMemoryExtratoBancarioItemRepository</c>. O
/// contrato documenta o comportamento REAL, não o que o nome do método sugere.
/// </summary>
public abstract class ExtratoBancarioItemRepositoryContractTests
{
    protected const string BusinessA = "biz-a";
    protected const string BusinessB = "biz-b";

    protected abstract IExtratoBancarioItemRepository CriarRepositorio();

    [Fact]
    public async Task Salvar_e_buscar_por_identificador_externo_retorna_o_mesmo_item()
    {
        var repo = CriarRepositorio();
        var item = ExtratoBancarioItem.Importar(BusinessA, "conta-1", DateTimeOffset.UtcNow, Money.DeReais(150), "Pagamento recebido", "ofx-123");

        await repo.SalvarAsync(item);
        var lido = await repo.BuscarPorIdentificadorExternoAsync(BusinessA, "ofx-123");

        Assert.NotNull(lido);
        Assert.Equal(item.Id, lido!.Id);
        Assert.Equal(item.BusinessId, lido.BusinessId);
        Assert.Equal(item.ContaBancariaCaixaId, lido.ContaBancariaCaixaId);
        Assert.Equal(item.Data, lido.Data);
        Assert.Equal(item.Valor, lido.Valor);
        Assert.Equal(item.Descricao, lido.Descricao);
        Assert.Equal(item.IdentificadorExterno, lido.IdentificadorExterno);
    }

    [Fact]
    public async Task Buscar_por_identificador_externo_inexistente_retorna_null()
    {
        var repo = CriarRepositorio();
        Assert.Null(await repo.BuscarPorIdentificadorExternoAsync(BusinessA, "ofx-que-nao-existe"));
    }

    [Fact]
    public async Task Buscar_por_identificador_externo_de_outro_business_nao_retorna()
    {
        var repo = CriarRepositorio();
        var item = ExtratoBancarioItem.Importar(BusinessA, "conta-1", DateTimeOffset.UtcNow, Money.DeReais(150), "Pagamento", "ofx-456");
        await repo.SalvarAsync(item);

        Assert.Null(await repo.BuscarPorIdentificadorExternoAsync(BusinessB, "ofx-456"));
    }

    [Fact]
    public async Task ListarNaoConciliadosAsync_retorna_todos_os_itens_da_conta_independente_de_conciliacao()
    {
        var repo = CriarRepositorio();
        var item1 = ExtratoBancarioItem.Importar(BusinessA, "conta-1", DateTimeOffset.UtcNow, Money.DeReais(10), "Item 1", "ofx-1");
        var item2 = ExtratoBancarioItem.Importar(BusinessA, "conta-1", DateTimeOffset.UtcNow, Money.DeReais(20), "Item 2", "ofx-2");
        var itemOutraConta = ExtratoBancarioItem.Importar(BusinessA, "conta-2", DateTimeOffset.UtcNow, Money.DeReais(30), "Item 3", "ofx-3");
        await repo.SalvarAsync(item1);
        await repo.SalvarAsync(item2);
        await repo.SalvarAsync(itemOutraConta);

        var lista = await repo.ListarNaoConciliadosAsync(BusinessA, "conta-1");

        Assert.Equal(2, lista.Count);
        Assert.Contains(lista, i => i.Id == item1.Id);
        Assert.Contains(lista, i => i.Id == item2.Id);
        Assert.DoesNotContain(lista, i => i.Id == itemOutraConta.Id);
    }

    [Fact]
    public async Task Salvar_o_mesmo_item_duas_vezes_nao_duplica()
    {
        var repo = CriarRepositorio();
        var item = ExtratoBancarioItem.Importar(BusinessA, "conta-1", DateTimeOffset.UtcNow, Money.DeReais(10), "Item", "ofx-dup");

        await repo.SalvarAsync(item);
        await repo.SalvarAsync(item);

        var lista = await repo.ListarNaoConciliadosAsync(BusinessA, "conta-1");
        Assert.Single(lista);
    }
}
