using SistemaX.Infrastructure.Local.Migrations;

namespace SistemaX.Modules.Financeiro.Infrastructure.Sqlite;

/// <summary>
/// Migração v4 do módulo "financeiro" — o motor invisível de partida dobrada:
/// <c>lancamentos_contabeis</c> (header) + <c>partidas_contabeis</c> (filhas). AMBAS as tabelas
/// são INSERT-ONLY: <c>LancamentoContabil</c> é imutável por construção (a única correção é um
/// novo lançamento de estorno via <c>GerarEstorno</c>), então as partidas nunca são
/// deletadas/reinseridas como no par Conta/Parcela — apenas inseridas uma vez.
/// </summary>
public sealed class FinanceiroSchemaMigrationV4 : SqlModuleSchemaMigration
{
    public override string Modulo => "financeiro";

    public override int Versao => 4;

    protected override string Sql =>
        """
        CREATE TABLE IF NOT EXISTS lancamentos_contabeis (
            id                TEXT PRIMARY KEY,
            business_id       TEXT NOT NULL,
            data              TEXT NOT NULL,
            descricao         TEXT NOT NULL,
            origem_modulo     TEXT NOT NULL,
            origem_tipo_fato  TEXT NOT NULL,
            origem_id         TEXT NOT NULL,
            origem_chave      TEXT NOT NULL,
            reversal_of_id    TEXT,
            criado_em         TEXT NOT NULL
        );
        CREATE INDEX IF NOT EXISTS ix_lancamentos_contabeis_business ON lancamentos_contabeis (business_id);
        CREATE UNIQUE INDEX IF NOT EXISTS ux_lancamentos_contabeis_origem ON lancamentos_contabeis (business_id, origem_chave);

        CREATE TABLE IF NOT EXISTS partidas_contabeis (
            id                 TEXT PRIMARY KEY,
            lancamento_id      TEXT NOT NULL REFERENCES lancamentos_contabeis(id) ON DELETE CASCADE,
            ordem              INTEGER NOT NULL,
            conta_contabil_id  TEXT NOT NULL,
            natureza           INTEGER NOT NULL,
            valor_centavos     INTEGER NOT NULL,
            valor_moeda        TEXT NOT NULL
        );
        CREATE INDEX IF NOT EXISTS ix_partidas_contabeis_lancamento ON partidas_contabeis (lancamento_id);
        """;
}
