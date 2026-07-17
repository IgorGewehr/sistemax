using SistemaX.Infrastructure.Local.Migrations;

namespace SistemaX.Modules.Estoque.Infrastructure.Sqlite;

/// <summary>
/// Migração v1 do módulo "estoque" — tabela <c>produtos</c> (catálogo) + as duas tabelas-filho
/// mutáveis (<c>produto_codigos_de_barras</c>, <c>produto_ficha_tecnica</c>), primeiro dos 3
/// repositórios do módulo a ganhar persistência real (segue o molde de
/// docs/persistencia/persistencia-sqlite.md). <c>SaldoDeItem</c> e <c>MovimentoDeEstoque</c> ganham
/// suas próprias versões (v2, v3) — nunca edite uma versão já aplicada em produção, some com uma
/// nova.
///
/// Os dois filhos não têm chave natural (<c>CodigoDeBarras</c>/<c>ComponenteDeFicha</c> são records
/// simples) — o <c>id</c> sintético é montado no repositório a partir das mesmas colunas que o
/// próprio agregado já usa como invariante de unicidade (<c>Produto.AdicionarCodigoDeBarras</c>
/// rejeita <c>Valor</c> duplicado; <c>Produto.Criar</c> rejeita <c>ProdutoInsumoId</c> duplicado na
/// ficha), então nunca colide dentro do mesmo produto.
/// </summary>
public sealed class EstoqueSchemaMigrationV1 : SqlModuleSchemaMigration
{
    public override string Modulo => "estoque";

    public override int Versao => 1;

    protected override string Sql =>
        """
        CREATE TABLE IF NOT EXISTS produtos (
            id                          TEXT PRIMARY KEY,
            tenant_id                   TEXT NOT NULL,
            sku                         TEXT NOT NULL,
            nome                        TEXT NOT NULL,
            descricao                   TEXT,
            categoria                   TEXT,
            unidade                     INTEGER NOT NULL,
            preco_venda_centavos        INTEGER NOT NULL,
            preco_venda_moeda           TEXT NOT NULL,
            fiscal_ncm                  TEXT,
            fiscal_cest                 TEXT,
            estoque_minimo_milesimos    INTEGER NOT NULL,
            ponto_reposicao_milesimos   INTEGER,
            lote_economico_milesimos    INTEGER,
            lead_time_dias              INTEGER,
            localizacao                 TEXT,
            controla_estoque            INTEGER NOT NULL,
            controle_por_lote           INTEGER NOT NULL,
            valorizacao                 INTEGER NOT NULL,
            ativo                       INTEGER NOT NULL
        );
        CREATE INDEX IF NOT EXISTS ix_produtos_tenant ON produtos (tenant_id);
        CREATE UNIQUE INDEX IF NOT EXISTS ux_produtos_tenant_sku ON produtos (tenant_id, sku);

        CREATE TABLE IF NOT EXISTS produto_codigos_de_barras (
            id          TEXT PRIMARY KEY,
            produto_id  TEXT NOT NULL REFERENCES produtos(id) ON DELETE CASCADE,
            valor       TEXT NOT NULL,
            tipo        INTEGER NOT NULL
        );
        CREATE INDEX IF NOT EXISTS ix_produto_codigos_produto ON produto_codigos_de_barras (produto_id);

        CREATE TABLE IF NOT EXISTS produto_ficha_tecnica (
            id                    TEXT PRIMARY KEY,
            produto_id            TEXT NOT NULL REFERENCES produtos(id) ON DELETE CASCADE,
            produto_insumo_id     TEXT NOT NULL,
            quantidade_milesimos  INTEGER NOT NULL
        );
        CREATE INDEX IF NOT EXISTS ix_produto_ficha_produto ON produto_ficha_tecnica (produto_id);
        """;
}
