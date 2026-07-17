using SistemaX.Infrastructure.Local.Migrations;

namespace SistemaX.Modules.Compras.Infrastructure.Sqlite;

/// <summary>
/// Migração v1 do módulo "compras" — hoje só a tabela <c>fornecedores</c>, o primeiro dos 4
/// repositórios do módulo a ganhar persistência real (<c>NotaDeCompra</c> e
/// <c>VinculoProdutoFornecedor</c> seguem in-memory até a F1 portá-los). MOLDE: cada novo repo
/// SQLite deste módulo ganha sua PRÓPRIA versão aqui (v2, v3, ...) — nunca edite uma versão já
/// aplicada em produção, some com uma nova.
///
/// A unicidade "documento não-vazio não repete por tenant" é reforçada aqui como defesa em
/// profundidade (o dedupe de verdade é do caso de uso — <c>CadastrarFornecedorUseCase</c> — ver
/// nota em <see cref="Domain.Fornecedores.Fornecedor"/> sobre a fusão indevida por documento
/// vazio) via índice único PARCIAL (SQLite suporta <c>WHERE</c> em índice).
/// </summary>
public sealed class ComprasSchemaMigrationV1 : SqlModuleSchemaMigration
{
    public override string Modulo => "compras";

    public override int Versao => 1;

    protected override string Sql =>
        """
        CREATE TABLE IF NOT EXISTS fornecedores (
            id            TEXT PRIMARY KEY,
            tenant_id     TEXT NOT NULL,
            documento     TEXT,
            razao_social  TEXT NOT NULL,
            nome_fantasia TEXT,
            status        INTEGER NOT NULL
        );

        CREATE INDEX IF NOT EXISTS ix_fornecedores_tenant
            ON fornecedores (tenant_id);

        CREATE UNIQUE INDEX IF NOT EXISTS ux_fornecedores_tenant_documento
            ON fornecedores (tenant_id, documento)
            WHERE documento IS NOT NULL;
        """;
}
