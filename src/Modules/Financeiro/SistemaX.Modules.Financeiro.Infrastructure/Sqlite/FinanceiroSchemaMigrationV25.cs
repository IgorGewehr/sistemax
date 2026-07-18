using SistemaX.Infrastructure.Local.Migrations;

namespace SistemaX.Modules.Financeiro.Infrastructure.Sqlite;

/// <summary>
/// Migração v25 do módulo "financeiro" — P1-4 (docs/financeiro/revisao-domain-fit-cnpj.md): o
/// ledger append-only <c>mrr_movimentos</c>, LAR de <c>MovimentoMrr</c> — o painel de movimentos
/// que decompõe a variação do MRR em Novo/Expansão/Contração/Churn/Reativação, corrigindo o viés
/// de churn% de <c>ReceitaRecorrenteService</c> (snapshot algébrico) com soma cumulativa real.
/// </summary>
public sealed class FinanceiroSchemaMigrationV25 : SqlModuleSchemaMigration
{
    public override string Modulo => "financeiro";

    public override int Versao => 25;

    protected override string Sql =>
        """
        CREATE TABLE IF NOT EXISTS mrr_movimentos (
            id             TEXT PRIMARY KEY,
            business_id    TEXT NOT NULL,
            assinatura_id  TEXT NOT NULL,
            servico_id     TEXT NOT NULL,
            tipo           INTEGER NOT NULL,
            valor_centavos INTEGER NOT NULL,
            competencia    TEXT NOT NULL,
            ocorrido_em    TEXT NOT NULL
        );

        CREATE INDEX IF NOT EXISTS ix_mrr_movimentos_business_competencia ON mrr_movimentos (business_id, competencia);
        """;
}
