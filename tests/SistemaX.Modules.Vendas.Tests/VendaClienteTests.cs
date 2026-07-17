using SistemaX.Modules.Vendas.Domain;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Vendas.Tests;

/// <summary>
/// <see cref="Venda.DefinirCliente"/> — companion dimensional da F0 do plano de inteligência do
/// Financeiro (docs/financeiro/inteligencia-arquitetura.md §3.3/ADR-0005): fecha o gap que
/// <c>VendaConcluida.ClienteId</c> documentava (agregado nunca capturava cliente). Mesma trava de
/// MONTAGEM vs PAGAMENTO das demais mutações de carrinho (ver <see cref="Venda"/>).
/// </summary>
public class VendaClienteTests
{
    [Fact]
    public void DefinirCliente_EmMontagem_AtribuiClienteId()
    {
        var venda = VendaTestBuilder.AbrirComItem(Money.DeReais(50));

        var resultado = venda.DefinirCliente("cliente-1");

        Assert.True(resultado.Sucesso);
        Assert.Equal("cliente-1", venda.ClienteId);
    }

    [Fact]
    public void DefinirCliente_ComNuloOuVazio_RemoveClienteJaVinculado()
    {
        var venda = VendaTestBuilder.AbrirComItem(Money.DeReais(50));
        venda.DefinirCliente("cliente-1");

        var resultado = venda.DefinirCliente(null);

        Assert.True(resultado.Sucesso);
        Assert.Null(venda.ClienteId);
    }

    [Fact]
    public void DefinirCliente_AposPrimeiroPagamento_Falha()
    {
        var venda = VendaTestBuilder.AbrirComItem(Money.DeReais(50));
        venda.RegistrarPagamento(MetodoPagamento.Pix, Money.DeReais(50), null, DateTimeOffset.UtcNow);

        var resultado = venda.DefinirCliente("cliente-1");

        Assert.True(resultado.Falha);
        Assert.Equal("venda.pagamento_ja_iniciado", resultado.Erro.Codigo);
    }

    [Fact]
    public void Concluir_ComClienteVinculado_PropagaClienteIdAoEventoDeIntegracao()
    {
        var venda = VendaTestBuilder.AbrirComItem(Money.DeReais(50));
        venda.DefinirCliente("cliente-1");
        venda.RegistrarPagamento(MetodoPagamento.Pix, Money.DeReais(50), null, DateTimeOffset.UtcNow);

        venda.Concluir();

        var domainEvent = venda.DomainEvents.OfType<VendaConcluidaDomainEvent>().Single();
        Assert.Equal("cliente-1", domainEvent.ClienteId);
        Assert.Equal("cliente-1", domainEvent.ParaEventoDeIntegracao().ClienteId);
    }

    [Fact]
    public void Concluir_SemClienteVinculado_PublicaClienteIdNulo()
    {
        var venda = VendaTestBuilder.AbrirComItem(Money.DeReais(50));
        venda.RegistrarPagamento(MetodoPagamento.Pix, Money.DeReais(50), null, DateTimeOffset.UtcNow);

        venda.Concluir();

        var domainEvent = venda.DomainEvents.OfType<VendaConcluidaDomainEvent>().Single();
        Assert.Null(domainEvent.ClienteId);
        Assert.Null(domainEvent.ParaEventoDeIntegracao().ClienteId);
    }
}
