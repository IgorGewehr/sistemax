using System.Globalization;
using Microsoft.Data.Sqlite;
using SistemaX.Infrastructure.Local;
using SistemaX.Infrastructure.Local.UnitOfWork;
using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Domain.Caixa;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Infrastructure.Sqlite;

/// <summary>
/// Persistência REAL (SQLite) de <see cref="Conciliacao"/> — entidade raiz MUTÁVEL sem filhos
/// (vínculo Movimento↔Extrato), upsert simples no mesmo espírito de <c>SqliteFornecedorRepository</c>.
/// Compartilha arquivo com <see cref="SqliteExtratoBancarioItemRepository"/>, espelhando
/// <c>InMemoryConciliacaoRepository.cs</c> (que também agrupa as duas classes).
/// </summary>
public sealed class SqliteConciliacaoRepository(ILocalSqliteConnectionFactory connectionFactory, ILocalSessao sessao)
    : IConciliacaoRepository
{
    private const string Colunas = "id, business_id, movimento_financeiro_id, extrato_bancario_item_id, status, conciliado_em";

    public Task<Conciliacao?> ObterPorIdAsync(string id, CancellationToken ct = default)
        => ConsultarAsync(async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = $"SELECT {Colunas} FROM conciliacoes WHERE id = $id;";
            cmd.Parameters.AddWithValue("$id", id);

            return await LerUmAsync(cmd, ct).ConfigureAwait(false);
        }, ct);

    public Task<Conciliacao?> BuscarPorParAsync(string movimentoFinanceiroId, string extratoBancarioItemId, CancellationToken ct = default)
        => ConsultarAsync(async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText =
                $"""
                SELECT {Colunas}
                FROM conciliacoes
                WHERE movimento_financeiro_id = $mov AND extrato_bancario_item_id = $ext;
                """;
            cmd.Parameters.AddWithValue("$mov", movimentoFinanceiroId);
            cmd.Parameters.AddWithValue("$ext", extratoBancarioItemId);

            return await LerUmAsync(cmd, ct).ConfigureAwait(false);
        }, ct);

    public Task SalvarAsync(Conciliacao conciliacao, CancellationToken ct = default)
        => ExecutarAsync(async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText =
                """
                INSERT INTO conciliacoes (id, business_id, movimento_financeiro_id, extrato_bancario_item_id, status, conciliado_em)
                VALUES ($id, $biz, $mov, $ext, $status, $conciliadoEm)
                ON CONFLICT(id) DO UPDATE SET
                    status        = excluded.status,
                    conciliado_em = excluded.conciliado_em;
                """;
            cmd.Parameters.AddWithValue("$id", conciliacao.Id);
            cmd.Parameters.AddWithValue("$biz", conciliacao.BusinessId);
            cmd.Parameters.AddWithValue("$mov", conciliacao.MovimentoFinanceiroId);
            cmd.Parameters.AddWithValue("$ext", conciliacao.ExtratoBancarioItemId);
            cmd.Parameters.AddWithValue("$status", (int)conciliacao.Status);
            cmd.Parameters.AddWithValue("$conciliadoEm", (object?)Iso(conciliacao.ConciliadoEm) ?? DBNull.Value);

            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }, ct);

    public Task<IReadOnlyList<Conciliacao>> ListarPorBusinessIdAsync(string businessId, CancellationToken ct = default)
        => ConsultarAsync(async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = $"SELECT {Colunas} FROM conciliacoes WHERE business_id = $biz;";
            cmd.Parameters.AddWithValue("$biz", businessId);

            var resultado = new List<Conciliacao>();
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                resultado.Add(Ler(reader));
            }
            return (IReadOnlyList<Conciliacao>)resultado;
        }, ct);

    private static async Task<Conciliacao?> LerUmAsync(SqliteCommand cmd, CancellationToken ct)
    {
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            return null;
        }

        return Ler(reader);
    }

    private static Conciliacao Ler(SqliteDataReader reader)
        => Conciliacao.Reconstituir(
            id: reader.GetString(0),
            businessId: reader.GetString(1),
            movimentoFinanceiroId: reader.GetString(2),
            extratoBancarioItemId: reader.GetString(3),
            status: (StatusConciliacao)reader.GetInt32(4),
            conciliadoEm: reader.IsDBNull(5) ? null : ParseData(reader.GetString(5)));

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

