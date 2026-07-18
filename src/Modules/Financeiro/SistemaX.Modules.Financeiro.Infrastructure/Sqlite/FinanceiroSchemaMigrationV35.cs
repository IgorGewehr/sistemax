using SistemaX.Infrastructure.Local.Migrations;

namespace SistemaX.Modules.Financeiro.Infrastructure.Sqlite;

/// <summary>
/// Migração v35 do módulo "financeiro" — Análise por Projeto/Imobilizado, Parte B (P3,
/// docs/financeiro/design-analise-por-projeto.md §3.3/§7, docs/financeiro/design-imobilizado-roi.md
/// §3.1/§9): a tabela <c>ativos_de_capital</c>, o agregado GERAL (Natureza Tangível/Intangível) que
/// cobre tanto o caso DigiSat (licença amortizável tageada por projeto) quanto a futura fatia de
/// Imobilizado (equipamento/reforma/móveis, sem projeto) — mesma tabela, sem refactor.
/// </summary>
public sealed class FinanceiroSchemaMigrationV35 : SqlModuleSchemaMigration
{
    public override string Modulo => "financeiro";

    public override int Versao => 35;

    protected override string Sql =>
        """
        CREATE TABLE IF NOT EXISTS ativos_de_capital (
            id                              TEXT PRIMARY KEY,
            business_id                     TEXT NOT NULL,
            projeto_id                      TEXT NULL,
            nome                            TEXT NOT NULL,
            natureza                        INTEGER NOT NULL,
            categoria                       INTEGER NOT NULL,
            custo_aquisicao_centavos        INTEGER NOT NULL,
            valor_residual_centavos         INTEGER NOT NULL,
            data_aquisicao                  TEXT NOT NULL,
            inicio_depreciacao              TEXT NOT NULL,
            vida_util_meses                 INTEGER NOT NULL,
            metodo                          INTEGER NOT NULL,
            quantidade_unidades             INTEGER NOT NULL,
            conta_a_pagar_id                TEXT NULL,
            status                          INTEGER NOT NULL,
            ultima_competencia_reconhecida  TEXT NULL,
            encerrado_em                    TEXT NULL,
            baixado_em                      TEXT NULL,
            motivo_baixa                    TEXT NULL,
            valor_reconhecido_na_baixa_centavos INTEGER NULL,
            valor_venda_centavos            INTEGER NULL,
            criado_em                       TEXT NOT NULL
        );

        CREATE INDEX IF NOT EXISTS ix_ativos_de_capital_business_status ON ativos_de_capital (business_id, status);

        CREATE INDEX IF NOT EXISTS ix_ativos_de_capital_business_projeto ON ativos_de_capital (business_id, projeto_id);
        """;
}
