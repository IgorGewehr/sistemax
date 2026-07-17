using SistemaX.Modules.Vendas.Domain;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Vendas.Tests;

/// <summary>Atalhos comuns aos testes de <see cref="Venda"/> — evita repetir "abre venda, adiciona
/// 1 item" em toda classe de teste.</summary>
internal static class VendaTestBuilder
{
    public const string TenantId = "tenant-1";

    public static Venda AbrirComItem(
        Money preco, int quantidade = 1, string produtoId = "produto-1", string descricao = "Item de teste")
    {
        var venda = Venda.Abrir(TenantId);
        var resultado = venda.AdicionarItem(produtoId, descricao, quantidade, preco);
        if (resultado.Falha)
            throw new InvalidOperationException($"Falha ao montar cenário de teste: {resultado.Erro.Mensagem}");

        return venda;
    }
}
