using SistemaX.Infrastructure.Local.Migrations;

namespace SistemaX.Modules.Financeiro.Infrastructure.Sqlite;

/// <summary>
/// Migração v24 do módulo "financeiro" — P1-4 (docs/financeiro/revisao-domain-fit-cnpj.md),
/// dunning: <c>assinaturas</c> ganha <c>inadimplente_desde</c> (NULLABLE, o relógio da política de
/// graça — <c>AvaliarDunningAssinaturasUseCase</c>). O novo status
/// <c>StatusAssinatura.Inadimplente</c> não precisa de migração própria — é só mais um valor do
/// enum já persistido como INTEGER na coluna <c>status</c> existente.
/// </summary>
public sealed class FinanceiroSchemaMigrationV24 : SqlModuleSchemaMigration
{
    public override string Modulo => "financeiro";

    public override int Versao => 24;

    protected override string Sql =>
        """
        ALTER TABLE assinaturas ADD COLUMN inadimplente_desde TEXT NULL;
        """;
}
