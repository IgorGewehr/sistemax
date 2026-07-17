using SistemaX.Infrastructure.Local.Migrations;

namespace SistemaX.Modules.Fiscal.Infrastructure.Sqlite;

/// <summary>
/// Migração v2 do módulo "fiscal" — fecha dois gaps de persistência de
/// docs/fiscal/emissao-mapping.md §11 que a V1 deixou de fora: (gap #6) GTIN/unidade comercial
/// por produto em <c>fiscal_dados_produto_cache</c>; (gap #5) referência à NF-e original de
/// devolução em uma tabela nova, mesma convenção de <c>fiscal_destinatarios_documento</c>/
/// <c>fiscal_formas_pagamento_documento</c> (chaveada só por <c>documento_fiscal_id</c>, sem FK
/// de propósito). Nunca edite V1, já aplicada.
/// </summary>
public sealed class FiscalSchemaMigrationV2 : SqlModuleSchemaMigration
{
    public override string Modulo => "fiscal";

    public override int Versao => 2;

    protected override string Sql =>
        """
        ALTER TABLE fiscal_dados_produto_cache ADD COLUMN gtin TEXT;
        ALTER TABLE fiscal_dados_produto_cache ADD COLUMN unidade_comercial TEXT;

        CREATE TABLE IF NOT EXISTS fiscal_referencias_devolucao_documento (
            documento_fiscal_id TEXT PRIMARY KEY,
            ref_nfe             TEXT NOT NULL
        );
        """;
}
