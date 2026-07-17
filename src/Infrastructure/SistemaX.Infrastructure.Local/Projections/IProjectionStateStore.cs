namespace SistemaX.Infrastructure.Local.Projections;

/// <summary>
/// Port do cursor persistido por projeção (tabela <c>projection_state</c>, schema em
/// <see cref="Migrations.IntegrationEventsSchemaMigration"/>). Puro detalhe de implementação do
/// <see cref="ProjectionRunner"/> — por isso vive aqui (Infrastructure.Local) e não no kernel
/// compartilhado, ao contrário de <c>IIntegrationEventLedgerStore</c>/<c>IProjection</c> (que
/// módulos de negócio implementam/consomem diretamente).
/// </summary>
public interface IProjectionStateStore
{
    /// <summary>Último cursor processado por esta projeção — 0 se a projeção nunca rodou.</summary>
    Task<long> ObterCursorAsync(string nomeProjecao, CancellationToken ct = default);

    /// <summary>Grava o cursor (upsert) — chamado após cada lote aplicado com sucesso, nunca no
    /// meio de um lote (se o processo cair a meio de um lote, o próximo boot reaplica o lote
    /// inteiro desde o último cursor salvo; <see cref="IProjection.AplicarAsync"/> precisa ser
    /// determinística para isso ser sempre seguro).</summary>
    Task SalvarCursorAsync(string nomeProjecao, long cursor, CancellationToken ct = default);
}
