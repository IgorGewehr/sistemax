using Microsoft.Extensions.Logging;

namespace SistemaX.Infrastructure.Sync.Conflict;

/// <summary>
/// Catálogo inicial de estratégias por entidade — mesma tabela do Supermarket-OS
/// (docs/robustez §3), adaptado ao vocabulário do SistemaX. Módulos futuros que precisem de uma
/// entidade fora deste catálogo devem estender via <see cref="IConflictResolutionPolicy"/>
/// próprio (decorator ou substituição total do registro em DI) — nunca editando "na mão" o
/// resultado de um conflito individual.
/// </summary>
public sealed class DefaultConflictResolutionPolicy(ILogger<DefaultConflictResolutionPolicy> logger) : IConflictResolutionPolicy
{
    private static readonly IReadOnlyDictionary<string, ConflictStrategy> Catalogo = new Dictionary<string, ConflictStrategy>(StringComparer.OrdinalIgnoreCase)
    {
        // Dinheiro real — terminal sempre vence.
        ["Venda"] = ConflictStrategy.TerminalWins,
        ["ItemVenda"] = ConflictStrategy.TerminalWins,
        ["Pagamento"] = ConflictStrategy.TerminalWins,
        ["SessaoCaixa"] = ConflictStrategy.TerminalWins,
        ["MovimentoCaixa"] = ConflictStrategy.TerminalWins,
        ["MovimentoEstoque"] = ConflictStrategy.TerminalWins, // é um LANÇAMENTO imutável, não o saldo agregado

        // Cadastro — servidor manda (com checagem de version).
        ["Produto"] = ConflictStrategy.ServerWinsWithVersion,
        ["Categoria"] = ConflictStrategy.ServerWinsWithVersion,
        ["Cliente"] = ConflictStrategy.ServerWinsWithVersion,
        ["Configuracao"] = ConflictStrategy.ServerWinsWithVersion,
        ["Funcionario"] = ConflictStrategy.ServerWinsWithVersion,
        ["Promocao"] = ConflictStrategy.ServerWinsWithVersion,

        // Contador agregado — soma de delta, nunca substituição.
        ["Estoque"] = ConflictStrategy.ReconcileDelta,
    };

    public ConflictStrategy StrategyFor(string entityType)
    {
        if (Catalogo.TryGetValue(entityType, out var strategy))
        {
            return strategy;
        }

        // Entidade não catalogada: provavelmente um módulo novo esqueceu de classificar seu
        // tipo. Default conservador (servidor manda, nunca sobrescreve sem version maior) — mas
        // TORNADO VISÍVEL em log, nunca um "provavelmente está certo" silencioso.
        logger.LogWarning(
            "Tipo de entidade '{EntityType}' sem estratégia de conflito catalogada — usando ServerWinsWithVersion por padrão. Cadastre-o em DefaultConflictResolutionPolicy ou substitua IConflictResolutionPolicy.",
            entityType);
        return ConflictStrategy.ServerWinsWithVersion;
    }
}