/// <summary>
/// Persistência REAL (SQLite) de <see cref="ExtratoBancarioItem"/> — IMUTÁVEL uma vez importado
/// (insert-only, <c>ON CONFLICT(id) DO NOTHING</c>). <see cref="ListarNaoConciliadosAsync"/>
/// REPLICA o comportamento (surpreendente) do adapter in-memory: não filtra por status de
/// conciliação nenhum — só business_id + conta — porque é isso que
/// <c>InMemoryExtratoBancarioItemRepository.ListarNaoConciliadosAsync</c> faz hoje (gap
/// pré-existente do port, não corrigido aqui: o job é replicar, não consertar).
/// </summary>
public sealed class SqliteExtratoBancarioItemRepository(ILocalSqliteConnectionFactory connectionFactory, ILocalSessao sessao)
    : IExtratoBancarioItemRepository
{
    private const string Colunas =
        "id, business_id, conta_bancaria_caixa_id, data, valor_centavos, valor_moeda, descricao, identificador_externo";

    public Task<ExtratoBancarioItem?> BuscarPorIdentificadorExternoAsync(string businessId, string identificadorExterno, CancellationToken ct = default)
        => ConsultarAsync(async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText =
                $"""
                SELECT {Colunas}
                FROM extratos_bancarios_itens
                WHERE business_id = $biz AND identificador_externo = $ident;
                """;
            cmd.Parameters.AddWithValue("$biz", businessId);
            cmd.Parameters.AddWithValue("$ident", identificadorExterno);

            return await LerUmAsync(cmd, ct).ConfigureAwait(false);
        }, ct);

    public Task<IReadOnlyList<ExtratoBancarioItem>> ListarNaoConciliadosAsync(string businessId, string contaBancariaCaixaId, CancellationToken ct = default)
        => ConsultarAsync(async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText =
                $"""
                SELECT {Colunas}
                FROM extratos_bancarios_itens
                WHERE business_id = $biz AND conta_bancaria_caixa_id = $conta;
                """;
            cmd.Parameters.AddWithValue("$biz", businessId);
            cmd.Parameters.AddWithValue("$conta", contaBancariaCaixaId);

            var resultado = new List<ExtratoBancarioItem>();
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                resultado.Add(Ler(reader));
            }
            return (IReadOnlyList<ExtratoBancarioItem>)resultado;
        }, ct);

    public Task SalvarAsync(ExtratoBancarioItem item, CancellationToken ct = default)
        => ExecutarAsync(async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText =
                """
                INSERT INTO extratos_bancarios_itens
                    (id, business_id, conta_bancaria_caixa_id, data, valor_centavos, valor_moeda, descricao, identificador_externo)
                VALUES
                    ($id, $biz, $conta, $data, $valorCentavos, $valorMoeda, $descricao, $ident)
                ON CONFLICT(id) DO NOTHING;
                """;
            cmd.Parameters.AddWithValue("$id", item.Id);
            cmd.Parameters.AddWithValue("$biz", item.BusinessId);
            cmd.Parameters.AddWithValue("$conta", item.ContaBancariaCaixaId);
            cmd.Parameters.AddWithValue("$data", Iso(item.Data));
            cmd.Parameters.AddWithValue("$valorCentavos", item.Valor.Centavos);
            cmd.Parameters.AddWithValue("$valorMoeda", item.Valor.Moeda);
            cmd.Parameters.AddWithValue("$descricao", item.Descricao);
            cmd.Parameters.AddWithValue("$ident", item.IdentificadorExterno);

            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }, ct);

    private static async Task<ExtratoBancarioItem?> LerUmAsync(SqliteCommand cmd, CancellationToken ct)
    {
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            return null;
        }

        return Ler(reader);
    }

    private static ExtratoBancarioItem Ler(SqliteDataReader reader)
        => ExtratoBancarioItem.Reconstituir(
            id: reader.GetString(0),
            businessId: reader.GetString(1),
            contaBancariaCaixaId: reader.GetString(2),
            data: ParseData(reader.GetString(3))!.Value,
            valor: new Money(reader.GetInt64(4), reader.GetString(5)),
            descricao: reader.GetString(6),
            identificadorExterno: reader.GetString(7));

    private static string Iso(DateTimeOffset d) => d.ToString("O", CultureInfo.InvariantCulture);
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
