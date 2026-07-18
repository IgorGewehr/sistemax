using System.Globalization;
using Microsoft.Data.Sqlite;
using SistemaX.Infrastructure.Local;
using SistemaX.Infrastructure.Local.UnitOfWork;
using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Domain.Tempo;

namespace SistemaX.Modules.Financeiro.Infrastructure.Sqlite;

/// <summary>
/// Persistência REAL (SQLite) de <see cref="ApontamentoDeTempo"/> — mesmo molde de
/// <see cref="SqliteProjetoRepository"/>. Schema nasce de <see cref="FinanceiroSchemaMigrationV36"/>.
/// </summary>
public sealed class SqliteApontamentoDeTempoRepository(ILocalSqliteConnectionFactory connectionFactory, ILocalSessao sessao)
    : IApontamentoDeTempoRepository
{
    private const string Colunas =
        "id, business_id, projeto_id, cliente_id, cliente_nome, assinatura_id, ordem_servico_id, minutos, data, " +
        "operador_id, operador_nome, descricao, custo_hora_centavos_snapshot, criado_em";

    public Task<ApontamentoDeTempo?> ObterPorIdAsync(string businessId, string apontamentoId, CancellationToken ct = default)
        => ConsultarAsync(async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = $"SELECT {Colunas} FROM apontamentos_de_tempo WHERE business_id = $biz AND id = $id;";
            cmd.Parameters.AddWithValue("$biz", businessId);
            cmd.Parameters.AddWithValue("$id", apontamentoId);

            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            return await reader.ReadAsync(ct).ConfigureAwait(false) ? Ler(reader) : null;
        }, ct);

    public Task<IReadOnlyList<ApontamentoDeTempo>> ListarAsync(
        string businessId, DateTimeOffset de, DateTimeOffset ate, string? projetoId = null, string? clienteId = null, CancellationToken ct = default)
        => ConsultarAsync(async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            var filtros = "business_id = $biz AND data >= $de AND data <= $ate";
            if (projetoId is not null) filtros += " AND projeto_id = $projeto";
            if (clienteId is not null) filtros += " AND cliente_id = $cliente";
            cmd.CommandText = $"SELECT {Colunas} FROM apontamentos_de_tempo WHERE {filtros} ORDER BY data;";
            cmd.Parameters.AddWithValue("$biz", businessId);
            cmd.Parameters.AddWithValue("$de", Iso(de));
            cmd.Parameters.AddWithValue("$ate", Iso(ate));
            if (projetoId is not null) cmd.Parameters.AddWithValue("$projeto", projetoId);
            if (clienteId is not null) cmd.Parameters.AddWithValue("$cliente", clienteId);

            var resultado = new List<ApontamentoDeTempo>();
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false)) resultado.Add(Ler(reader));
            return (IReadOnlyList<ApontamentoDeTempo>)resultado;
        }, ct);

    public Task SalvarAsync(ApontamentoDeTempo apontamento, CancellationToken ct = default)
        => ExecutarAsync(async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText =
                """
                INSERT INTO apontamentos_de_tempo (
                    id, business_id, projeto_id, cliente_id, cliente_nome, assinatura_id, ordem_servico_id, minutos, data,
                    operador_id, operador_nome, descricao, custo_hora_centavos_snapshot, criado_em)
                VALUES (
                    $id, $biz, $projeto, $cliente, $clienteNome, $assinatura, $os, $minutos, $data,
                    $operadorId, $operadorNome, $descricao, $custoHora, $criadoEm)
                ON CONFLICT(id) DO UPDATE SET
                    descricao = excluded.descricao;
                """;
            cmd.Parameters.AddWithValue("$id", apontamento.Id);
            cmd.Parameters.AddWithValue("$biz", apontamento.BusinessId);
            cmd.Parameters.AddWithValue("$projeto", (object?)apontamento.ProjetoId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$cliente", (object?)apontamento.ClienteId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$clienteNome", (object?)apontamento.ClienteNome ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$assinatura", (object?)apontamento.AssinaturaId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$os", (object?)apontamento.OrdemServicoId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$minutos", apontamento.Minutos);
            cmd.Parameters.AddWithValue("$data", Iso(apontamento.Data));
            cmd.Parameters.AddWithValue("$operadorId", apontamento.OperadorId);
            cmd.Parameters.AddWithValue("$operadorNome", apontamento.OperadorNome);
            cmd.Parameters.AddWithValue("$descricao", (object?)apontamento.Descricao ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$custoHora", (object?)apontamento.CustoHoraCentavosSnapshot ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$criadoEm", Iso(apontamento.CriadoEm));
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }, ct);

    public Task<bool> ExcluirAsync(string businessId, string apontamentoId, CancellationToken ct = default)
        => ConsultarAsync(async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = "DELETE FROM apontamentos_de_tempo WHERE business_id = $biz AND id = $id;";
            cmd.Parameters.AddWithValue("$biz", businessId);
            cmd.Parameters.AddWithValue("$id", apontamentoId);
            var linhas = await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            return linhas > 0;
        }, ct);

    private static ApontamentoDeTempo Ler(SqliteDataReader reader)
        => ApontamentoDeTempo.Reconstituir(
            id: reader.GetString(0),
            businessId: reader.GetString(1),
            projetoId: reader.IsDBNull(2) ? null : reader.GetString(2),
            clienteId: reader.IsDBNull(3) ? null : reader.GetString(3),
            clienteNome: reader.IsDBNull(4) ? null : reader.GetString(4),
            assinaturaId: reader.IsDBNull(5) ? null : reader.GetString(5),
            ordemServicoId: reader.IsDBNull(6) ? null : reader.GetString(6),
            minutos: reader.GetInt32(7),
            data: ParseInstante(reader.GetString(8))!.Value,
            operadorId: reader.GetString(9),
            operadorNome: reader.GetString(10),
            descricao: reader.IsDBNull(11) ? null : reader.GetString(11),
            custoHoraCentavosSnapshot: reader.IsDBNull(12) ? null : reader.GetInt64(12),
            criadoEm: ParseInstante(reader.GetString(13))!.Value);

    private static string Iso(DateTimeOffset d) => d.ToString("O", CultureInfo.InvariantCulture);
    private static DateTimeOffset? ParseInstante(string? s) => s is null ? null : DateTimeOffset.Parse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

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
