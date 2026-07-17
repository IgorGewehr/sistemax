namespace SistemaX.Infrastructure.Local.Outbox;

/// <summary>
/// Gera DDL de triggers SQLite (<c>AFTER INSERT/UPDATE/DELETE</c>) que populam
/// <c>outbox_messages</c> automaticamente a cada escrita numa tabela de negócio — a versão MAIS
/// FORTE do padrão "outbox transacional", direto no motor de banco em vez de depender de todo
/// caminho de código lembrar de chamar <c>ILocalUnitOfWork.EnqueueOutboxAsync</c>.
///
/// É opt-in: este projeto (Infrastructure.Local) não conhece as tabelas de negócio dos módulos
/// (Vendas, Estoque, ...), então não pode criar os triggers sozinho. Um módulo que queira a
/// garantia estrutural máxima chama <see cref="BuildCreateTriggersSql"/> na sua própria migração
/// de schema, passando o nome da tabela e das colunas que compõem o payload.
///
/// A chave de idempotência gerada pelo trigger usa <c>lower(hex(randomblob(16)))</c> — 128 bits
/// de aleatoriedade pura do SQLite, NUNCA <c>strftime('%s','now')</c> (a lição do Supermarket-OS:
/// timestamp com granularidade de 1 segundo colidia em updates em lote e, por estar sob um
/// UNIQUE INDEX, derrubava a transação inteira via rollback — inclusive a venda que disparou o
/// trigger). <c>randomblob</c> não é ordenável como um ULID, mas SQL puro não tem como computar
/// um ULID (precisaria de uma função customizada registrada via <c>CreateFunction</c>) — o
/// caminho ULID "de verdade" é o de <see cref="Ids.UlidGenerator"/>, usado pelo
/// <see cref="UnitOfWork.LocalUnitOfWork"/> ao enfileirar em código de aplicação.
/// </summary>
public static class OutboxTriggerFactory
{
    public static string BuildCreateTriggersSql(string tableName, string entityType, string idColumn, IReadOnlyList<string> payloadColumns)
    {
        var jsonObjectArgs = string.Join(", ", payloadColumns.Select(c => $"'{c}', NEW.{c}"));
        var jsonObjectArgsOld = string.Join(", ", payloadColumns.Select(c => $"'{c}', OLD.{c}"));

        return $"""
            CREATE TRIGGER IF NOT EXISTS trg_{tableName}_outbox_insert
            AFTER INSERT ON {tableName}
            BEGIN
                INSERT INTO outbox_messages (id, entity_type, entity_id, operation, payload_json, created_at_utc, status, attempts, next_attempt_at_utc, last_error)
                VALUES (lower(hex(randomblob(16))), '{entityType}', NEW.{idColumn}, 'Insert', json_object({jsonObjectArgs}), CAST((julianday('now') - 2440587.5) * 86400000 AS INTEGER), 'Pending', 0, NULL, NULL);
            END;

            CREATE TRIGGER IF NOT EXISTS trg_{tableName}_outbox_update
            AFTER UPDATE ON {tableName}
            BEGIN
                INSERT INTO outbox_messages (id, entity_type, entity_id, operation, payload_json, created_at_utc, status, attempts, next_attempt_at_utc, last_error)
                VALUES (lower(hex(randomblob(16))), '{entityType}', NEW.{idColumn}, 'Update', json_object({jsonObjectArgs}), CAST((julianday('now') - 2440587.5) * 86400000 AS INTEGER), 'Pending', 0, NULL, NULL);
            END;

            CREATE TRIGGER IF NOT EXISTS trg_{tableName}_outbox_delete
            AFTER DELETE ON {tableName}
            BEGIN
                INSERT INTO outbox_messages (id, entity_type, entity_id, operation, payload_json, created_at_utc, status, attempts, next_attempt_at_utc, last_error)
                VALUES (lower(hex(randomblob(16))), '{entityType}', OLD.{idColumn}, 'Delete', json_object({jsonObjectArgsOld}), CAST((julianday('now') - 2440587.5) * 86400000 AS INTEGER), 'Pending', 0, NULL, NULL);
            END;
            """;
    }
}
