using SistemaX.Infrastructure.Local.Migrations;

namespace SistemaX.Modules.Vendas.Infrastructure.Sqlite;

/// <summary>
/// Migração v2 do módulo "vendas" — acrescenta <c>cliente_id</c> (nullable) ao cabeçalho
/// <c>vendas</c>. Companion dimensional da F0 do plano de inteligência do Financeiro
/// (docs/financeiro/inteligencia-arquitetura.md §3.3/ADR-0005): fecha o gap documentado em
/// <c>VendaConcluida.ClienteId</c> (Modules.Abstractions) — o carrinho do PDV passa a poder
/// vincular um cliente opcional (<c>Venda.DefinirCliente</c>). Molde de
/// <c>EstoqueSchemaMigrationV4</c> (ALTER TABLE ADD COLUMN sobre tabela já existente) — nunca edite
/// V1, já aplicada.
/// </summary>
public sealed class VendasSchemaMigrationV2 : SqlModuleSchemaMigration
{
    public override string Modulo => "vendas";

    public override int Versao => 2;

    protected override string Sql =>
        """
        ALTER TABLE vendas ADD COLUMN cliente_id TEXT;
        """;
}
