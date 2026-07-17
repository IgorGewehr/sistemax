using SistemaX.Infrastructure.Local.Migrations;

namespace SistemaX.Modules.Financeiro.Infrastructure.Sqlite;

/// <summary>
/// Migração v13 do módulo "financeiro" — <c>formas_pagamento</c>: o LAR ÚNICO do MDR/lag por forma
/// de pagamento (docs/wiring/financeiro-telas-restantes.md §3). <c>FatoRecebiveisProjection</c>
/// passa a consultar esta tabela via <c>IFormaDePagamentoRepository.ObterPorNomeAsync</c> em vez da
/// antiga <c>ConfiguracaoDeRecebiveisOptions</c> (config estática, removida) — uma fonte da verdade.
/// </summary>
public sealed class FinanceiroSchemaMigrationV13 : SqlModuleSchemaMigration
{
    public override string Modulo => "financeiro";

    public override int Versao => 13;

    protected override string Sql =>
        """
        CREATE TABLE IF NOT EXISTS formas_pagamento (
            id                      TEXT PRIMARY KEY,
            business_id             TEXT NOT NULL,
            nome                    TEXT NOT NULL,
            tipo                    INTEGER NOT NULL,
            taxa_percentual         TEXT NOT NULL,
            prazo_compensacao_dias  INTEGER NOT NULL,
            conta_liquidacao_id     TEXT NULL,
            ativo                   INTEGER NOT NULL
        );

        CREATE INDEX IF NOT EXISTS ix_formas_pagamento_business
            ON formas_pagamento (business_id);

        CREATE INDEX IF NOT EXISTS ix_formas_pagamento_business_nome
            ON formas_pagamento (business_id, nome);
        """;
}
