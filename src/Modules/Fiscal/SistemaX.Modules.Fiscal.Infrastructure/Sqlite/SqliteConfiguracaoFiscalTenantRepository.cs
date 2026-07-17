using Microsoft.Data.Sqlite;
using SistemaX.Infrastructure.Local;
using SistemaX.Infrastructure.Local.UnitOfWork;
using SistemaX.Modules.Fiscal.Application.Ports;
using SistemaX.Modules.Fiscal.Domain.Regimes;

namespace SistemaX.Modules.Fiscal.Infrastructure.Sqlite;

public sealed class SqliteConfiguracaoFiscalTenantRepository(ILocalSqliteConnectionFactory connectionFactory, ILocalSessao sessao)
    : IConfiguracaoFiscalTenantRepository
{
    public Task<ConfiguracaoFiscalTenant?> ObterAsync(string tenantId, CancellationToken ct = default)
        => SqliteSessaoHelper.ConsultarAsync(connectionFactory, sessao, async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = "SELECT tenant_id, regime, uf_origem, serie_nfce, serie_nfe FROM fiscal_configuracoes_tenant WHERE tenant_id = $tenantId;";
            cmd.Parameters.AddWithValue("$tenantId", tenantId);

            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            if (!await reader.ReadAsync(ct).ConfigureAwait(false)) return null;

            return new ConfiguracaoFiscalTenant(
                reader.GetString(0), (RegimeTributario)reader.GetInt32(1), reader.GetString(2), reader.GetString(3), reader.GetString(4));
        }, ct);

    public Task SalvarAsync(ConfiguracaoFiscalTenant configuracao, CancellationToken ct = default)
        => SqliteSessaoHelper.ExecutarAsync(connectionFactory, sessao, async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText =
                """
                INSERT INTO fiscal_configuracoes_tenant (tenant_id, regime, uf_origem, serie_nfce, serie_nfe)
                VALUES ($tenantId, $regime, $ufOrigem, $serieNfce, $serieNfe)
                ON CONFLICT(tenant_id) DO UPDATE SET
                    regime = excluded.regime, uf_origem = excluded.uf_origem,
                    serie_nfce = excluded.serie_nfce, serie_nfe = excluded.serie_nfe;
                """;
            cmd.Parameters.AddWithValue("$tenantId", configuracao.TenantId);
            cmd.Parameters.AddWithValue("$regime", (int)configuracao.Regime);
            cmd.Parameters.AddWithValue("$ufOrigem", configuracao.UfOrigem);
            cmd.Parameters.AddWithValue("$serieNfce", configuracao.SerieNfce);
            cmd.Parameters.AddWithValue("$serieNfe", configuracao.SerieNfe);
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }, ct);
}
