using System.Globalization;
using Microsoft.Data.Sqlite;
using SistemaX.Infrastructure.Local;
using SistemaX.Infrastructure.Local.UnitOfWork;
using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Domain.Assinaturas;
using SistemaX.Modules.Financeiro.Domain.Recorrencia;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Infrastructure.Sqlite;

/// <summary>
/// Persistência REAL (SQLite) de <see cref="Assinatura"/> — a tabela <c>assinaturas</c> que você
/// pode abrir e navegar (DB Browser for SQLite / sqlite3). Mesmo port <see cref="IAssinaturaRepository"/>
/// que o in-memory; trocar um pelo outro não toca Domain/Application. Na nuvem, o equivalente é
/// Postgres — mesmo modelo, provider diferente.
///
/// Segue o MESMO molde de <see cref="SqliteRecorrenciaRepository"/>: participa da transação
/// ambiente via <see cref="ILocalSessao"/> quando um caso de uso abriu uma (ex.: gravar a cobrança
/// gerada + a própria assinatura atualizada na mesma transação), e abre conexão curta própria
/// quando chamado fora de uma sessão (leituras soltas). O schema nasce de
/// <c>FinanceiroSchemaMigrationV15</c> — nunca cria a tabela no construtor (P0-3: essa era a razão
/// pela qual este repositório nunca era wired em produção antes de aqui).
/// </summary>
public sealed class SqliteAssinaturaRepository(ILocalSqliteConnectionFactory connectionFactory, ILocalSessao sessao)
    : IAssinaturaRepository
{
    private const string Colunas =
        """
        id, business_id, cliente_id, cliente_nome, servico_id, servico_nome, valor_centavos, moeda,
        ciclo, dia_cobranca, status, data_inicio, cancelada_em, motivo_cancelamento, ultima_cobranca_em
        """;

    public Task<IReadOnlyList<Assinatura>> ListarAsync(string businessId, CancellationToken ct = default)
        => ConsultarAsync("WHERE business_id = $biz", businessId, null, ct);

    public Task<IReadOnlyList<Assinatura>> ListarAtivasAsync(string businessId, CancellationToken ct = default)
        => ConsultarAsync("WHERE business_id = $biz AND status = $status", businessId, (int)StatusAssinatura.Ativa, ct);

    public async Task<Assinatura?> BuscarAsync(string businessId, string assinaturaId, CancellationToken ct = default)
    {
        var lista = await ConsultarAsync("WHERE business_id = $biz AND id = $id", businessId, null, ct, assinaturaId).ConfigureAwait(false);
        return lista.Count > 0 ? lista[0] : null;
    }

    public Task SalvarAsync(Assinatura a, CancellationToken ct = default)
        => ExecutarAsync(async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText =
                """
                INSERT INTO assinaturas
                    (id, business_id, cliente_id, cliente_nome, servico_id, servico_nome, valor_centavos, moeda,
                     ciclo, dia_cobranca, status, data_inicio, cancelada_em, motivo_cancelamento, ultima_cobranca_em)
                VALUES
                    ($id, $biz, $cliId, $cliNome, $srvId, $srvNome, $valor, $moeda,
                     $ciclo, $dia, $status, $inicio, $cancel, $motivo, $ultima)
                ON CONFLICT(id) DO UPDATE SET
                    status = excluded.status, cancelada_em = excluded.cancelada_em,
                    motivo_cancelamento = excluded.motivo_cancelamento, ultima_cobranca_em = excluded.ultima_cobranca_em;
                """;
            cmd.Parameters.AddWithValue("$id", a.Id);
            cmd.Parameters.AddWithValue("$biz", a.BusinessId);
            cmd.Parameters.AddWithValue("$cliId", a.ClienteId);
            cmd.Parameters.AddWithValue("$cliNome", a.ClienteNome);
            cmd.Parameters.AddWithValue("$srvId", a.ServicoId);
            cmd.Parameters.AddWithValue("$srvNome", a.ServicoNome);
            cmd.Parameters.AddWithValue("$valor", a.ValorPorCiclo.Centavos);
            cmd.Parameters.AddWithValue("$moeda", a.ValorPorCiclo.Moeda);
            cmd.Parameters.AddWithValue("$ciclo", (int)a.Ciclo);
            cmd.Parameters.AddWithValue("$dia", a.DiaCobranca);
            cmd.Parameters.AddWithValue("$status", (int)a.Status);
            cmd.Parameters.AddWithValue("$inicio", Iso(a.DataInicio));
            cmd.Parameters.AddWithValue("$cancel", (object?)Iso(a.CanceladaEm) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$motivo", (object?)a.MotivoCancelamento ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$ultima", (object?)Iso(a.UltimaCobrancaGeradaEm) ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }, ct);

    private async Task<IReadOnlyList<Assinatura>> ConsultarAsync(
        string filtro, string businessId, int? status, CancellationToken ct, string? id = null)
        => await ConsultarAsync(async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = $"SELECT {Colunas} FROM assinaturas {filtro};";
            cmd.Parameters.AddWithValue("$biz", businessId);
            if (status is { } s) cmd.Parameters.AddWithValue("$status", s);
            if (id is not null) cmd.Parameters.AddWithValue("$id", id);

            var resultado = new List<Assinatura>();
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                resultado.Add(Ler(reader));
            }
            return (IReadOnlyList<Assinatura>)resultado;
        }, ct).ConfigureAwait(false);

    private static Assinatura Ler(SqliteDataReader reader)
        => Assinatura.Reconstituir(
            id: reader.GetString(0),
            businessId: reader.GetString(1),
            clienteId: reader.GetString(2),
            clienteNome: reader.GetString(3),
            servicoId: reader.GetString(4),
            servicoNome: reader.GetString(5),
            valorPorCiclo: new Money(reader.GetInt64(6), reader.GetString(7)),
            ciclo: (FrequenciaRecorrencia)reader.GetInt32(8),
            diaCobranca: reader.GetInt32(9),
            status: (StatusAssinatura)reader.GetInt32(10),
            dataInicio: ParseData(reader.GetString(11))!.Value,
            canceladaEm: reader.IsDBNull(12) ? null : ParseData(reader.GetString(12)),
            motivoCancelamento: reader.IsDBNull(13) ? null : reader.GetString(13),
            ultimaCobrancaGeradaEm: reader.IsDBNull(14) ? null : ParseData(reader.GetString(14)));

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
