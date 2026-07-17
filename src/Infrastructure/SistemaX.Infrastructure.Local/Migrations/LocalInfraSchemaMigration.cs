namespace SistemaX.Infrastructure.Local.Migrations;

/// <summary>
/// Migração versão 1 do módulo "local" — absorve o DDL que antes era aplicado direto por
/// <see cref="LocalSchemaMigrator"/> (outbox, sequências locais, kv, log de crash-recovery) para
/// dentro do fluxo versionado do <see cref="SchemaMigrationRunner"/>. Registrada automaticamente
/// por <c>AddSistemaXLocalInfrastructure</c> — nenhum host precisa (nem deve) registrá-la na mão.
/// </summary>
public sealed class LocalInfraSchemaMigration : SqlModuleSchemaMigration
{
    public override string Modulo => "local";

    public override int Versao => 1;

    protected override string Sql => LocalSchemaMigrator.Ddl;
}
