using Microsoft.Data.Sqlite;
using SistemaX.Infrastructure.Local;
using SistemaX.Infrastructure.Local.UnitOfWork;
using SistemaX.Modules.Estoque.Application.Ports;
using SistemaX.Modules.Estoque.Domain.Comum;
using SistemaX.Modules.Estoque.Domain.Saldos;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Estoque.Infrastructure.Sqlite;

/// <summary>
/// Persistência REAL (SQLite) de <see cref="SaldoDeItem"/> — o read-model materializado
/// (produto × depósito), chave primária composta (tenant+produto+depósito, sem <c>Id</c> próprio).
/// Reidratação usa <see cref="SaldoDeItem.Reconstituir"/> — nunca <see cref="SaldoDeItem.Vazio"/>
/// seguido de mutação manual, que perderia o <c>UltimoMovimentoId</c> gravado.
/// </summary>
public sealed class SqliteSaldoRepository(ILocalSqliteConnectionFactory connectionFactory, ILocalSessao sessao)
    : ISaldoRepository
{
    private const string Colunas =
        "tenant_id, produto_id, deposito_id, fisico_milesimos, reservado_milesimos, custo_medio_centavos, custo_medio_moeda, ultimo_movimento_id";

    public Task<SaldoDeItem?> ObterAsync(string tenantId, string produtoId, string depositoId, CancellationToken ct = default)
        => ConsultarAsync(async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText =
                $"""
                SELECT {Colunas}
                FROM saldos_de_item
                WHERE tenant_id = $tenantId AND produto_id = $produtoId AND deposito_id = $depositoId;
                """;
            cmd.Parameters.AddWithValue("$tenantId", tenantId);
            cmd.Parameters.AddWithValue("$produtoId", produtoId);
            cmd.Parameters.AddWithValue("$depositoId", depositoId);

            return await LerUmAsync(cmd, ct).ConfigureAwait(false);
        }, ct);

    /// <summary>Espelha o adapter in-memory: o "ou criar" NUNCA toca o banco — só devolve
    /// <see cref="SaldoDeItem.Vazio"/> em memória. Persistir de fato só acontece quando o chamador
    /// (depois de <c>AplicarMovimento</c>) invocar <see cref="SalvarAsync"/> explicitamente.</summary>
    public async Task<SaldoDeItem> ObterOuCriarAsync(string tenantId, string produtoId, string depositoId, CancellationToken ct = default)
        => await ObterAsync(tenantId, produtoId, depositoId, ct).ConfigureAwait(false)
           ?? SaldoDeItem.Vazio(tenantId, produtoId, depositoId);

    public Task SalvarAsync(SaldoDeItem saldo, CancellationToken ct = default)
        => ExecutarAsync(async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText =
                """
                INSERT INTO saldos_de_item
                    (tenant_id, produto_id, deposito_id, fisico_milesimos, reservado_milesimos, custo_medio_centavos, custo_medio_moeda, ultimo_movimento_id)
                VALUES
                    ($tenantId, $produtoId, $depositoId, $fisico, $reservado, $custoMedioCentavos, $custoMedioMoeda, $ultimoMovimentoId)
                ON CONFLICT(tenant_id, produto_id, deposito_id) DO UPDATE SET
                    fisico_milesimos      = excluded.fisico_milesimos,
                    reservado_milesimos   = excluded.reservado_milesimos,
                    custo_medio_centavos  = excluded.custo_medio_centavos,
                    custo_medio_moeda     = excluded.custo_medio_moeda,
                    ultimo_movimento_id   = excluded.ultimo_movimento_id;
                """;
            cmd.Parameters.AddWithValue("$tenantId", saldo.TenantId);
            cmd.Parameters.AddWithValue("$produtoId", saldo.ProdutoId);
            cmd.Parameters.AddWithValue("$depositoId", saldo.DepositoId);
            cmd.Parameters.AddWithValue("$fisico", saldo.Fisico.Milesimos);
            cmd.Parameters.AddWithValue("$reservado", saldo.Reservado.Milesimos);
            cmd.Parameters.AddWithValue("$custoMedioCentavos", saldo.CustoMedio.Centavos);
            cmd.Parameters.AddWithValue("$custoMedioMoeda", saldo.CustoMedio.Moeda);
            cmd.Parameters.AddWithValue("$ultimoMovimentoId", (object?)saldo.UltimoMovimentoId ?? DBNull.Value);

            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }, ct);

    public Task<IReadOnlyList<SaldoDeItem>> ListarAsync(string tenantId, CancellationToken ct = default)
        => ConsultarAsync(async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = $"SELECT {Colunas} FROM saldos_de_item WHERE tenant_id = $tenantId;";
            cmd.Parameters.AddWithValue("$tenantId", tenantId);

            var resultado = new List<SaldoDeItem>();
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                resultado.Add(LerSaldo(reader));
            }

            return (IReadOnlyList<SaldoDeItem>)resultado;
        }, ct);

    private static async Task<SaldoDeItem?> LerUmAsync(SqliteCommand cmd, CancellationToken ct)
    {
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            return null;
        }

        return LerSaldo(reader);
    }

    private static SaldoDeItem LerSaldo(SqliteDataReader reader)
        => SaldoDeItem.Reconstituir(
            tenantId: reader.GetString(0),
            produtoId: reader.GetString(1),
            depositoId: reader.GetString(2),
            fisico: new Quantidade(reader.GetInt64(3)),
            reservado: new Quantidade(reader.GetInt64(4)),
            custoMedio: new Money(reader.GetInt64(5), reader.GetString(6)),
            ultimoMovimentoId: reader.IsDBNull(7) ? null : reader.GetString(7));

    /// <summary>Escreve dentro da sessão ambiente, se houver uma ativa; senão abre conexão própria
    /// e curta. Este método (e <see cref="ConsultarAsync{T}"/>) é o que TODO repositório SQLite
    /// novo deve reusar — nunca abrir conexão "na mão" espalhado pelos métodos do port.</summary>
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
