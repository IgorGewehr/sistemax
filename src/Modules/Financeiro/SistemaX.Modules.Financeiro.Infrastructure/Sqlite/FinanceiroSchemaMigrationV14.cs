using SistemaX.Infrastructure.Local.Migrations;

namespace SistemaX.Modules.Financeiro.Infrastructure.Sqlite;

/// <summary>
/// Migração v14 do módulo "financeiro" — <c>sessoes_caixa</c>/<c>movimentos_sessao_caixa</c>: o LAR
/// ÚNICO do ritual de caixa físico (docs/wiring/financeiro-telas-restantes.md §4 — SessaoCaixa).
/// Duas tabelas porque a sessão tem uma lista de movimentos de tamanho variável (suprimento/
/// sangria/venda em espécie) — mesmo desenho relacional 1:N de <c>Parcela</c> dentro de
/// <c>ContaFinanceiraBase</c> (uma tabela pai + uma tabela filha com FK lógica por <c>sessao_id</c>,
/// sem FK física declarada — mesma convenção já usada nas demais tabelas do módulo, que também não
/// declaram FK entre si).
/// </summary>
public sealed class FinanceiroSchemaMigrationV14 : SqlModuleSchemaMigration
{
    public override string Modulo => "financeiro";

    public override int Versao => 14;

    protected override string Sql =>
        """
        CREATE TABLE IF NOT EXISTS sessoes_caixa (
            id                       TEXT PRIMARY KEY,
            business_id              TEXT NOT NULL,
            conta_caixa_id           TEXT NOT NULL,
            operador_id              TEXT NOT NULL,
            operador_nome            TEXT NOT NULL,
            aberta_em                TEXT NOT NULL,
            saldo_abertura_centavos  INTEGER NOT NULL,
            saldo_abertura_moeda     TEXT NOT NULL,
            status                   INTEGER NOT NULL,
            fechada_em               TEXT NULL,
            saldo_informado_centavos INTEGER NULL,
            saldo_informado_moeda    TEXT NULL
        );

        CREATE INDEX IF NOT EXISTS ix_sessoes_caixa_business
            ON sessoes_caixa (business_id);

        CREATE INDEX IF NOT EXISTS ix_sessoes_caixa_business_conta
            ON sessoes_caixa (business_id, conta_caixa_id);

        CREATE INDEX IF NOT EXISTS ix_sessoes_caixa_business_conta_status
            ON sessoes_caixa (business_id, conta_caixa_id, status);

        CREATE TABLE IF NOT EXISTS movimentos_sessao_caixa (
            id            TEXT PRIMARY KEY,
            sessao_id     TEXT NOT NULL,
            tipo          INTEGER NOT NULL,
            valor_centavos INTEGER NOT NULL,
            valor_moeda   TEXT NOT NULL,
            motivo        TEXT NULL,
            registrado_em TEXT NOT NULL,
            operador_id   TEXT NOT NULL,
            operador_nome TEXT NOT NULL
        );

        CREATE INDEX IF NOT EXISTS ix_movimentos_sessao_caixa_sessao
            ON movimentos_sessao_caixa (sessao_id);
        """;
}
