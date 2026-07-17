using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Estoque.Application.Ports;
using SistemaX.Modules.Estoque.Domain.Catalogo;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Estoque.Application.CasosDeUso;

/// <summary>
/// Fecha o gap documentado em docs/fiscal/arquitetura.md §4: até aqui <c>Produto.Fiscal</c> só
/// era preenchido no <c>Criar</c>. Publica <c>ProdutoFiscalAtualizado</c> DEPOIS do commit local —
/// o Fiscal assina e mantém sua cópia local (<c>DadosFiscaisProdutoCache</c>), nunca lê este
/// produto por chamada síncrona cross-módulo (mesma disciplina R3/R5 do CLAUDE.md).
/// </summary>
public sealed class AtualizarDadosFiscaisProdutoUseCase(IProdutoRepository produtos, IIntegrationEventBus bus)
{
    public async Task<Result<Produto>> ExecutarAsync(
        string produtoId, string? ncm, string? cest, NaturezaOperacaoProduto naturezaOperacao,
        string? cfopOverride, DateTimeOffset ocorridoEm, CancellationToken ct = default)
    {
        var produto = await produtos.ObterPorIdAsync(produtoId, ct);
        if (produto is null)
            return Result.Falhar<Produto>(new Error("estoque.produto.nao_encontrado", $"Produto '{produtoId}' não encontrado."));

        var fiscal = new DadosFiscaisProduto(ncm, cest, naturezaOperacao, cfopOverride);
        var resultado = produto.AtualizarDadosFiscais(fiscal);
        if (resultado.Falha)
            return Result.Falhar<Produto>(resultado.Erro);

        await produtos.SalvarAsync(produto, ct); // commit local confirmado

        await bus.PublishAsync(new ProdutoFiscalAtualizado(
            produto.Id, produto.TenantId, ncm, cest, naturezaOperacao.ParaCodigo(), cfopOverride, ocorridoEm), ct);

        return Result.Ok(produto);
    }
}
