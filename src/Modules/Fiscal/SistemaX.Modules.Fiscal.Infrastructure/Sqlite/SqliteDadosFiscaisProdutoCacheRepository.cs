using SistemaX.Infrastructure.Local;
using SistemaX.Infrastructure.Local.UnitOfWork;
using SistemaX.Modules.Fiscal.Application.Ports;
using SistemaX.Modules.Fiscal.Domain.Produtos;

namespace SistemaX.Modules.Fiscal.Infrastructure.Sqlite;

public sealed class SqliteDadosFiscaisProdutoCacheRepository(ILocalSqliteConnectionFactory connectionFactory, ILocalSessao sessao)
    : IDadosFiscaisProdutoCacheRepository
{
    public Task<DadosFiscaisProdutoCache?> ObterAsync(string tenantId, string produtoId, CancellationToken ct = default)
        => SqliteSessaoHelper.ConsultarAsync(connectionFactory, sessao, async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText =
                "SELECT tenant_id, produto_id, ncm, cest, natureza_operacao, cfop_override, gtin, unidade_comercial FROM fiscal_dados_produto_cache WHERE tenant_id = $tenantId AND produto_id = $produtoId;";
            cmd.Parameters.AddWithValue("$tenantId", tenantId);
            cmd.Parameters.AddWithValue("$produtoId", produtoId);

            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            if (!await reader.ReadAsync(ct).ConfigureAwait(false)) return null;

            return new DadosFiscaisProdutoCache(
                reader.GetString(0), reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                NaturezaOperacaoProdutoExtensions.DeCodigo(reader.GetString(4)),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7));
        }, ct);

    public Task SalvarAsync(DadosFiscaisProdutoCache dados, CancellationToken ct = default)
        => SqliteSessaoHelper.ExecutarAsync(connectionFactory, sessao, async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText =
                """
                INSERT INTO fiscal_dados_produto_cache (tenant_id, produto_id, ncm, cest, natureza_operacao, cfop_override, gtin, unidade_comercial)
                VALUES ($tenantId, $produtoId, $ncm, $cest, $naturezaOperacao, $cfopOverride, $gtin, $unidadeComercial)
                ON CONFLICT(tenant_id, produto_id) DO UPDATE SET
                    ncm = excluded.ncm, cest = excluded.cest,
                    natureza_operacao = excluded.natureza_operacao, cfop_override = excluded.cfop_override,
                    gtin = excluded.gtin, unidade_comercial = excluded.unidade_comercial;
                """;
            cmd.Parameters.AddWithValue("$tenantId", dados.TenantId);
            cmd.Parameters.AddWithValue("$produtoId", dados.ProdutoId);
            cmd.Parameters.AddWithValue("$ncm", (object?)dados.Ncm ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$cest", (object?)dados.Cest ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$naturezaOperacao", dados.NaturezaOperacao.ParaCodigo());
            cmd.Parameters.AddWithValue("$cfopOverride", (object?)dados.CfopOverride ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$gtin", (object?)dados.Gtin ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$unidadeComercial", (object?)dados.UnidadeComercial ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }, ct);
}
