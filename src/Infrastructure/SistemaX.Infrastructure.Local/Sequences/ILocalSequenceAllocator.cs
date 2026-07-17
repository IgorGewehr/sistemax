using Microsoft.Data.Sqlite;

namespace SistemaX.Infrastructure.Local.Sequences;

/// <summary>
/// Aloca o próximo número de uma sequência LOCAL (ex.: número da venda de um caixa/registrador)
/// sem depender do servidor estar de pé — requisito duro de operação 100% offline (ver
/// docs/robustez §1). A colisão entre terminais (dois PDVs gerando o mesmo número enquanto
/// ambos offline) é reconciliada DEPOIS, do lado do servidor/nuvem — nunca aqui.
/// </summary>
public interface ILocalSequenceAllocator
{
    /// <summary>
    /// Aloca o próximo valor de <paramref name="sequenceName"/> DENTRO da transação informada —
    /// deve ser chamado a partir de um <see cref="SistemaX.Infrastructure.Local.UnitOfWork.ILocalUnitOfWork"/>
    /// já aberto, nunca em conexão própria, para que a alocação seja atômica com o resto da escrita.
    /// </summary>
    Task<long> NextAsync(SqliteConnection connection, SqliteTransaction transaction, string sequenceName, CancellationToken ct = default);
}
