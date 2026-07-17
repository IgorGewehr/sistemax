using SistemaX.Infrastructure.Local.Migrations;

namespace SistemaX.Modules.Vendas.Infrastructure.Sqlite;

/// <summary>
/// Migração v1 do módulo "vendas" — tabela <c>vendas</c> (cabeçalho) + <c>venda_itens</c> +
/// <c>venda_pagamentos</c> (filhos mutáveis do agregado, apagados/reinseridos a cada
/// <see cref="SqliteVendaRepository.SalvarAsync"/> — ver nota de MONTAGEM vs PAGAMENTO em
/// <c>Venda</c>). MOLDE seguido de docs/persistencia/persistencia-sqlite.md (F0, Fornecedor):
/// DDL idempotente, colunas snake_case, FK <c>ON DELETE CASCADE</c> só pai→filho do MESMO
/// agregado.
/// </summary>
public sealed class VendasSchemaMigrationV1 : SqlModuleSchemaMigration
{
    public override string Modulo => "vendas";

    public override int Versao => 1;

    protected override string Sql =>
        """
        CREATE TABLE IF NOT EXISTS vendas (
            id                      TEXT PRIMARY KEY,
            tenant_id               TEXT NOT NULL,
            status                  INTEGER NOT NULL,
            desconto_venda_centavos INTEGER NOT NULL,
            desconto_venda_moeda    TEXT NOT NULL,
            motivo_desconto_venda   TEXT
        );
        CREATE INDEX IF NOT EXISTS ix_vendas_tenant ON vendas (tenant_id);

        CREATE TABLE IF NOT EXISTS venda_itens (
            id                  TEXT PRIMARY KEY,
            venda_id            TEXT NOT NULL REFERENCES vendas(id) ON DELETE CASCADE,
            produto_id          TEXT NOT NULL,
            descricao           TEXT NOT NULL,
            quantidade          INTEGER NOT NULL,
            preco_unit_centavos INTEGER NOT NULL,
            preco_unit_moeda    TEXT NOT NULL,
            desconto_centavos   INTEGER NOT NULL,
            desconto_moeda      TEXT NOT NULL
        );
        CREATE INDEX IF NOT EXISTS ix_venda_itens_venda ON venda_itens (venda_id);

        CREATE TABLE IF NOT EXISTS venda_pagamentos (
            id                       TEXT PRIMARY KEY,
            venda_id                 TEXT NOT NULL REFERENCES vendas(id) ON DELETE CASCADE,
            metodo                   INTEGER NOT NULL,
            valor_centavos           INTEGER NOT NULL,
            valor_moeda              TEXT NOT NULL,
            valor_recebido_centavos  INTEGER,
            valor_recebido_moeda     TEXT,
            registrado_em            TEXT NOT NULL
        );
        CREATE INDEX IF NOT EXISTS ix_venda_pagamentos_venda ON venda_pagamentos (venda_id);
        """;
}
