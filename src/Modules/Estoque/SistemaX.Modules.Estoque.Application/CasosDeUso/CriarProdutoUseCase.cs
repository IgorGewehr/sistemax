using SistemaX.Modules.Estoque.Application.Ports;
using SistemaX.Modules.Estoque.Domain.Catalogo;
using SistemaX.Modules.Estoque.Domain.Comum;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Estoque.Application.CasosDeUso;

/// <summary>
/// Cadastra um produto no catálogo — "busca nada, chama a factory do agregado, salva", o mesmo
/// padrão de <see cref="RegistrarEntradaManualUseCase"/>. Existe como caso de uso dedicado (em vez
/// de <c>Produto.Criar</c> + <c>repo.SalvarAsync</c> direto no endpoint) para o cadastro de produto
/// ter o mesmo tratamento de qualquer outra escrita do módulo — cobertura de teste incluída (ver
/// <c>CasosDeUsoManuaisTests</c>).
/// </summary>
public sealed class CriarProdutoUseCase(IProdutoRepository produtos)
{
    public async Task<Result<Produto>> ExecutarAsync(
        string tenantId,
        string nome,
        UnidadeDeMedida unidade,
        string? sku = null,
        Money? precoVenda = null,
        string? categoria = null,
        bool controlaEstoque = true,
        Quantidade? estoqueMinimo = null,
        CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(sku))
        {
            var existente = await produtos.ObterPorSkuAsync(tenantId, sku, ct).ConfigureAwait(false);
            if (existente is not null)
                return Result.Falhar<Produto>(new Error("estoque.produto.sku_duplicado", $"Já existe um produto com o SKU '{sku}' neste tenant."));
        }

        var resultado = Produto.Criar(
            tenantId, nome, unidade,
            sku: sku, precoVenda: precoVenda, categoria: categoria,
            controlaEstoque: controlaEstoque, estoqueMinimo: estoqueMinimo);

        if (resultado.Falha)
            return resultado;

        await produtos.SalvarAsync(resultado.Valor, ct).ConfigureAwait(false);
        return resultado;
    }
}
