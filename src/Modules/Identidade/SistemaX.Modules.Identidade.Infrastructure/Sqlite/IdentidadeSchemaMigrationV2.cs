using SistemaX.Infrastructure.Local.Migrations;

namespace SistemaX.Modules.Identidade.Infrastructure.Sqlite;

/// <summary>
/// Migração v2 do módulo "identidade" — <c>pin_provisorio</c> (wizard de 1º-boot): ALTER aditivo
/// com <c>DEFAULT 0</c> que reproduz o comportamento de hoje para toda linha já existente (nenhum
/// usuário já persistido vira "PIN provisório" por acidente — só o founder recém-semeado, que
/// grava a coluna explicitamente via <see cref="SqliteUsuarioRepository.SalvarAsync"/>).
/// </summary>
public sealed class IdentidadeSchemaMigrationV2 : SqlModuleSchemaMigration
{
    public override string Modulo => "identidade";

    public override int Versao => 2;

    protected override string Sql =>
        """
        ALTER TABLE usuarios ADD COLUMN pin_provisorio INTEGER NOT NULL DEFAULT 0;
        """;
}
