using SistemaX.Infrastructure.Local.Migrations;

namespace SistemaX.Modules.Estoque.Infrastructure.Sqlite;

/// <summary>
/// Migração v2 do módulo "estoque" — <c>saldos_de_item</c>, o read-model persistido
/// (produto × depósito) descrito em <c>SaldoDeItem</c>. Chave primária composta (não é
/// <c>AggregateRoot</c>, não tem <c>Id</c> próprio — a identidade é tenant+produto+depósito).
/// </summary>
public sealed class EstoqueSchemaMigrationV2 : SqlModuleSchemaMigration
{
    public override string Modulo => "estoque";

    public override int Versao => 2;

    protected override string Sql =>
        """
        CREATE TABLE IF NOT EXISTS saldos_de_item (
            tenant_id             TEXT NOT NULL,
            produto_id            TEXT NOT NULL,
            deposito_id           TEXT NOT NULL,
            fisico_milesimos      INTEGER NOT NULL,
            reservado_milesimos   INTEGER NOT NULL,
            custo_medio_centavos  INTEGER NOT NULL,
            custo_medio_moeda     TEXT NOT NULL,
            ultimo_movimento_id   TEXT,
            PRIMARY KEY (tenant_id, produto_id, deposito_id)
        );
        """;
}
