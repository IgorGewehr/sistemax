using SistemaX.Infrastructure.Local;
using SistemaX.Infrastructure.Local.UnitOfWork;
using SistemaX.Modules.Fiscal.Application.Ports;
using SistemaX.Modules.Fiscal.Domain.Comum;
using SistemaX.Modules.Fiscal.Domain.Ncm;
using SistemaX.Modules.Fiscal.Domain.Produtos;

namespace SistemaX.Modules.Fiscal.Infrastructure.Sqlite;

public sealed class SqliteTributacaoProdutoRepository(ILocalSqliteConnectionFactory connectionFactory, ILocalSessao sessao)
    : ITributacaoProdutoRepository
{
    private const string Colunas =
        """
        tenant_id, produto_id, origem_override, exige_icms_st_override, cest_override,
        situacao_icms_override, aliquota_icms_override_milionesimos, reducao_base_override_milionesimos,
        mva_override_milionesimos, aliquota_ipi_override_milionesimos, cst_csosn_pis_cofins_override,
        aliquota_pis_override_milionesimos, aliquota_cofins_override_milionesimos, motivo, atualizado_em
        """;

    public Task<TributacaoProduto?> ObterAsync(string tenantId, string produtoId, CancellationToken ct = default)
        => SqliteSessaoHelper.ConsultarAsync(connectionFactory, sessao, async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = $"SELECT {Colunas} FROM fiscal_tributacoes_produto WHERE tenant_id = $tenantId AND produto_id = $produtoId;";
            cmd.Parameters.AddWithValue("$tenantId", tenantId);
            cmd.Parameters.AddWithValue("$produtoId", produtoId);

            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            return !await reader.ReadAsync(ct).ConfigureAwait(false) ? null : Ler(reader);
        }, ct);

    public Task SalvarAsync(TributacaoProduto tributacao, CancellationToken ct = default)
        => SqliteSessaoHelper.ExecutarAsync(connectionFactory, sessao, async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText =
                """
                INSERT INTO fiscal_tributacoes_produto
                    (tenant_id, produto_id, origem_override, exige_icms_st_override, cest_override,
                     situacao_icms_override, aliquota_icms_override_milionesimos, reducao_base_override_milionesimos,
                     mva_override_milionesimos, aliquota_ipi_override_milionesimos, cst_csosn_pis_cofins_override,
                     aliquota_pis_override_milionesimos, aliquota_cofins_override_milionesimos, motivo, atualizado_em)
                VALUES
                    ($tenantId, $produtoId, $origem, $exigeSt, $cest,
                     $situacaoIcms, $aliquotaIcms, $reducaoBase,
                     $mva, $aliquotaIpi, $cstPisCofins,
                     $aliquotaPis, $aliquotaCofins, $motivo, $atualizadoEm)
                ON CONFLICT(tenant_id, produto_id) DO UPDATE SET
                    origem_override = excluded.origem_override,
                    exige_icms_st_override = excluded.exige_icms_st_override,
                    cest_override = excluded.cest_override,
                    situacao_icms_override = excluded.situacao_icms_override,
                    aliquota_icms_override_milionesimos = excluded.aliquota_icms_override_milionesimos,
                    reducao_base_override_milionesimos = excluded.reducao_base_override_milionesimos,
                    mva_override_milionesimos = excluded.mva_override_milionesimos,
                    aliquota_ipi_override_milionesimos = excluded.aliquota_ipi_override_milionesimos,
                    cst_csosn_pis_cofins_override = excluded.cst_csosn_pis_cofins_override,
                    aliquota_pis_override_milionesimos = excluded.aliquota_pis_override_milionesimos,
                    aliquota_cofins_override_milionesimos = excluded.aliquota_cofins_override_milionesimos,
                    motivo = excluded.motivo,
                    atualizado_em = excluded.atualizado_em;
                """;
            cmd.Parameters.AddWithValue("$tenantId", tributacao.TenantId);
            cmd.Parameters.AddWithValue("$produtoId", tributacao.ProdutoId);
            cmd.Parameters.AddWithValue("$origem", (object?)(int?)tributacao.OrigemOverride ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$exigeSt", (object?)tributacao.ExigeIcmsStOverride ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$cest", (object?)tributacao.CestOverride ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$situacaoIcms", (object?)tributacao.SituacaoTributariaIcmsOverride ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$aliquotaIcms", (object?)tributacao.AliquotaIcmsOverride?.Milionesimos ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$reducaoBase", (object?)tributacao.ReducaoBaseCalculoOverride?.Milionesimos ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$mva", (object?)tributacao.MvaOverride?.Milionesimos ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$aliquotaIpi", (object?)tributacao.AliquotaIpiOverride?.Milionesimos ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$cstPisCofins", (object?)tributacao.CstOuCsosnPisCofinsOverride ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$aliquotaPis", (object?)tributacao.AliquotaPisOverride?.Milionesimos ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$aliquotaCofins", (object?)tributacao.AliquotaCofinsOverride?.Milionesimos ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$motivo", tributacao.Motivo);
            cmd.Parameters.AddWithValue("$atualizadoEm", tributacao.AtualizadoEm.ToUnixTimeMilliseconds());
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }, ct);

    private static TributacaoProduto Ler(Microsoft.Data.Sqlite.SqliteDataReader reader) => new(
        TenantId: reader.GetString(0),
        ProdutoId: reader.GetString(1),
        OrigemOverride: reader.IsDBNull(2) ? null : (OrigemMercadoria)reader.GetInt32(2),
        ExigeIcmsStOverride: reader.IsDBNull(3) ? null : reader.GetBoolean(3),
        CestOverride: reader.IsDBNull(4) ? null : reader.GetString(4),
        SituacaoTributariaIcmsOverride: reader.IsDBNull(5) ? null : reader.GetString(5),
        AliquotaIcmsOverride: reader.IsDBNull(6) ? null : new Percentual(reader.GetInt64(6)),
        ReducaoBaseCalculoOverride: reader.IsDBNull(7) ? null : new Percentual(reader.GetInt64(7)),
        MvaOverride: reader.IsDBNull(8) ? null : new Percentual(reader.GetInt64(8)),
        AliquotaIpiOverride: reader.IsDBNull(9) ? null : new Percentual(reader.GetInt64(9)),
        CstOuCsosnPisCofinsOverride: reader.IsDBNull(10) ? null : reader.GetString(10),
        AliquotaPisOverride: reader.IsDBNull(11) ? null : new Percentual(reader.GetInt64(11)),
        AliquotaCofinsOverride: reader.IsDBNull(12) ? null : new Percentual(reader.GetInt64(12)),
        Motivo: reader.GetString(13),
        AtualizadoEm: DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(14)));
}
