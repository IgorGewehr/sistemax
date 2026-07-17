using Microsoft.Data.Sqlite;

namespace SistemaX.Infrastructure.Local;

/// <summary>
/// Fábrica central de conexões SQLite do terminal. TODA conexão aberta pelo processo — seja
/// para escrita de negócio (Unit-of-Work), leitura do outbox pela camada de Sync, ou consulta
/// administrativa — passa por aqui, garantindo que os pragmas (§ <see cref="SqlitePragmas"/>)
/// e o caminho do arquivo sejam sempre consistentes.
/// </summary>
public interface ILocalSqliteConnectionFactory
{
    /// <summary>Caminho absoluto do arquivo .db em uso (após eventual recuperação de corrupção).</summary>
    string DatabasePath { get; }

    /// <summary>Abre (assíncrono) e retorna uma conexão nova, já com os pragmas aplicados.</summary>
    Task<SqliteConnection> OpenConnectionAsync(CancellationToken ct = default);

    /// <summary>Versão síncrona — usar só em caminhos que já são síncronos por natureza (ex.: dispose/boot).</summary>
    SqliteConnection OpenConnection();
}
