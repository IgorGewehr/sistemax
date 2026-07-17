using SistemaX.Infrastructure.Local.Migrations;

namespace SistemaX.Modules.Estoque.Infrastructure.Sqlite;

/// <summary>
/// Migração v4 do módulo "estoque" — acrescenta a natureza da operação (produção própria vs
/// revenda de terceiros vs importação própria) e o override pontual de CFOP a
/// <c>produtos</c> (campo <c>DadosFiscaisProduto</c>, ver <c>Produto.cs</c>). Fecha o gap de CFOP
/// documentado em docs/fiscal/arquitetura.md §2.3/§9 — nunca edite V1/V2/V3, já aplicadas.
/// </summary>
public sealed class EstoqueSchemaMigrationV4 : SqlModuleSchemaMigration
{
    public override string Modulo => "estoque";

    public override int Versao => 4;

    protected override string Sql =>
        """
        ALTER TABLE produtos ADD COLUMN fiscal_natureza_operacao TEXT NOT NULL DEFAULT 'revenda_terceiros';
        ALTER TABLE produtos ADD COLUMN fiscal_cfop_override TEXT;
        """;
}
