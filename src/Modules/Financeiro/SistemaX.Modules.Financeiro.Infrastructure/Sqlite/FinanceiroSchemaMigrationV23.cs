using SistemaX.Infrastructure.Local.Migrations;

namespace SistemaX.Modules.Financeiro.Infrastructure.Sqlite;

/// <summary>
/// Migração v23 do módulo "financeiro" — P1-5 (docs/financeiro/revisao-domain-fit-cnpj.md), receita
/// diferida: <c>contas_a_receber</c> ganha <c>meses_de_reconhecimento</c> (NULLABLE, aditiva, sem
/// backfill — linhas antigas ficam <c>NULL</c> = reconhecimento imediato, o comportamento de
/// sempre). Só <c>Assinatura.GerarCobranca</c> preenche este campo hoje, para cobrança de ciclo
/// trimestral/semestral/anual — ver <see cref="Application.Quant.ReceitaReconhecidaResolver"/>.
/// </summary>
public sealed class FinanceiroSchemaMigrationV23 : SqlModuleSchemaMigration
{
    public override string Modulo => "financeiro";

    public override int Versao => 23;

    protected override string Sql =>
        """
        ALTER TABLE contas_a_receber ADD COLUMN meses_de_reconhecimento INTEGER NULL;
        """;
}
