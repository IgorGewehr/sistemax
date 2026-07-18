using Microsoft.Data.Sqlite;
using SistemaX.Infrastructure.Local;
using SistemaX.Infrastructure.Local.UnitOfWork;
using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Domain.Configuracao;

namespace SistemaX.Modules.Financeiro.Infrastructure.Sqlite;

/// <summary>
/// Persistência REAL (SQLite) de <see cref="ConfiguracaoFinanceiraTenant"/> — espelho de
/// <c>SqliteConfiguracaoFiscalTenantRepository</c> (Fiscal). Schema nasce de
/// <see cref="FinanceiroSchemaMigrationV27"/>.
/// </summary>
public sealed class SqliteConfiguracaoFinanceiraTenantRepository(ILocalSqliteConnectionFactory connectionFactory, ILocalSessao sessao)
    : IConfiguracaoFinanceiraTenantRepository
{
    public Task<ConfiguracaoFinanceiraTenant?> ObterAsync(string tenantId, CancellationToken ct = default)
        => ConsultarAsync(async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText =
                "SELECT tenant_id, analise_por_projeto_ativa, custo_hora_padrao_centavos, tempo_entra_no_dre FROM configuracoes_financeiras WHERE tenant_id = $tenantId;";
            cmd.Parameters.AddWithValue("$tenantId", tenantId);

            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            if (!await reader.ReadAsync(ct).ConfigureAwait(false)) return null;

            return new ConfiguracaoFinanceiraTenant(
                reader.GetString(0), reader.GetInt32(1) != 0, reader.IsDBNull(2) ? null : reader.GetInt64(2), reader.GetInt32(3) != 0);
        }, ct);

    public Task SalvarAsync(ConfiguracaoFinanceiraTenant configuracao, CancellationToken ct = default)
        => ExecutarAsync(async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText =
                """
                INSERT INTO configuracoes_financeiras (tenant_id, analise_por_projeto_ativa, custo_hora_padrao_centavos, tempo_entra_no_dre)
                VALUES ($tenantId, $ativa, $custoHora, $tempoNoDre)
                ON CONFLICT(tenant_id) DO UPDATE SET
                    analise_por_projeto_ativa = excluded.analise_por_projeto_ativa,
                    custo_hora_padrao_centavos = excluded.custo_hora_padrao_centavos,
                    tempo_entra_no_dre = excluded.tempo_entra_no_dre;
                """;
            cmd.Parameters.AddWithValue("$tenantId", configuracao.TenantId);
            cmd.Parameters.AddWithValue("$ativa", configuracao.AnalisePorProjetoAtiva ? 1 : 0);
            cmd.Parameters.AddWithValue("$custoHora", (object?)configuracao.CustoHoraPadraoCentavos ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$tempoNoDre", configuracao.TempoEntraNoDre ? 1 : 0);
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }, ct);

    private async Task ExecutarAsync(Func<SqliteConnection, SqliteTransaction?, Task> acao, CancellationToken ct)
    {
        if (sessao.Atual is { } uow)
        {
            await acao(uow.Connection, uow.Transaction).ConfigureAwait(false);
            return;
        }

        await using var connection = await connectionFactory.OpenConnectionAsync(ct).ConfigureAwait(false);
        await acao(connection, null).ConfigureAwait(false);
    }

    private async Task<T> ConsultarAsync<T>(Func<SqliteConnection, SqliteTransaction?, Task<T>> consulta, CancellationToken ct)
    {
        if (sessao.Atual is { } uow)
        {
            return await consulta(uow.Connection, uow.Transaction).ConfigureAwait(false);
        }

        await using var connection = await connectionFactory.OpenConnectionAsync(ct).ConfigureAwait(false);
        return await consulta(connection, null).ConfigureAwait(false);
    }
}
