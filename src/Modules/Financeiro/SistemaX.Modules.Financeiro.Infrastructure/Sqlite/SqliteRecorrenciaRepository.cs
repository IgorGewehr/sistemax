using System.Globalization;
using Microsoft.Data.Sqlite;
using SistemaX.Infrastructure.Local;
using SistemaX.Infrastructure.Local.UnitOfWork;
using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Domain.Recorrencia;
using SistemaX.SharedKernel;
using RecorrenciaAgg = SistemaX.Modules.Financeiro.Domain.Recorrencia.Recorrencia;

namespace SistemaX.Modules.Financeiro.Infrastructure.Sqlite;

/// <summary>
/// Persistência REAL (SQLite) de <see cref="RecorrenciaAgg"/> — entidade raiz MUTÁVEL sem filhos
/// (template gerador de contas futuras), upsert simples. Toda leitura filtra por
/// <c>business_id</c> mesmo quando já se tem o <c>id</c> — espelha a defensividade da chave
/// composta <c>Chave(businessId,id)</c> do adapter in-memory (R1 — businessId é sagrado).
/// </summary>
public sealed class SqliteRecorrenciaRepository(ILocalSqliteConnectionFactory connectionFactory, ILocalSessao sessao)
    : IRecorrenciaRepository
{
    private const string Colunas =
        """
        id, business_id, descricao, tipo, valor_previsto_centavos, valor_previsto_moeda, categoria_id,
        dia_fixo, frequencia, data_inicio, data_fim, ativa, ultima_geracao_em, projeto_id
        """;

    public Task<IReadOnlyList<RecorrenciaAgg>> ListarAtivasAsync(string businessId, CancellationToken ct = default)
        => ConsultarAsync(async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = $"SELECT {Colunas} FROM recorrencias WHERE business_id = $biz AND ativa = 1;";
            cmd.Parameters.AddWithValue("$biz", businessId);

            var resultado = new List<RecorrenciaAgg>();
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                resultado.Add(Ler(reader));
            }
            return (IReadOnlyList<RecorrenciaAgg>)resultado;
        }, ct);

    public Task<RecorrenciaAgg?> BuscarAsync(string businessId, string id, CancellationToken ct = default)
        => ConsultarAsync(async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = $"SELECT {Colunas} FROM recorrencias WHERE business_id = $biz AND id = $id;";
            cmd.Parameters.AddWithValue("$biz", businessId);
            cmd.Parameters.AddWithValue("$id", id);

            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            if (!await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                return null;
            }
            return Ler(reader);
        }, ct);

    public Task SalvarAsync(RecorrenciaAgg recorrencia, CancellationToken ct = default)
        => ExecutarAsync(async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText =
                """
                INSERT INTO recorrencias
                    (id, business_id, descricao, tipo, valor_previsto_centavos, valor_previsto_moeda, categoria_id,
                     dia_fixo, frequencia, data_inicio, data_fim, ativa, ultima_geracao_em, projeto_id)
                VALUES
                    ($id, $biz, $descricao, $tipo, $valorCentavos, $valorMoeda, $categoriaId,
                     $diaFixo, $frequencia, $dataInicio, $dataFim, $ativa, $ultimaGeracaoEm, $projetoId)
                ON CONFLICT(id) DO UPDATE SET
                    descricao               = excluded.descricao,
                    valor_previsto_centavos = excluded.valor_previsto_centavos,
                    valor_previsto_moeda    = excluded.valor_previsto_moeda,
                    categoria_id            = excluded.categoria_id,
                    dia_fixo                = excluded.dia_fixo,
                    frequencia              = excluded.frequencia,
                    data_fim                = excluded.data_fim,
                    ativa                   = excluded.ativa,
                    ultima_geracao_em       = excluded.ultima_geracao_em,
                    projeto_id              = excluded.projeto_id;
                """;
            cmd.Parameters.AddWithValue("$id", recorrencia.Id);
            cmd.Parameters.AddWithValue("$biz", recorrencia.BusinessId);
            cmd.Parameters.AddWithValue("$descricao", recorrencia.Descricao);
            cmd.Parameters.AddWithValue("$tipo", (int)recorrencia.Tipo);
            cmd.Parameters.AddWithValue("$valorCentavos", recorrencia.ValorPrevisto.Centavos);
            cmd.Parameters.AddWithValue("$valorMoeda", recorrencia.ValorPrevisto.Moeda);
            cmd.Parameters.AddWithValue("$categoriaId", recorrencia.CategoriaId);
            cmd.Parameters.AddWithValue("$diaFixo", (object?)recorrencia.DiaFixo ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$frequencia", (int)recorrencia.Frequencia);
            cmd.Parameters.AddWithValue("$dataInicio", Iso(recorrencia.DataInicio));
            cmd.Parameters.AddWithValue("$dataFim", (object?)Iso(recorrencia.DataFim) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$ativa", recorrencia.Ativa ? 1 : 0);
            cmd.Parameters.AddWithValue("$ultimaGeracaoEm", (object?)Iso(recorrencia.UltimaGeracaoEm) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$projetoId", (object?)recorrencia.ProjetoId ?? DBNull.Value);

            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }, ct);

    private static RecorrenciaAgg Ler(SqliteDataReader reader)
        => RecorrenciaAgg.Reconstituir(
            id: reader.GetString(0),
            businessId: reader.GetString(1),
            descricao: reader.GetString(2),
            tipo: (TipoContaRecorrente)reader.GetInt32(3),
            valorPrevisto: new Money(reader.GetInt64(4), reader.GetString(5)),
            categoriaId: reader.GetString(6),
            diaFixo: reader.IsDBNull(7) ? null : reader.GetInt32(7),
            frequencia: (FrequenciaRecorrencia)reader.GetInt32(8),
            dataInicio: ParseData(reader.GetString(9))!.Value,
            dataFim: reader.IsDBNull(10) ? null : ParseData(reader.GetString(10)),
            ativa: reader.GetInt32(11) != 0,
            ultimaGeracaoEm: reader.IsDBNull(12) ? null : ParseData(reader.GetString(12)),
            projetoId: reader.IsDBNull(13) ? null : reader.GetString(13));

    private static string Iso(DateTimeOffset d) => d.ToString("O", CultureInfo.InvariantCulture);
    private static string? Iso(DateTimeOffset? d) => d?.ToString("O", CultureInfo.InvariantCulture);
    private static DateTimeOffset? ParseData(string? s) => s is null ? null : DateTimeOffset.Parse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

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
