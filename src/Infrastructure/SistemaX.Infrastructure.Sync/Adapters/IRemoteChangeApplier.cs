using SistemaX.Infrastructure.Sync.Conflict;
using SistemaX.Infrastructure.Sync.Model;

namespace SistemaX.Infrastructure.Sync.Adapters;

/// <summary>
/// Aplica uma <see cref="RemoteChange"/> recebida na tabela de negócio concreta. Este projeto
/// (Sync) não conhece o schema de "Venda" ou "Produto" — cada módulo dono de uma entidade
/// registra seu applier via DI (mesmo padrão de plugin de
/// <c>SistemaX.Modules.Abstractions.IIntegrationEventHandler&lt;TEvent&gt;</c>: o motor descobre
/// via <c>IEnumerable&lt;IRemoteChangeApplier&gt;</c> e casa por <see cref="EntityType"/>, nunca
/// via <c>if (entityType == "Produto")</c> hardcoded no motor).
///
/// <paramref name="strategy"/> já vem resolvida pelo <see cref="ConflictResolver"/> — o applier
/// só decide COMO aplicar dado a estratégia (ex.: em <see cref="ConflictStrategy.ReconcileDelta"/>,
/// chama <see cref="ConflictMath.ReconcileByDelta"/> com os valores extraídos do seu próprio payload).
/// </summary>
public interface IRemoteChangeApplier
{
    /// <summary>Tipo de entidade que este applier trata (ex.: "Venda", "Produto", "Estoque").</summary>
    string EntityType { get; }

    Task ApplyAsync(RemoteChange change, ConflictStrategy strategy, CancellationToken ct = default);
}
