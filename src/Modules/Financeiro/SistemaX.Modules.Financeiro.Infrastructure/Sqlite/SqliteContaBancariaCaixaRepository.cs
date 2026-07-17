using System.Globalization;
using Microsoft.Data.Sqlite;
using SistemaX.Infrastructure.Local;
using SistemaX.Infrastructure.Local.UnitOfWork;
using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Domain.Caixa;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Infrastructure.Sqlite;

/// <summary>
/// Persistência REAL (SQLite) de <see cref="ContaBancariaCaixa"/> — schema em
/// <see cref="FinanceiroSchemaMigrationV12"/>. Entidade mutável (nome/ativa podem mudar), upsert
/// simples — mesmo molde de <see cref="SqliteRecorrenciaRepository"/>. Toda leitura filtra por
/// <c>business_id</c> mesmo já tendo o <c>id</c> (R1 — businessId é sagrado).
/// </summary>
public sealed class SqliteContaBancariaCaixaRepository(ILocalSqliteConnectionFactory connectionFactory, ILocalSessao sessao)
    : IContaBancariaCaixaRepository
{
    private const string Colunas =
        """
        id, business_id, nome, tipo, saldo_inicial_centavos, saldo_inicial_moeda,
        ativa, criado_em, atualizado_em
        """;

    public Task<ContaBancariaCaixa?> ObterPorIdAsync(string businessId, string id, CancellationToken ct = default)
        => ConsultarAsync(async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = $"SELECT {Colunas} FROM contas_bancarias_caixa WHERE business_id = $biz AND id = $id;";
            cmd.Parameters.AddWithValue("$biz", businessId);
            cmd.Parameters.AddWithValue("$id", id);

            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            return await reader.ReadAsync(ct).ConfigureAwait(false) ? Ler(reader) : null;
        }, ct);

    public Task<IReadOnlyList<ContaBancariaCaixa>> ListarAsync(string businessId, bool apenasAtivas = false, CancellationToken ct = default)
        => ConsultarAsync(async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = apenasAtivas
                ? $"SELECT {Colunas} FROM contas_bancarias_caixa WHERE business_id = $biz AND ativa = 1 ORDER BY criado_em ASC;"
                : $"SELECT {Colunas} FROM contas_bancarias_caixa WHERE business_id = $biz ORDER BY criado_em ASC;";
            cmd.Parameters.AddWithValue("$biz", businessId);

            var resultado = new List<ContaBancariaCaixa>();
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                resultado.Add(Ler(reader));
            }
            return (IReadOnlyList<ContaBancariaCaixa>)resultado;
        }, ct);

    public Task SalvarAsync(ContaBancariaCaixa conta, CancellationToken ct = default)
        => ExecutarAsync(async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText =
                """
                INSERT INTO contas_bancarias_caixa
                    (id, business_id, nome, tipo, saldo_inicial_centavos, saldo_inicial_moeda,
                     ativa, criado_em, atualizado_em)
                VALUES
                    ($id, $biz, $nome, $tipo, $saldoCentavos, $saldoMoeda,
                     $ativa, $criadoEm, $atualizadoEm)
                ON CONFLICT(id) DO UPDATE SET
                    nome          = excluded.nome,
                    tipo          = excluded.tipo,
                    ativa         = excluded.ativa,
                    atualizado_em = excluded.atualizado_em;
                """;
            cmd.Parameters.AddWithValue("$id", conta.Id);
            cmd.Parameters.AddWithValue("$biz", conta.BusinessId);
            cmd.Parameters.AddWithValue("$nome", conta.Nome);
            cmd.Parameters.AddWithValue("$tipo", (int)conta.Tipo);
            cmd.Parameters.AddWithValue("$saldoCentavos", conta.SaldoInicial.Centavos);
            cmd.Parameters.AddWithValue("$saldoMoeda", conta.SaldoInicial.Moeda);
            cmd.Parameters.AddWithValue("$ativa", conta.Ativa ? 1 : 0);
            cmd.Parameters.AddWithValue("$criadoEm", Iso(conta.CriadoEm));
            cmd.Parameters.AddWithValue("$atualizadoEm", Iso(conta.AtualizadoEm));

            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }, ct);

    private static ContaBancariaCaixa Ler(SqliteDataReader reader)
        => ContaBancariaCaixa.Reconstituir(
            id: reader.GetString(0),
            businessId: reader.GetString(1),
            nome: reader.GetString(2),
            tipo: (TipoContaBancariaCaixa)reader.GetInt32(3),
            saldoInicial: new Money(reader.GetInt64(4), reader.GetString(5)),
            ativa: reader.GetInt32(6) != 0,
            criadoEm: ParseData(reader.GetString(7)),
            atualizadoEm: ParseData(reader.GetString(8)));

    private static string Iso(DateTimeOffset d) => d.ToString("O", CultureInfo.InvariantCulture);
    private static DateTimeOffset ParseData(string s) => DateTimeOffset.Parse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

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
