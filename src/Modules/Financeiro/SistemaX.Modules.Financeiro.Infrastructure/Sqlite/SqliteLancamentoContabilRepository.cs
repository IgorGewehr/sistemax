using System.Globalization;
using Microsoft.Data.Sqlite;
using SistemaX.Infrastructure.Local;
using SistemaX.Infrastructure.Local.UnitOfWork;
using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Domain.Contabil;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Infrastructure.Sqlite;

/// <summary>
/// Persistência REAL (SQLite) de <see cref="LancamentoContabil"/> — o motor invisível de partida
/// dobrada. Header E filhas (<see cref="PartidaContabil"/>) são INSERT-ONLY: um lançamento é
/// imutável por construção (corrigir é <c>GerarEstorno</c>, um novo <see cref="LancamentoContabil"/>),
/// então nunca há <c>DELETE</c>/<c>DO UPDATE</c> aqui — diferente do par Conta/Parcela, que É
/// mutável. Cada <see cref="PartidaContabil"/> não tem id natural: sintetizamos
/// <c>$"{lancamento.Id}:{ordem}"</c> antes de inserir, o que torna o insert-only idempotente via
/// <c>ON CONFLICT(id) DO NOTHING</c>. <c>ordem</c> preserva a ordem original da lista ao reler
/// (<c>ORDER BY ordem</c>).
/// </summary>
public sealed class SqliteLancamentoContabilRepository(ILocalSqliteConnectionFactory connectionFactory, ILocalSessao sessao)
    : ILancamentoContabilRepository
{
    private const string ColunasHeader =
        """
        id, business_id, data, descricao, origem_modulo, origem_tipo_fato, origem_id, reversal_of_id, criado_em
        """;

    public Task<LancamentoContabil?> ObterPorIdAsync(string id, CancellationToken ct = default)
        => ConsultarAsync(async (connection, transaction) =>
        {
            var lista = await LerLancamentosAsync(connection, transaction, "WHERE id = $id",
                cmd => cmd.Parameters.AddWithValue("$id", id), ct).ConfigureAwait(false);
            return lista.Count > 0 ? lista[0] : null;
        }, ct);

    public Task<LancamentoContabil?> BuscarPorOrigemAsync(string businessId, string origemChave, CancellationToken ct = default)
        => ConsultarAsync(async (connection, transaction) =>
        {
            var lista = await LerLancamentosAsync(connection, transaction, "WHERE business_id = $biz AND origem_chave = $chave",
                cmd =>
                {
                    cmd.Parameters.AddWithValue("$biz", businessId);
                    cmd.Parameters.AddWithValue("$chave", origemChave);
                }, ct).ConfigureAwait(false);
            return lista.Count > 0 ? lista[0] : null;
        }, ct);

    public Task<IReadOnlyList<LancamentoContabil>> ListarPorPeriodoAsync(string businessId, DateTimeOffset inicio, DateTimeOffset fim, CancellationToken ct = default)
        => ConsultarAsync(async (connection, transaction) =>
        {
            var lista = await LerLancamentosAsync(connection, transaction,
                "WHERE business_id = $biz AND data >= $inicio AND data <= $fim",
                cmd =>
                {
                    cmd.Parameters.AddWithValue("$biz", businessId);
                    cmd.Parameters.AddWithValue("$inicio", Iso(inicio));
                    cmd.Parameters.AddWithValue("$fim", Iso(fim));
                }, ct).ConfigureAwait(false);
            return (IReadOnlyList<LancamentoContabil>)lista;
        }, ct);

    public Task SalvarAsync(LancamentoContabil lancamento, CancellationToken ct = default)
        => ExecutarAsync(async (connection, transaction) =>
        {
            await using (var cmd = connection.CreateCommand())
            {
                cmd.Transaction = transaction;
                cmd.CommandText =
                    """
                    INSERT INTO lancamentos_contabeis
                        (id, business_id, data, descricao, origem_modulo, origem_tipo_fato, origem_id, origem_chave, reversal_of_id, criado_em)
                    VALUES
                        ($id, $biz, $data, $descricao, $origemModulo, $origemTipoFato, $origemId, $origemChave, $reversalOfId, $criadoEm)
                    ON CONFLICT(id) DO NOTHING;
                    """;
                cmd.Parameters.AddWithValue("$id", lancamento.Id);
                cmd.Parameters.AddWithValue("$biz", lancamento.BusinessId);
                cmd.Parameters.AddWithValue("$data", Iso(lancamento.Data));
                cmd.Parameters.AddWithValue("$descricao", lancamento.Descricao);
                cmd.Parameters.AddWithValue("$origemModulo", lancamento.Origem.Modulo);
                cmd.Parameters.AddWithValue("$origemTipoFato", lancamento.Origem.TipoFato);
                cmd.Parameters.AddWithValue("$origemId", lancamento.Origem.Id);
                cmd.Parameters.AddWithValue("$origemChave", lancamento.Origem.Chave);
                cmd.Parameters.AddWithValue("$reversalOfId", (object?)lancamento.ReversalOfId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$criadoEm", Iso(lancamento.CriadoEm));

                await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }

            var ordem = 0;
            foreach (var partida in lancamento.Partidas)
            {
                await using var insCmd = connection.CreateCommand();
                insCmd.Transaction = transaction;
                insCmd.CommandText =
                    """
                    INSERT INTO partidas_contabeis (id, lancamento_id, ordem, conta_contabil_id, natureza, valor_centavos, valor_moeda)
                    VALUES ($id, $lancamentoId, $ordem, $contaContabilId, $natureza, $valorCentavos, $valorMoeda)
                    ON CONFLICT(id) DO NOTHING;
                    """;
                insCmd.Parameters.AddWithValue("$id", $"{lancamento.Id}:{ordem}");
                insCmd.Parameters.AddWithValue("$lancamentoId", lancamento.Id);
                insCmd.Parameters.AddWithValue("$ordem", ordem);
                insCmd.Parameters.AddWithValue("$contaContabilId", partida.ContaContabilId);
                insCmd.Parameters.AddWithValue("$natureza", (int)partida.Natureza);
                insCmd.Parameters.AddWithValue("$valorCentavos", partida.Valor.Centavos);
                insCmd.Parameters.AddWithValue("$valorMoeda", partida.Valor.Moeda);

                await insCmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                ordem++;
            }
        }, ct);

    private static async Task<List<LancamentoContabil>> LerLancamentosAsync(
        SqliteConnection connection, SqliteTransaction? transaction, string whereClause,
        Action<SqliteCommand> configurarParametros, CancellationToken ct)
    {
        var headers = new List<HeaderRow>();
        await using (var cmd = connection.CreateCommand())
        {
            cmd.Transaction = transaction;
            cmd.CommandText = $"SELECT {ColunasHeader} FROM lancamentos_contabeis {whereClause};";
            configurarParametros(cmd);

            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                headers.Add(new HeaderRow(
                    reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
                    reader.GetString(4), reader.GetString(5), reader.GetString(6),
                    reader.IsDBNull(7) ? null : reader.GetString(7), reader.GetString(8)));
            }
        }

        var resultado = new List<LancamentoContabil>(headers.Count);
        foreach (var h in headers)
        {
            var partidas = await LerPartidasAsync(connection, transaction, h.Id, ct).ConfigureAwait(false);
            resultado.Add(LancamentoContabil.Reconstituir(
                h.Id, h.BusinessId, ParseData(h.Data)!.Value, h.Descricao,
                new OrigemLancamento(h.OrigemModulo, h.OrigemTipoFato, h.OrigemId),
                h.ReversalOfId, ParseData(h.CriadoEm)!.Value, partidas));
        }
        return resultado;
    }

    private static async Task<List<PartidaContabil>> LerPartidasAsync(SqliteConnection connection, SqliteTransaction? transaction, string lancamentoId, CancellationToken ct)
    {
        await using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText =
            """
            SELECT conta_contabil_id, natureza, valor_centavos, valor_moeda
            FROM partidas_contabeis
            WHERE lancamento_id = $lancamentoId
            ORDER BY ordem;
            """;
        cmd.Parameters.AddWithValue("$lancamentoId", lancamentoId);

        var partidas = new List<PartidaContabil>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            partidas.Add(new PartidaContabil(
                reader.GetString(0),
                (NaturezaPartida)reader.GetInt32(1),
                new Money(reader.GetInt64(2), reader.GetString(3))));
        }
        return partidas;
    }

    private static string Iso(DateTimeOffset d) => d.ToString("O", CultureInfo.InvariantCulture);
    private static DateTimeOffset? ParseData(string? s) => s is null ? null : DateTimeOffset.Parse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    private sealed record HeaderRow(
        string Id, string BusinessId, string Data, string Descricao, string OrigemModulo,
        string OrigemTipoFato, string OrigemId, string? ReversalOfId, string CriadoEm);

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
