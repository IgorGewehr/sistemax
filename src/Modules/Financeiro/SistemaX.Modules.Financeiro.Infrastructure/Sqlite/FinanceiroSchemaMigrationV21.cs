using SistemaX.Infrastructure.Local.Migrations;

namespace SistemaX.Modules.Financeiro.Infrastructure.Sqlite;

/// <summary>
/// Migração v21 do módulo "financeiro" — fecha as duas lacunas documentadas de P1-7
/// (docs/financeiro/revisao-domain-fit-cnpj.md) em <c>ContaAReceber</c>: coluna <c>tecnico_id</c>
/// (permite consultar "qual técnico faturou esta OS" a partir do Financeiro — ainda NÃO gera
/// comissão sozinha, falta o cadastro de percentual por tenant) e colunas
/// <c>valor_servico_centavos</c>/<c>valor_pecas_centavos</c> (repartição mão de obra vs peças, só
/// para granularidade de relatório — a cobrança/parcela continua sobre o total). Todas NULLABLE,
/// sem backfill: diferente da dimensão "corrente" (V16), não existe pista retroativa confiável no
/// dado legado para inferir técnico ou repartição — linhas antigas ficam <c>NULL</c> nas três,
/// exatamente como as origens que nunca preenchem (venda, pedido, assinatura).
/// </summary>
public sealed class FinanceiroSchemaMigrationV21 : SqlModuleSchemaMigration
{
    public override string Modulo => "financeiro";

    public override int Versao => 21;

    protected override string Sql =>
        """
        ALTER TABLE contas_a_receber ADD COLUMN tecnico_id TEXT NULL;
        ALTER TABLE contas_a_receber ADD COLUMN valor_servico_centavos INTEGER NULL;
        ALTER TABLE contas_a_receber ADD COLUMN valor_pecas_centavos INTEGER NULL;
        """;
}
