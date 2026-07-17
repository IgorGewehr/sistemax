namespace SistemaX.Infrastructure.Local.Migrations;

/// <summary>
/// Migração v2 do módulo "local" — F0 do plano de inteligência do Financeiro (ver
/// docs/financeiro/inteligencia-arquitetura.md e ADR-0005). Duas tabelas de infraestrutura
/// cross-módulo, no mesmo espírito de <see cref="LocalInfraSchemaMigration"/> (outbox/sequências):
///
/// <c>integration_events</c> — o ledger append-only, "a verdade histórica". <c>seq</c> é o
/// cursor sequencial (rowid autoincrement do SQLite — grátis, monotônico, nunca reamostrado);
/// <c>id</c> é o ULID de negócio da linha; <c>chave_idempotencia</c> é UNIQUE — é essa constraint
/// que torna <c>IIntegrationEventLedgerStore.AppendAsync</c> idempotente via
/// <c>ON CONFLICT DO NOTHING</c>, nunca uma checagem SELECT-then-INSERT sujeita a corrida.
///
/// <c>projection_state</c> — cursor por projeção (nome estável → último <c>seq</c> processado).
/// Reconstruir uma projeção do zero é sempre "zera a fact table + zera esta linha + reaplica
/// desde seq=0" (ADR-0005 §7).
///
/// NÃO reaproveita a versão 1 (<see cref="LocalInfraSchemaMigration"/>) porque uma migração já
/// aplicada em produção nunca deveria ter seu conteúdo (e portanto o checksum) editado — cada
/// necessidade nova de schema do módulo "local" ganha a PRÓXIMA versão.
/// </summary>
public sealed class IntegrationEventsSchemaMigration : SqlModuleSchemaMigration
{
    public override string Modulo => "local";

    public override int Versao => 2;

    protected override string Sql =>
        """
        CREATE TABLE IF NOT EXISTS integration_events (
            seq                 INTEGER PRIMARY KEY AUTOINCREMENT,
            id                  TEXT NOT NULL,
            tipo                TEXT NOT NULL,
            tenant_id           TEXT NOT NULL,
            payload_json        TEXT NOT NULL,
            ocorrido_em         TEXT NOT NULL,
            chave_idempotencia  TEXT NOT NULL,
            persistido_em_utc   INTEGER NOT NULL
        );
        CREATE UNIQUE INDEX IF NOT EXISTS ux_integration_events_id ON integration_events (id);
        CREATE UNIQUE INDEX IF NOT EXISTS ux_integration_events_chave_idempotencia ON integration_events (chave_idempotencia);
        CREATE INDEX IF NOT EXISTS ix_integration_events_tenant_tipo ON integration_events (tenant_id, tipo);
        CREATE INDEX IF NOT EXISTS ix_integration_events_tipo_seq ON integration_events (tipo, seq);

        CREATE TABLE IF NOT EXISTS projection_state (
            nome                      TEXT PRIMARY KEY,
            ultimo_cursor_processado  INTEGER NOT NULL DEFAULT 0,
            atualizado_em_utc         INTEGER NOT NULL
        );
        """;
}
