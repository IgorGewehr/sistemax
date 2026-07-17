using SistemaX.Infrastructure.Local.Migrations;

namespace SistemaX.Modules.Estoque.Infrastructure.Sqlite;

/// <summary>
/// Migração v3 do módulo "estoque" — <c>movimentos_de_estoque</c>, o RAZÃO. APPEND-ONLY por
/// invariante do domínio (<c>MovimentoDeEstoque</c>/<c>IMovimentoRepository</c> não expõem
/// update/delete): nunca é alterada nem apagada depois de gravada, só recebe INSERT.
/// <c>ux_movimentos_chave_idempotencia</c> é o que torna reprocessar o mesmo evento de origem um
/// no-op (R3).
/// </summary>
public sealed class EstoqueSchemaMigrationV3 : SqlModuleSchemaMigration
{
    public override string Modulo => "estoque";

    public override int Versao => 3;

    protected override string Sql =>
        """
        CREATE TABLE IF NOT EXISTS movimentos_de_estoque (
            id                       TEXT PRIMARY KEY,
            tenant_id                TEXT NOT NULL,
            deposito_id              TEXT NOT NULL,
            produto_id               TEXT NOT NULL,
            tipo                     INTEGER NOT NULL,
            quantidade_milesimos     INTEGER NOT NULL,
            custo_unitario_centavos  INTEGER NOT NULL,
            custo_unitario_moeda     TEXT NOT NULL,
            origem_modulo            TEXT NOT NULL,
            origem_id                TEXT NOT NULL,
            origem_chave             TEXT NOT NULL,
            chave_idempotencia       TEXT NOT NULL,
            lote_id                  TEXT,
            motivo                   TEXT NOT NULL,
            operador_id              TEXT NOT NULL,
            operador_nome            TEXT NOT NULL,
            ocorrido_em              TEXT NOT NULL
        );
        CREATE INDEX IF NOT EXISTS ix_movimentos_tenant_produto_deposito ON movimentos_de_estoque (tenant_id, produto_id, deposito_id);
        CREATE INDEX IF NOT EXISTS ix_movimentos_tenant_periodo ON movimentos_de_estoque (tenant_id, ocorrido_em);
        CREATE UNIQUE INDEX IF NOT EXISTS ux_movimentos_chave_idempotencia ON movimentos_de_estoque (chave_idempotencia);
        CREATE INDEX IF NOT EXISTS ix_movimentos_origem ON movimentos_de_estoque (tenant_id, origem_modulo, origem_id);
        """;
}
