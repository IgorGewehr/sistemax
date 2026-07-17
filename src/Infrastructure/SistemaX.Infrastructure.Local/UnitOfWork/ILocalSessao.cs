using SistemaX.Infrastructure.Local.Outbox;

namespace SistemaX.Infrastructure.Local.UnitOfWork;

/// <summary>
/// Unidade de trabalho AMBIENTE (escopo por requisição/caso de uso) que os CASOS DE USO iniciam
/// explicitamente e os REPOSITÓRIOS SQLite consultam implicitamente. Existe para resolver "qual
/// conexão/transação usar" sem que a sessão vire parâmetro de cada método dos ports — o que
/// mudaria a assinatura dos 14 ports existentes (Financeiro, Vendas, Estoque, Compras).
///
/// Uso típico num caso de uso que grava fato + publica evento (ver docs/persistencia):
/// <code>
/// await sessao.IniciarAsync(ct);
/// await repositorio.SalvarAsync(agregado, ct);              // participa da MESMA transação
/// await sessao.EnqueueOutboxAsync("Fornecedor", id, OutboxOperation.Update, payload, ct);
/// await sessao.CommitAsync(ct);
/// await bus.PublishAsync(evento, ct);                       // SÓ depois do commit — R3/R5
/// </code>
///
/// Um repositório SQLite consulta <see cref="Atual"/>: se não-nulo, participa da transação em
/// andamento; se nulo (nenhum caso de uso iniciou sessão — ex.: uma leitura solta fora de um caso
/// de uso orquestrado), abre sua própria conexão curta (barato com WAL).
/// </summary>
public interface ILocalSessao
{
    /// <summary>A transação em andamento nesta sessão/escopo, ou <c>null</c> se nenhuma foi
    /// iniciada ainda.</summary>
    ILocalUnitOfWork? Atual { get; }

    /// <summary>Inicia (ou reusa, se já iniciada) a transação ambiente desta sessão. Chamado pelo
    /// caso de uso ANTES de qualquer chamada a repositório que precise participar da transação.</summary>
    Task<ILocalUnitOfWork> IniciarAsync(CancellationToken ct = default);

    /// <summary>Atalho para <see cref="ILocalUnitOfWork.EnqueueOutboxAsync"/> na sessão atual.
    /// Lança se nenhuma sessão foi iniciada — enfileirar outbox sem transação ativa não tem
    /// sentido (perderia a atomicidade com o dado de negócio).</summary>
    Task EnqueueOutboxAsync(string entityType, string entityId, OutboxOperation operation, object payload, CancellationToken ct = default);

    /// <summary>Confirma a transação ambiente. Só o CASO DE USO chama isto — nunca um repositório.</summary>
    Task CommitAsync(CancellationToken ct = default);

    /// <summary>Desfaz a transação ambiente, se houver uma. No-op se nenhuma sessão foi iniciada.</summary>
    Task RollbackAsync(CancellationToken ct = default);
}
