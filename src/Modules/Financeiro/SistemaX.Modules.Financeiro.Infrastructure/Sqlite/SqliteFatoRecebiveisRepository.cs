using System.Globalization;
using Microsoft.Data.Sqlite;
using SistemaX.Infrastructure.Local;
using SistemaX.Infrastructure.Local.UnitOfWork;
using SistemaX.Modules.Financeiro.Application.Analitico;
using SistemaX.Modules.Financeiro.Application.Ports;

namespace SistemaX.Modules.Financeiro.Infrastructure.Sqlite;

/// <summary>
/// Persistência REAL (SQLite) de <c>fato_recebiveis</c> — schema em
/// <see cref="FinanceiroSchemaMigrationV11"/>. Puro INSERT (append-only, sem upsert): cada chamada
/// de <see cref="AdicionarAsync"/> é uma linha nova, nunca uma atualização — mesma filosofia
/// imutável do restante do ledger/fact tables da F0/F1.
/// </summary>
public sealed class SqliteFatoRecebiveisRepository(ILocalSqliteConnectionFactory connectionFactory, ILocalSessao sessao)
    : IFatoRecebiveisRepository
{
    private const string Colunas =
        """
        tenant_id, origem_chave, vencimento, data_liquidacao_prevista, forma_pagamento,
        taxa_percentual_aplicada, valor_bruto_centavos, valor_liquido_centavos, atualizado_em_utc
        """;

    public Task AdicionarAsync(FatoRecebivel item, CancellationToken ct = default)
        => ExecutarAsync(async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText =
                $"""
                INSERT INTO fato_recebiveis ({Colunas})
                VALUES ($tenantId, $origemChave, $vencimento, $dataLiquidacao, $formaPagamento,
                        $taxa, $bruto, $liquido, $agora);
                """;
            cmd.Parameters.AddWithValue("$tenantId", item.TenantId);
            cmd.Parameters.AddWithValue("$origemChave", item.OrigemChave);
            cmd.Parameters.AddWithValue("$vencimento", Iso(item.Vencimento));
            cmd.Parameters.AddWithValue("$dataLiquidacao", Iso(item.DataLiquidacaoPrevista));
            cmd.Parameters.AddWithValue("$formaPagamento", (object?)item.FormaPagamento ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$taxa", item.TaxaPercentualAplicada.ToString(CultureInfo.InvariantCulture));
            cmd.Parameters.AddWithValue("$bruto", item.ValorBrutoCentavos);
            cmd.Parameters.AddWithValue("$liquido", item.ValorLiquidoCentavos);
            cmd.Parameters.AddWithValue("$agora", item.AtualizadoEmUtc.ToUnixTimeMilliseconds());

            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }, ct);

    public Task<IReadOnlyList<FatoRecebivel>> ListarPorVencimentoAsync(string tenantId, DateOnly de, DateOnly ate, CancellationToken ct = default)
        => ConsultarAsync(async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText =
                $"""
                SELECT {Colunas} FROM fato_recebiveis
                WHERE tenant_id = $tenantId AND vencimento >= $de AND vencimento <= $ate
                ORDER BY vencimento ASC;
                """;
            cmd.Parameters.AddWithValue("$tenantId", tenantId);
            cmd.Parameters.AddWithValue("$de", Iso(de));
            cmd.Parameters.AddWithValue("$ate", Iso(ate));

            var resultado = new List<FatoRecebivel>();
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                resultado.Add(Ler(reader));
            }
            return (IReadOnlyList<FatoRecebivel>)resultado;
        }, ct);

    public Task ZerarTudoAsync(CancellationToken ct = default)
        => ExecutarAsync(async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = "DELETE FROM fato_recebiveis;";
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }, ct);

    private static FatoRecebivel Ler(SqliteDataReader reader)
        => new(
            TenantId: reader.GetString(0),
            OrigemChave: reader.GetString(1),
            Vencimento: DateOnly.Parse(reader.GetString(2), CultureInfo.InvariantCulture),
            DataLiquidacaoPrevista: DateOnly.Parse(reader.GetString(3), CultureInfo.InvariantCulture),
            FormaPagamento: reader.IsDBNull(4) ? null : reader.GetString(4),
            TaxaPercentualAplicada: decimal.Parse(reader.GetString(5), CultureInfo.InvariantCulture),
            ValorBrutoCentavos: reader.GetInt64(6),
            ValorLiquidoCentavos: reader.GetInt64(7),
            AtualizadoEmUtc: DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(8)));

    private static string Iso(DateOnly d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

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
