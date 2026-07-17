using SistemaX.Infrastructure.Local.Migrations;

namespace SistemaX.Modules.Identidade.Infrastructure.Sqlite;

/// <summary>
/// Migração v1 do módulo "identidade" (ADR-0003) — a tabela <c>usuarios</c>. MOLDE: mesmo padrão
/// de <c>ComprasSchemaMigrationV1</c> — cada novo repo SQLite deste módulo ganha sua PRÓPRIA
/// versão aqui (v2, v3, ...), nunca editando uma versão já aplicada em produção.
///
/// PIN em si NUNCA é gravado — só o par hash/salt (<c>pin_hash</c>/<c>pin_salt</c>), PBKDF2 via
/// <c>PinHasher</c>. Não há índice de unicidade de PIN no schema (ao contrário do documento vs.
/// fornecedor): a unicidade de PIN entre usuários ATIVOS é reforçada no CASO DE USO
/// (<c>CriarUsuarioUseCase</c>/<c>AlterarUsuarioUseCase</c>), porque comparar hashes PBKDF2 não é
/// uma igualdade de coluna que o SQLite consiga indexar — cada candidato precisa ser verificado
/// via <c>PinHasher.Verificar</c> em memória.
/// </summary>
public sealed class IdentidadeSchemaMigrationV1 : SqlModuleSchemaMigration
{
    public override string Modulo => "identidade";

    public override int Versao => 1;

    protected override string Sql =>
        """
        CREATE TABLE IF NOT EXISTS usuarios (
            id                  TEXT PRIMARY KEY,
            business_id         TEXT NOT NULL,
            nome                TEXT NOT NULL,
            email               TEXT NOT NULL,
            papel               TEXT NOT NULL,
            status              TEXT NOT NULL,
            pin_hash            TEXT NOT NULL,
            pin_salt            TEXT NOT NULL,
            criado_em           TEXT NOT NULL,
            ultimo_acesso_em    TEXT NULL
        );

        CREATE INDEX IF NOT EXISTS ix_usuarios_business_id ON usuarios (business_id);
        """;
}
