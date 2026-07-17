using SistemaX.Infrastructure.Local.Migrations;

namespace SistemaX.Modules.Fiscal.Infrastructure.Sqlite;

/// <summary>
/// Migração v1 do módulo "fiscal" — todas as tabelas do core tributário (docs/fiscal/arquitetura.md
/// §8), incluindo os 4 insumos do gateway de emissão (emitente/certificado/destinatário/pagamento
/// — gaps #1-#4 de docs/fiscal/emissao-mapping.md §11) que antes só existiam como adapter
/// InMemory. <c>sequencias_fiscais</c> NÃO tem tabela própria: a alocação atômica reusa
/// <c>local_sequences</c> (já criada pela migração "local") via <c>ILocalSequenceAllocator</c> —
/// ver <see cref="SqliteSequenciaFiscalRepository"/>.
/// </summary>
public sealed class FiscalSchemaMigrationV1 : SqlModuleSchemaMigration
{
    public override string Modulo => "fiscal";

    public override int Versao => 1;

    protected override string Sql =>
        """
        CREATE TABLE IF NOT EXISTS fiscal_configuracoes_tenant (
            tenant_id   TEXT PRIMARY KEY,
            regime      INTEGER NOT NULL,
            uf_origem   TEXT NOT NULL,
            serie_nfce  TEXT NOT NULL,
            serie_nfe   TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS fiscal_perfis_ncm (
            tenant_id                     TEXT NOT NULL,
            regime                        INTEGER NOT NULL,
            ncm                           TEXT NOT NULL,
            origem_mercadoria             INTEGER NOT NULL,
            exige_icms_st                 INTEGER NOT NULL,
            cest                          TEXT,
            aliquota_ipi_milionesimos     INTEGER,
            cst_csosn_pis_cofins          TEXT NOT NULL,
            aliquota_pis_milionesimos     INTEGER,
            aliquota_cofins_milionesimos  INTEGER,
            atualizado_em                 INTEGER NOT NULL,
            PRIMARY KEY (tenant_id, regime, ncm)
        );

        CREATE TABLE IF NOT EXISTS fiscal_tributacoes_produto (
            tenant_id                             TEXT NOT NULL,
            produto_id                            TEXT NOT NULL,
            origem_override                       INTEGER,
            exige_icms_st_override                INTEGER,
            cest_override                         TEXT,
            situacao_icms_override                TEXT,
            aliquota_icms_override_milionesimos   INTEGER,
            reducao_base_override_milionesimos    INTEGER,
            mva_override_milionesimos             INTEGER,
            aliquota_ipi_override_milionesimos    INTEGER,
            cst_csosn_pis_cofins_override         TEXT,
            aliquota_pis_override_milionesimos    INTEGER,
            aliquota_cofins_override_milionesimos INTEGER,
            motivo                                TEXT NOT NULL,
            atualizado_em                         INTEGER NOT NULL,
            PRIMARY KEY (tenant_id, produto_id)
        );

        CREATE TABLE IF NOT EXISTS fiscal_regras_operacao (
            id                                    TEXT PRIMARY KEY,
            tenant_id                             TEXT,
            regime                                INTEGER NOT NULL,
            tipo_operacao                         INTEGER NOT NULL,
            uf_origem                             TEXT NOT NULL,
            uf_destino                            TEXT,
            indicador_st                          INTEGER NOT NULL,
            situacao_tributaria                   TEXT NOT NULL,
            eh_csosn                              INTEGER NOT NULL,
            aliquota_interna_milionesimos         INTEGER,
            aliquota_interestadual_milionesimos   INTEGER,
            reducao_base_milionesimos             INTEGER,
            mva_milionesimos                      INTEGER,
            aliquota_fcp_milionesimos             INTEGER
        );
        CREATE INDEX IF NOT EXISTS ix_fiscal_regras_operacao_lookup
            ON fiscal_regras_operacao (regime, tipo_operacao, uf_origem);

        CREATE TABLE IF NOT EXISTS fiscal_regras_cfop (
            id                               TEXT PRIMARY KEY,
            tenant_id                        TEXT,
            tipo_operacao                    INTEGER NOT NULL,
            eh_interestadual                 INTEGER NOT NULL,
            destinatario_contribuinte_icms   INTEGER NOT NULL,
            natureza_operacao                INTEGER NOT NULL,
            cfop                             TEXT NOT NULL
        );
        CREATE INDEX IF NOT EXISTS ix_fiscal_regras_cfop_lookup
            ON fiscal_regras_cfop (tipo_operacao, eh_interestadual, destinatario_contribuinte_icms, natureza_operacao);

        CREATE TABLE IF NOT EXISTS fiscal_dados_produto_cache (
            tenant_id          TEXT NOT NULL,
            produto_id         TEXT NOT NULL,
            ncm                TEXT,
            cest               TEXT,
            natureza_operacao  TEXT NOT NULL,
            cfop_override      TEXT,
            PRIMARY KEY (tenant_id, produto_id)
        );

        CREATE TABLE IF NOT EXISTS fiscal_documentos (
            id             TEXT PRIMARY KEY,
            tenant_id      TEXT NOT NULL,
            tipo           INTEGER NOT NULL,
            origem_modulo  TEXT NOT NULL,
            origem_id      TEXT NOT NULL,
            status         INTEGER NOT NULL,
            serie          TEXT,
            numero         INTEGER,
            chave_acesso   TEXT,
            protocolo      TEXT,
            motivo         TEXT,
            criado_em      INTEGER NOT NULL
        );
        CREATE UNIQUE INDEX IF NOT EXISTS ux_fiscal_documentos_origem
            ON fiscal_documentos (tenant_id, origem_modulo, origem_id);
        CREATE INDEX IF NOT EXISTS ix_fiscal_documentos_tenant_status
            ON fiscal_documentos (tenant_id, status);

        CREATE TABLE IF NOT EXISTS fiscal_itens_documento (
            id                        TEXT PRIMARY KEY,
            documento_fiscal_id       TEXT NOT NULL REFERENCES fiscal_documentos(id) ON DELETE CASCADE,
            ordem                     INTEGER NOT NULL,
            produto_id                TEXT NOT NULL,
            descricao                 TEXT NOT NULL,
            ncm                       TEXT NOT NULL,
            cest                      TEXT,
            origem_mercadoria         INTEGER NOT NULL,
            cfop                      TEXT NOT NULL,
            quantidade_milesimos      INTEGER NOT NULL,
            preco_unitario_centavos   INTEGER NOT NULL,
            preco_unitario_moeda      TEXT NOT NULL,
            desconto_centavos         INTEGER NOT NULL,
            desconto_moeda            TEXT NOT NULL
        );
        CREATE INDEX IF NOT EXISTS ix_fiscal_itens_documento_documento
            ON fiscal_itens_documento (documento_fiscal_id);

        CREATE TABLE IF NOT EXISTS fiscal_tributos_item (
            item_id                    TEXT NOT NULL REFERENCES fiscal_itens_documento(id) ON DELETE CASCADE,
            tipo_tributo               INTEGER NOT NULL,
            situacao_tributaria        TEXT,
            base_centavos              INTEGER NOT NULL,
            base_moeda                 TEXT NOT NULL,
            aliquota_milionesimos      INTEGER NOT NULL,
            valor_centavos             INTEGER NOT NULL,
            valor_moeda                TEXT NOT NULL,
            reducao_base_milionesimos  INTEGER,
            mva_milionesimos           INTEGER
        );
        CREATE INDEX IF NOT EXISTS ix_fiscal_tributos_item_item
            ON fiscal_tributos_item (item_id);

        CREATE TABLE IF NOT EXISTS fiscal_cadastros_emitente (
            tenant_id           TEXT PRIMARY KEY,
            cnpj                TEXT NOT NULL,
            razao_social        TEXT NOT NULL,
            nome_fantasia       TEXT,
            inscricao_estadual  TEXT NOT NULL,
            inscricao_municipal TEXT,
            logradouro          TEXT NOT NULL,
            numero              TEXT NOT NULL,
            complemento         TEXT,
            bairro              TEXT NOT NULL,
            codigo_municipio    TEXT NOT NULL,
            municipio           TEXT NOT NULL,
            cep                 TEXT NOT NULL,
            telefone            TEXT
        );

        CREATE TABLE IF NOT EXISTS fiscal_certificados_digitais (
            tenant_id   TEXT PRIMARY KEY,
            pfx_base64  TEXT NOT NULL,
            senha       TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS fiscal_destinatarios_documento (
            documento_fiscal_id       TEXT PRIMARY KEY,
            cnpj                      TEXT,
            cpf                       TEXT,
            nome                      TEXT NOT NULL,
            email                     TEXT,
            inscricao_estadual        TEXT,
            endereco_logradouro       TEXT,
            endereco_numero           TEXT,
            endereco_complemento      TEXT,
            endereco_bairro           TEXT,
            endereco_codigo_municipio TEXT,
            endereco_municipio        TEXT,
            endereco_uf               TEXT,
            endereco_cep              TEXT
        );

        CREATE TABLE IF NOT EXISTS fiscal_formas_pagamento_documento (
            documento_fiscal_id TEXT NOT NULL,
            ordem                INTEGER NOT NULL,
            metodo               TEXT NOT NULL,
            valor_centavos       INTEGER NOT NULL,
            valor_moeda          TEXT NOT NULL,
            PRIMARY KEY (documento_fiscal_id, ordem)
        );
        """;
}
