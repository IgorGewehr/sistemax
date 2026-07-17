using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;

namespace SistemaX.Infrastructure.Local.Migrations;

/// <summary>
/// Uma migração de schema de UM módulo, numa versão. Cada módulo de negócio (Financeiro, Vendas,
/// Estoque, Compras, ...) registra a(s) sua(s) via DI — ver
/// <c>ServiceCollectionExtensions.AddModuleSchemaMigration&lt;T&gt;()</c> — e o
/// <see cref="SchemaMigrationRunner"/> as descobre via <c>IEnumerable&lt;IModuleSchemaMigration&gt;</c>.
/// Este projeto NUNCA conhece o schema de negócio, só a ordem/mecânica de aplicar (mesma filosofia
/// de <c>IModule</c> em <c>SistemaX.Modules.Abstractions</c>: Core descobre via DI, nunca conhece
/// o tipo concreto).
///
/// Para o caso comum (DDL puro, "CREATE TABLE IF NOT EXISTS ..."/"CREATE INDEX IF NOT EXISTS ..."),
/// herde de <see cref="SqlModuleSchemaMigration"/> em vez de implementar esta interface direto —
/// é o molde que a F1 deve copiar para as migrações de Estoque/Vendas/Financeiro/Assistência
/// restantes (ver docs/persistencia/persistencia-sqlite.md).
/// </summary>
public interface IModuleSchemaMigration
{
    /// <summary>Código estável do módulo dono (ex.: "compras", "estoque", "local" para a infra
    /// deste projeto) — chave, junto com <see cref="Versao"/>, em <c>schema_migrations</c>.</summary>
    string Modulo { get; }

    /// <summary>
    /// Versão desta migração dentro do módulo. O runner aplica em ordem crescente por módulo;
    /// uma versão já aplicada nunca é reaplicada. Uma versão PERSISTIDA maior que a maior
    /// declarada no código é tratada como downgrade — erro fatal no boot (nunca degradação
    /// silenciosa: um binário mais antigo rodando sobre um banco mais novo é bug de deployment).
    /// </summary>
    int Versao { get; }

    /// <summary>
    /// Hash estável do conteúdo desta migração, gravado em <c>schema_migrations.checksum</c>.
    /// Serve de rastro forense (uma migração "fechada" — já aplicada em produção — nunca deveria
    /// ter seu conteúdo editado; validar o checksum contra o gravado fica para a F1/F3, aqui só
    /// se grava).
    /// </summary>
    string Checksum { get; }

    /// <summary>
    /// Aplica a migração. Chamado pelo <see cref="SchemaMigrationRunner"/> DENTRO de uma
    /// transação já aberta — a migração nunca abre nem confirma sua própria transação.
    /// </summary>
    Task AplicarAsync(SqliteConnection connection, SqliteTransaction transaction, CancellationToken ct);
}

/// <summary>
/// Base para o caso comum de migração: um bloco de SQL DDL idempotente e determinístico. É o
/// molde-padrão — a MAIORIA das migrações de módulo deveria só herdar isto e declarar
/// <see cref="Modulo"/>/<see cref="Versao"/>/<see cref="Sql"/>, sem tocar em ADO.NET.
/// </summary>
public abstract class SqlModuleSchemaMigration : IModuleSchemaMigration
{
    public abstract string Modulo { get; }

    public abstract int Versao { get; }

    /// <summary>O DDL desta migração — texto ESTÁTICO (nunca gerado a partir de dado em runtime;
    /// o checksum só é estável se o texto for sempre o mesmo para o mesmo binário).</summary>
    protected abstract string Sql { get; }

    public string Checksum => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Sql)));

    public async Task AplicarAsync(SqliteConnection connection, SqliteTransaction transaction, CancellationToken ct)
    {
        await using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = Sql;

        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }
}
