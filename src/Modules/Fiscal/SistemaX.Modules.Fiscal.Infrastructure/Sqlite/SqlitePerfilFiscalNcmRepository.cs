using SistemaX.Infrastructure.Local;
using SistemaX.Infrastructure.Local.UnitOfWork;
using SistemaX.Modules.Fiscal.Application.Ports;
using SistemaX.Modules.Fiscal.Domain.Comum;
using SistemaX.Modules.Fiscal.Domain.Ncm;
using SistemaX.Modules.Fiscal.Domain.Regimes;

namespace SistemaX.Modules.Fiscal.Infrastructure.Sqlite;

public sealed class SqlitePerfilFiscalNcmRepository(ILocalSqliteConnectionFactory connectionFactory, ILocalSessao sessao)
    : IPerfilFiscalNcmRepository
{
    private const string Colunas =
        """
        tenant_id, regime, ncm, origem_mercadoria, exige_icms_st, cest, aliquota_ipi_milionesimos,
        cst_csosn_pis_cofins, aliquota_pis_milionesimos, aliquota_cofins_milionesimos, atualizado_em
        """;

    public Task<PerfilFiscalNCM?> ObterAsync(string tenantId, RegimeTributario regime, string ncm, CancellationToken ct = default)
        => SqliteSessaoHelper.ConsultarAsync(connectionFactory, sessao, async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = $"SELECT {Colunas} FROM fiscal_perfis_ncm WHERE tenant_id = $tenantId AND regime = $regime AND ncm = $ncm;";
            cmd.Parameters.AddWithValue("$tenantId", tenantId);
            cmd.Parameters.AddWithValue("$regime", (int)regime);
            cmd.Parameters.AddWithValue("$ncm", ncm);

            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            return !await reader.ReadAsync(ct).ConfigureAwait(false) ? null : Ler(reader);
        }, ct);

    public Task<IReadOnlyList<PerfilFiscalNCM>> ListarAsync(string tenantId, RegimeTributario regime, CancellationToken ct = default)
        => SqliteSessaoHelper.ConsultarAsync(connectionFactory, sessao, async (connection, transaction) =>
        {
            var lista = new List<PerfilFiscalNCM>();
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = $"SELECT {Colunas} FROM fiscal_perfis_ncm WHERE tenant_id = $tenantId AND regime = $regime;";
            cmd.Parameters.AddWithValue("$tenantId", tenantId);
            cmd.Parameters.AddWithValue("$regime", (int)regime);

            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
                lista.Add(Ler(reader));

            return (IReadOnlyList<PerfilFiscalNCM>)lista;
        }, ct);

    public Task SalvarAsync(PerfilFiscalNCM perfil, CancellationToken ct = default)
        => SqliteSessaoHelper.ExecutarAsync(connectionFactory, sessao, async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText =
                """
                INSERT INTO fiscal_perfis_ncm
                    (tenant_id, regime, ncm, origem_mercadoria, exige_icms_st, cest, aliquota_ipi_milionesimos,
                     cst_csosn_pis_cofins, aliquota_pis_milionesimos, aliquota_cofins_milionesimos, atualizado_em)
                VALUES
                    ($tenantId, $regime, $ncm, $origem, $exigeSt, $cest, $aliquotaIpi,
                     $cstPisCofins, $aliquotaPis, $aliquotaCofins, $atualizadoEm)
                ON CONFLICT(tenant_id, regime, ncm) DO UPDATE SET
                    origem_mercadoria = excluded.origem_mercadoria,
                    exige_icms_st = excluded.exige_icms_st,
                    cest = excluded.cest,
                    aliquota_ipi_milionesimos = excluded.aliquota_ipi_milionesimos,
                    cst_csosn_pis_cofins = excluded.cst_csosn_pis_cofins,
                    aliquota_pis_milionesimos = excluded.aliquota_pis_milionesimos,
                    aliquota_cofins_milionesimos = excluded.aliquota_cofins_milionesimos,
                    atualizado_em = excluded.atualizado_em;
                """;
            cmd.Parameters.AddWithValue("$tenantId", perfil.TenantId);
            cmd.Parameters.AddWithValue("$regime", (int)perfil.Regime);
            cmd.Parameters.AddWithValue("$ncm", perfil.Ncm);
            cmd.Parameters.AddWithValue("$origem", (int)perfil.Origem);
            cmd.Parameters.AddWithValue("$exigeSt", perfil.ExigeIcmsSt);
            cmd.Parameters.AddWithValue("$cest", (object?)perfil.Cest ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$aliquotaIpi", (object?)perfil.AliquotaIpi?.Milionesimos ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$cstPisCofins", perfil.CstOuCsosnPisCofins);
            cmd.Parameters.AddWithValue("$aliquotaPis", (object?)perfil.AliquotaPis?.Milionesimos ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$aliquotaCofins", (object?)perfil.AliquotaCofins?.Milionesimos ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$atualizadoEm", perfil.AtualizadoEm.ToUnixTimeMilliseconds());
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }, ct);

    private static PerfilFiscalNCM Ler(Microsoft.Data.Sqlite.SqliteDataReader reader) => new(
        TenantId: reader.GetString(0),
        Regime: (RegimeTributario)reader.GetInt32(1),
        Ncm: reader.GetString(2),
        Origem: (OrigemMercadoria)reader.GetInt32(3),
        ExigeIcmsSt: reader.GetBoolean(4),
        Cest: reader.IsDBNull(5) ? null : reader.GetString(5),
        AliquotaIpi: reader.IsDBNull(6) ? null : new Percentual(reader.GetInt64(6)),
        CstOuCsosnPisCofins: reader.GetString(7),
        AliquotaPis: reader.IsDBNull(8) ? null : new Percentual(reader.GetInt64(8)),
        AliquotaCofins: reader.IsDBNull(9) ? null : new Percentual(reader.GetInt64(9)),
        AtualizadoEm: DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(10)));
}
