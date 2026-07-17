using SistemaX.Infrastructure.Local.Migrations;

namespace SistemaX.Modules.Financeiro.Infrastructure.Sqlite;

/// <summary>
/// Migração v15 do módulo "financeiro" — <c>assinaturas</c>: o LAR real de
/// <see cref="Domain.Assinaturas.Assinatura"/> (P0-3, docs/financeiro/revisao-domain-fit-cnpj.md).
/// Existia código de repositório SQLite pronto (<c>SqliteAssinaturaRepository</c>) desde antes desta
/// migração, mas nunca era wired em produção — <c>FinanceiroInfrastructureModule</c> registrava
/// SEMPRE o adapter in-memory, mesmo com <c>persistencia == "sqlite"</c>; a cada restart do host o
/// cron de faturamento (<c>GerarCobrancasAssinaturasBackgroundService</c>) não tinha nada pra
/// iterar. Esta migração fecha esse gap.
/// </summary>
public sealed class FinanceiroSchemaMigrationV15 : SqlModuleSchemaMigration
{
    public override string Modulo => "financeiro";

    public override int Versao => 15;

    protected override string Sql =>
        """
        CREATE TABLE IF NOT EXISTS assinaturas (
            id                  TEXT PRIMARY KEY,
            business_id         TEXT NOT NULL,
            cliente_id          TEXT NOT NULL,
            cliente_nome        TEXT NOT NULL,
            servico_id          TEXT NOT NULL,
            servico_nome        TEXT NOT NULL,
            valor_centavos      INTEGER NOT NULL,
            moeda               TEXT NOT NULL,
            ciclo               INTEGER NOT NULL,
            dia_cobranca        INTEGER NOT NULL,
            status              INTEGER NOT NULL,
            data_inicio         TEXT NOT NULL,
            cancelada_em        TEXT NULL,
            motivo_cancelamento TEXT NULL,
            ultima_cobranca_em  TEXT NULL
        );

        CREATE INDEX IF NOT EXISTS ix_assinaturas_business ON assinaturas (business_id);

        CREATE INDEX IF NOT EXISTS ix_assinaturas_business_status ON assinaturas (business_id, status);
        """;
}
