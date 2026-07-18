using SistemaX.Infrastructure.Local.Migrations;

namespace SistemaX.Modules.Financeiro.Infrastructure.Sqlite;

/// <summary>
/// Migração v32 do módulo "financeiro" — espelha <see cref="FinanceiroSchemaMigrationV18"/> para
/// <c>movimentos_financeiros</c> (docs/financeiro/design-analise-por-projeto.md §3.2): coluna
/// <c>projeto_id</c> NULLABLE, sem backfill. Estorno herda (<c>MovimentoFinanceiro.GerarEstorno</c>
/// copia o campo, igual já copia <c>corrente</c>); <c>BaixarParcelaUseCase</c> propaga
/// <c>conta.ProjetoId</c> (e <c>conta.Corrente</c>, fechando de carona o gap documentado no design
/// §3.2 — <c>ProcessarLiquidacaoAsync</c> antes não propagava nenhum dos dois para o movimento).
/// </summary>
public sealed class FinanceiroSchemaMigrationV32 : SqlModuleSchemaMigration
{
    public override string Modulo => "financeiro";

    public override int Versao => 32;

    protected override string Sql =>
        """
        ALTER TABLE movimentos_financeiros ADD COLUMN projeto_id TEXT NULL;
        """;
}
