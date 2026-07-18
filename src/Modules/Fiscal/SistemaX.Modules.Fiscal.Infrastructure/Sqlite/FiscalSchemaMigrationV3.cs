using SistemaX.Infrastructure.Local.Migrations;

namespace SistemaX.Modules.Fiscal.Infrastructure.Sqlite;

/// <summary>
/// Migração v3 do módulo "fiscal" — fecha dois gaps de docs/fiscal/emissao-mapping.md §11 que
/// V1/V2 deixaram de fora: (gap #2, metade restante) CSC (Código de Segurança do Contribuinte) em
/// <c>fiscal_configuracoes_tenant</c> — exigido pela SEFAZ para compor o QR Code/hash de toda
/// NFC-e; (gap #11, CCe) log de Carta de Correção Eletrônica — side-channel de texto, NUNCA uma
/// transição de <c>StatusDocumentoFiscal</c> (o agregado <c>Autorizado</c> continua imutável, ver
/// nota em <c>DocumentoFiscal.cs</c>). Nunca edite V1/V2, já aplicadas.
/// </summary>
public sealed class FiscalSchemaMigrationV3 : SqlModuleSchemaMigration
{
    public override string Modulo => "fiscal";

    public override int Versao => 3;

    protected override string Sql =>
        """
        ALTER TABLE fiscal_configuracoes_tenant ADD COLUMN csc_id TEXT;
        ALTER TABLE fiscal_configuracoes_tenant ADD COLUMN csc_token TEXT;

        CREATE TABLE IF NOT EXISTS fiscal_cartas_correcao (
            id                  TEXT PRIMARY KEY,
            tenant_id           TEXT NOT NULL,
            documento_fiscal_id TEXT NOT NULL REFERENCES fiscal_documentos(id) ON DELETE CASCADE,
            chave_acesso        TEXT NOT NULL,
            sequencia           INTEGER NOT NULL,
            texto               TEXT NOT NULL,
            registrado_em       INTEGER NOT NULL
        );
        CREATE INDEX IF NOT EXISTS ix_fiscal_cartas_correcao_documento
            ON fiscal_cartas_correcao (documento_fiscal_id, sequencia);
        """;
}
