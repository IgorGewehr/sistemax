using SistemaX.Infrastructure.Local;
using SistemaX.Infrastructure.Local.UnitOfWork;
using SistemaX.Modules.Fiscal.Application.Ports;
using SistemaX.Modules.Fiscal.Domain.Comum;
using SistemaX.Modules.Fiscal.Domain.Operacoes;
using SistemaX.Modules.Fiscal.Domain.Regimes;
using SistemaX.Modules.Fiscal.Domain.Regras;

namespace SistemaX.Modules.Fiscal.Infrastructure.Sqlite;

/// <summary>
/// Persiste a matriz de decisão de CSOSN/CST. <c>id</c> é uma chave SINTÉTICA determinística
/// composta pela chave natural do registro (<see cref="ChaveSintetica"/>) — dá upsert idempotente
/// por natureza (editar uma linha existente em Settings→Fiscal é um <c>ON CONFLICT DO UPDATE</c>,
/// nunca um append duplicado) sem precisar de um índice único adicional.
/// </summary>
public sealed class SqliteRegraFiscalPorOperacaoRepository(ILocalSqliteConnectionFactory connectionFactory, ILocalSessao sessao)
    : IRegraFiscalPorOperacaoRepository
{
    private const string Colunas =
        """
        tenant_id, regime, tipo_operacao, uf_origem, uf_destino, indicador_st, situacao_tributaria, eh_csosn,
        aliquota_interna_milionesimos, aliquota_interestadual_milionesimos, reducao_base_milionesimos,
        mva_milionesimos, aliquota_fcp_milionesimos
        """;

    public Task<RegraFiscalPorOperacao?> ResolverAsync(
        string tenantId, RegimeTributario regime, TipoOperacaoFiscal tipoOperacao,
        string ufOrigem, string ufDestino, bool indicadorSt, CancellationToken ct = default)
        => SqliteSessaoHelper.ConsultarAsync(connectionFactory, sessao, async (connection, transaction) =>
        {
            var candidatas = new List<RegraFiscalPorOperacao>();
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText =
                $"""
                SELECT {Colunas} FROM fiscal_regras_operacao
                WHERE (tenant_id IS NULL OR tenant_id = $tenantId)
                  AND regime = $regime AND tipo_operacao = $tipoOperacao
                  AND uf_origem = $ufOrigem
                  AND (uf_destino IS NULL OR uf_destino = $ufDestino)
                  AND indicador_st = $indicadorSt;
                """;
            cmd.Parameters.AddWithValue("$tenantId", tenantId);
            cmd.Parameters.AddWithValue("$regime", (int)regime);
            cmd.Parameters.AddWithValue("$tipoOperacao", (int)tipoOperacao);
            cmd.Parameters.AddWithValue("$ufOrigem", ufOrigem);
            cmd.Parameters.AddWithValue("$ufDestino", ufDestino);
            cmd.Parameters.AddWithValue("$indicadorSt", indicadorSt);

            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
                candidatas.Add(Ler(reader));

            return candidatas.OrderByDescending(r => r.Especificidade).FirstOrDefault();
        }, ct);

    public Task<IReadOnlyList<RegraFiscalPorOperacao>> ListarAsync(string? tenantId, CancellationToken ct = default)
        => SqliteSessaoHelper.ConsultarAsync(connectionFactory, sessao, async (connection, transaction) =>
        {
            var lista = new List<RegraFiscalPorOperacao>();
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = tenantId is null
                ? $"SELECT {Colunas} FROM fiscal_regras_operacao WHERE tenant_id IS NULL;"
                : $"SELECT {Colunas} FROM fiscal_regras_operacao WHERE tenant_id IS NULL OR tenant_id = $tenantId;";
            if (tenantId is not null) cmd.Parameters.AddWithValue("$tenantId", tenantId);

            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
                lista.Add(Ler(reader));

            return (IReadOnlyList<RegraFiscalPorOperacao>)lista;
        }, ct);

    public Task SalvarAsync(RegraFiscalPorOperacao regra, CancellationToken ct = default)
        => SqliteSessaoHelper.ExecutarAsync(connectionFactory, sessao, async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText =
                $"""
                INSERT INTO fiscal_regras_operacao
                    (id, {Colunas})
                VALUES
                    ($id, $tenantId, $regime, $tipoOperacao, $ufOrigem, $ufDestino, $indicadorSt, $situacao, $ehCsosn,
                     $aliquotaInterna, $aliquotaInterestadual, $reducaoBase, $mva, $aliquotaFcp)
                ON CONFLICT(id) DO UPDATE SET
                    situacao_tributaria = excluded.situacao_tributaria,
                    eh_csosn = excluded.eh_csosn,
                    aliquota_interna_milionesimos = excluded.aliquota_interna_milionesimos,
                    aliquota_interestadual_milionesimos = excluded.aliquota_interestadual_milionesimos,
                    reducao_base_milionesimos = excluded.reducao_base_milionesimos,
                    mva_milionesimos = excluded.mva_milionesimos,
                    aliquota_fcp_milionesimos = excluded.aliquota_fcp_milionesimos;
                """;
            cmd.Parameters.AddWithValue("$id", ChaveSintetica(regra));
            cmd.Parameters.AddWithValue("$tenantId", (object?)regra.TenantId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$regime", (int)regra.Regime);
            cmd.Parameters.AddWithValue("$tipoOperacao", (int)regra.TipoOperacao);
            cmd.Parameters.AddWithValue("$ufOrigem", regra.UfOrigem);
            cmd.Parameters.AddWithValue("$ufDestino", (object?)regra.UfDestino ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$indicadorSt", regra.IndicadorSt);
            cmd.Parameters.AddWithValue("$situacao", regra.SituacaoIcms.Codigo);
            cmd.Parameters.AddWithValue("$ehCsosn", regra.SituacaoIcms.EhCsosn);
            cmd.Parameters.AddWithValue("$aliquotaInterna", (object?)regra.AliquotaInterna?.Milionesimos ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$aliquotaInterestadual", (object?)regra.AliquotaInterestadual?.Milionesimos ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$reducaoBase", (object?)regra.ReducaoBaseCalculo?.Milionesimos ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$mva", (object?)regra.Mva?.Milionesimos ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$aliquotaFcp", (object?)regra.AliquotaFcp?.Milionesimos ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }, ct);

    private static string ChaveSintetica(RegraFiscalPorOperacao r) =>
        $"{r.TenantId ?? "*"}:{r.Regime}:{r.TipoOperacao}:{r.UfOrigem}:{r.UfDestino ?? "*"}:{r.IndicadorSt}";

    private static RegraFiscalPorOperacao Ler(Microsoft.Data.Sqlite.SqliteDataReader reader)
    {
        var regime = (RegimeTributario)reader.GetInt32(1);
        var ehCsosn = reader.GetBoolean(7);
        var codigoSituacao = reader.GetString(6);
        var situacao = ehCsosn
            ? SituacaoTributariaIcms.ParaCsosn(regime, codigoSituacao).Valor
            : SituacaoTributariaIcms.ParaCst(regime, codigoSituacao).Valor;

        return new RegraFiscalPorOperacao(
            TenantId: reader.IsDBNull(0) ? null : reader.GetString(0),
            Regime: regime,
            TipoOperacao: (TipoOperacaoFiscal)reader.GetInt32(2),
            UfOrigem: reader.GetString(3),
            UfDestino: reader.IsDBNull(4) ? null : reader.GetString(4),
            IndicadorSt: reader.GetBoolean(5),
            SituacaoIcms: situacao,
            AliquotaInterna: reader.IsDBNull(8) ? null : new Percentual(reader.GetInt64(8)),
            AliquotaInterestadual: reader.IsDBNull(9) ? null : new Percentual(reader.GetInt64(9)),
            ReducaoBaseCalculo: reader.IsDBNull(10) ? null : new Percentual(reader.GetInt64(10)),
            Mva: reader.IsDBNull(11) ? null : new Percentual(reader.GetInt64(11)),
            AliquotaFcp: reader.IsDBNull(12) ? null : new Percentual(reader.GetInt64(12)));
    }
}
