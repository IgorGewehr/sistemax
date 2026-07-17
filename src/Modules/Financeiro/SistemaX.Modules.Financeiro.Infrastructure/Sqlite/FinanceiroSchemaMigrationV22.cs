using SistemaX.Infrastructure.Local.Migrations;

namespace SistemaX.Modules.Financeiro.Infrastructure.Sqlite;

/// <summary>
/// Migração v22 do módulo "financeiro" — mapeamento corrente→anexo do Radar do Simples,
/// CONFIGURÁVEL POR TENANT (P0-4, docs/financeiro/revisao-domain-fit-cnpj.md): o enquadramento de
/// Comércio/Serviço/Recorrente em Anexo I/III/V varia por CNPJ (contrato de assinatura, decisão do
/// contador etc.) — não é um fato legal fechado como as tabelas de faixa em si (essas continuam
/// hardcoded em <c>RadarDoSimplesNacional</c>, nunca configuráveis). Ausência de linha para um
/// tenant = "não personalizou" — <c>RadarDoSimplesService</c> cai para
/// <c>MapeamentoCorrenteAnexoPadrao</c> nesse caso.
/// </summary>
public sealed class FinanceiroSchemaMigrationV22 : SqlModuleSchemaMigration
{
    public override string Modulo => "financeiro";

    public override int Versao => 22;

    protected override string Sql =>
        """
        CREATE TABLE IF NOT EXISTS configuracao_radar_simples (
            business_id       TEXT PRIMARY KEY,
            mapeamento_json   TEXT NOT NULL,
            atualizado_em_utc INTEGER NOT NULL
        );
        """;
}
