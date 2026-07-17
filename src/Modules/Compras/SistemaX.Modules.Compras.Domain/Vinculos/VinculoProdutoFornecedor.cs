using SistemaX.Modules.Compras.Domain.Comum;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Compras.Domain.Vinculos;

/// <summary>
/// O DE-PARA aprendido (fornecedor + código do produto no fornecedor → produto do catálogo +
/// fator de conversão de unidade) — a peça central da superioridade do módulo sobre as duas
/// referências estudadas (plano §5/§6): nenhuma delas persiste isso. A 1ª nota de um fornecedor
/// exige conferência manual; da 2ª em diante, <c>RegistrarEntradaDeNotaUseCase</c> encontra este
/// vínculo e resolve o item como <see cref="Notas.MatchState.Auto"/> — zero interação.
///
/// <see cref="FatorConversaoMilesimos"/> é "quantas unidades de estoque (em milésimos) equivalem
/// a 1 unidade CHEIA da NF" — ex.: "1 CX = 12 UN" vira <c>12_000</c>. Unidade igual (1 UN NF = 1 UN
/// estoque) é <c>1_000</c>. Nunca zero/negativo — sem fator não há conversão seura (mesma lição do
/// gestao-raiz: falhar explícito é melhor que corromper quantidade).
///
/// Único por <c>(TenantId, FornecedorId, CodigoProdutoNoFornecedor)</c> — a Infrastructure
/// garante isso pela chave de armazenamento (não há update-in-place de Id: reaprender chama
/// <see cref="AtualizarMatch"/>, preservando o mesmo <see cref="Entity{TId}.Id"/>).
/// </summary>
public sealed class VinculoProdutoFornecedor : AggregateRoot<string>
{
    public string TenantId { get; private set; } = string.Empty;
    public string FornecedorId { get; private set; } = string.Empty;
    public string CodigoProdutoNoFornecedor { get; private set; } = string.Empty;
    public string ProdutoId { get; private set; } = string.Empty;
    public long FatorConversaoMilesimos { get; private set; }

    /// <summary>Nota de onde este vínculo foi aprendido/reaprendido pela última vez — auditável
    /// (a tela do fornecedor mostra "aprendido da NF X em DD/MM").</summary>
    public string AprendidoDaNotaId { get; private set; } = string.Empty;

    public DateTimeOffset AtualizadoEm { get; private set; }

    private VinculoProdutoFornecedor()
    {
    }

    public static Result<VinculoProdutoFornecedor> Criar(
        string tenantId, string fornecedorId, string codigoProdutoNoFornecedor, string produtoId,
        long fatorConversaoMilesimos, string aprendidoDaNotaId, DateTimeOffset? agora = null)
    {
        var validacao = Validar(codigoProdutoNoFornecedor, produtoId, fatorConversaoMilesimos);
        if (validacao.Falha) return Result.Falhar<VinculoProdutoFornecedor>(validacao.Erro);

        return Result.Ok(new VinculoProdutoFornecedor
        {
            Id = IdGenerator.NovoId(),
            TenantId = tenantId,
            FornecedorId = fornecedorId,
            CodigoProdutoNoFornecedor = codigoProdutoNoFornecedor,
            ProdutoId = produtoId,
            FatorConversaoMilesimos = fatorConversaoMilesimos,
            AprendidoDaNotaId = aprendidoDaNotaId,
            AtualizadoEm = agora ?? DateTimeOffset.UtcNow
        });
    }

    /// <summary>Reaprendizado — humano corrigiu produto/fator numa nota mais nova do mesmo
    /// fornecedor. Preserva <see cref="Entity{TId}.Id"/>: é o MESMO vínculo, novo estado, nunca um
    /// registro duplicado.</summary>
    public Result AtualizarMatch(string produtoId, long fatorConversaoMilesimos, string aprendidoDaNotaId, DateTimeOffset agora)
    {
        var validacao = Validar(CodigoProdutoNoFornecedor, produtoId, fatorConversaoMilesimos);
        if (validacao.Falha) return validacao;

        ProdutoId = produtoId;
        FatorConversaoMilesimos = fatorConversaoMilesimos;
        AprendidoDaNotaId = aprendidoDaNotaId;
        AtualizadoEm = agora;
        return Result.Ok();
    }

    private static Result Validar(string codigoProdutoNoFornecedor, string produtoId, long fatorConversaoMilesimos)
    {
        if (string.IsNullOrWhiteSpace(codigoProdutoNoFornecedor))
            return Result.Falhar(new Error("compras.vinculo.cprod_invalido", "Código do produto no fornecedor é obrigatório."));

        if (string.IsNullOrWhiteSpace(produtoId))
            return Result.Falhar(new Error("compras.vinculo.produto_invalido", "ProdutoId é obrigatório."));

        if (fatorConversaoMilesimos <= 0)
            return Result.Falhar(new Error("compras.vinculo.fator_invalido", "Fator de conversão deve ser maior que zero."));

        return Result.Ok();
    }
}
