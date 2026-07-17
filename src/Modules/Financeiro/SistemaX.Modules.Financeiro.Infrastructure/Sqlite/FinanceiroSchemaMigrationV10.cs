using SistemaX.Infrastructure.Local.Migrations;

namespace SistemaX.Modules.Financeiro.Infrastructure.Sqlite;

/// <summary>
/// Migração v10 do módulo "financeiro" — <c>fato_margem_produto</c>, a fact table por PRODUTO da
/// F1 do plano de inteligência do Financeiro (docs/financeiro/inteligencia-arquitetura.md/
/// ADR-0005). Duas tabelas: a fact table achatada por (tenant_id, produto_id, dia), DESCARTÁVEL
/// por construção (mesmo racional de V8/V9 — <c>DROP</c> + replay do <c>ProjectionRunner</c> é a
/// correção canônica), e <c>analitico_margem_pendente_itens_venda</c>, o estado de TRANSIÇÃO entre
/// <c>VendaItensMovimentados</c> e <c>CustoBaixadoPorVenda</c> da mesma venda (ver XML doc de
/// <c>IFatoMargemProdutoRepository</c>/<c>FatoMargemProdutoProjection</c>) — também descartável:
/// um replay do zero recria e consome as linhas pendentes na mesma ordem, nunca deixando resíduo
/// divergente do fold incremental.
/// </summary>
public sealed class FinanceiroSchemaMigrationV10 : SqlModuleSchemaMigration
{
    public override string Modulo => "financeiro";

    public override int Versao => 10;

    protected override string Sql =>
        """
        CREATE TABLE IF NOT EXISTS fato_margem_produto (
            tenant_id         TEXT NOT NULL,
            produto_id        TEXT NOT NULL,
            dia               TEXT NOT NULL,
            receita_centavos  INTEGER NOT NULL DEFAULT 0,
            custo_centavos    INTEGER NOT NULL DEFAULT 0,
            atualizado_em_utc INTEGER NOT NULL,
            PRIMARY KEY (tenant_id, produto_id, dia)
        );

        CREATE TABLE IF NOT EXISTS analitico_margem_pendente_itens_venda (
            tenant_id              TEXT NOT NULL,
            venda_id               TEXT NOT NULL,
            produto_id             TEXT NOT NULL,
            dia                    TEXT NOT NULL,
            receita_item_centavos  INTEGER NOT NULL DEFAULT 0,
            PRIMARY KEY (tenant_id, venda_id, produto_id)
        );
        """;
}
