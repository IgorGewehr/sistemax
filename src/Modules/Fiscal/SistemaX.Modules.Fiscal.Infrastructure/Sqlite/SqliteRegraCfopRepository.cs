using SistemaX.Infrastructure.Local;
using SistemaX.Infrastructure.Local.UnitOfWork;
using SistemaX.Modules.Fiscal.Application.Ports;
using SistemaX.Modules.Fiscal.Domain.Operacoes;
using SistemaX.Modules.Fiscal.Domain.Produtos;
using SistemaX.Modules.Fiscal.Domain.Regras;

namespace SistemaX.Modules.Fiscal.Infrastructure.Sqlite;

/// <summary>Persiste a tabela "padrão-config" de CFOP (decisão de Igor, ADR-0002) — mesmo padrão
/// de id sintético/upsert de <see cref="SqliteRegraFiscalPorOperacaoRepository"/>.</summary>
public sealed class SqliteRegraCfopRepository(ILocalSqliteConnectionFactory connectionFactory, ILocalSessao sessao)
    : IRegraCfopRepository
{
    private const string Colunas = "tenant_id, tipo_operacao, eh_interestadual, destinatario_contribuinte_icms, natureza_operacao, cfop";

    public Task<RegraCfop?> ResolverAsync(
        string tenantId, TipoOperacaoFiscal tipoOperacao, bool ehInterestadual,
        bool destinatarioContribuinteIcms, NaturezaOperacaoProduto natureza, CancellationToken ct = default)
        => SqliteSessaoHelper.ConsultarAsync(connectionFactory, sessao, async (connection, transaction) =>
        {
            var candidatas = new List<RegraCfop>();
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText =
                $"""
                SELECT {Colunas} FROM fiscal_regras_cfop
                WHERE (tenant_id IS NULL OR tenant_id = $tenantId)
                  AND tipo_operacao = $tipoOperacao
                  AND eh_interestadual = $ehInterestadual
                  AND destinatario_contribuinte_icms = $destinatarioContribuinteIcms
                  AND natureza_operacao = $natureza;
                """;
            cmd.Parameters.AddWithValue("$tenantId", tenantId);
            cmd.Parameters.AddWithValue("$tipoOperacao", (int)tipoOperacao);
            cmd.Parameters.AddWithValue("$ehInterestadual", ehInterestadual);
            cmd.Parameters.AddWithValue("$destinatarioContribuinteIcms", destinatarioContribuinteIcms);
            cmd.Parameters.AddWithValue("$natureza", (int)natureza);

            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
                candidatas.Add(Ler(reader));

            return candidatas.OrderByDescending(r => r.Especificidade).FirstOrDefault();
        }, ct);

    public Task<IReadOnlyList<RegraCfop>> ListarAsync(string? tenantId, CancellationToken ct = default)
        => SqliteSessaoHelper.ConsultarAsync(connectionFactory, sessao, async (connection, transaction) =>
        {
            var lista = new List<RegraCfop>();
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = tenantId is null
                ? $"SELECT {Colunas} FROM fiscal_regras_cfop WHERE tenant_id IS NULL;"
                : $"SELECT {Colunas} FROM fiscal_regras_cfop WHERE tenant_id IS NULL OR tenant_id = $tenantId;";
            if (tenantId is not null) cmd.Parameters.AddWithValue("$tenantId", tenantId);

            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
                lista.Add(Ler(reader));

            return (IReadOnlyList<RegraCfop>)lista;
        }, ct);

    public Task SalvarAsync(RegraCfop regra, CancellationToken ct = default)
        => SqliteSessaoHelper.ExecutarAsync(connectionFactory, sessao, async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText =
                $"""
                INSERT INTO fiscal_regras_cfop
                    (id, {Colunas})
                VALUES
                    ($id, $tenantId, $tipoOperacao, $ehInterestadual, $destinatarioContribuinteIcms, $natureza, $cfop)
                ON CONFLICT(id) DO UPDATE SET cfop = excluded.cfop;
                """;
            cmd.Parameters.AddWithValue("$id", ChaveSintetica(regra));
            cmd.Parameters.AddWithValue("$tenantId", (object?)regra.TenantId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$tipoOperacao", (int)regra.TipoOperacao);
            cmd.Parameters.AddWithValue("$ehInterestadual", regra.EhInterestadual);
            cmd.Parameters.AddWithValue("$destinatarioContribuinteIcms", regra.DestinatarioContribuinteIcms);
            cmd.Parameters.AddWithValue("$natureza", (int)regra.Natureza);
            cmd.Parameters.AddWithValue("$cfop", regra.Cfop);
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }, ct);

    private static string ChaveSintetica(RegraCfop r) =>
        $"{r.TenantId ?? "*"}:{r.TipoOperacao}:{r.EhInterestadual}:{r.DestinatarioContribuinteIcms}:{r.Natureza}";

    private static RegraCfop Ler(Microsoft.Data.Sqlite.SqliteDataReader reader) => new(
        TenantId: reader.IsDBNull(0) ? null : reader.GetString(0),
        TipoOperacao: (TipoOperacaoFiscal)reader.GetInt32(1),
        EhInterestadual: reader.GetBoolean(2),
        DestinatarioContribuinteIcms: reader.GetBoolean(3),
        Natureza: (NaturezaOperacaoProduto)reader.GetInt32(4),
        Cfop: reader.GetString(5));
}
