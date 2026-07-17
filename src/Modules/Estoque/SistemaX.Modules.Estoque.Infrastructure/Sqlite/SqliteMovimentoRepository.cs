using System.Globalization;
using Microsoft.Data.Sqlite;
using SistemaX.Infrastructure.Local;
using SistemaX.Infrastructure.Local.UnitOfWork;
using SistemaX.Modules.Estoque.Application.Ports;
using SistemaX.Modules.Estoque.Domain.Comum;
using SistemaX.Modules.Estoque.Domain.Razao;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Estoque.Infrastructure.Sqlite;

/// <summary>
/// Persistência REAL (SQLite) de <see cref="MovimentoDeEstoque"/> — o RAZÃO. APPEND-ONLY: este
/// port (<see cref="IMovimentoRepository"/>) não expõe update/delete, só <see cref="SalvarAsync"/>
/// (insert) e leituras. Reidratação usa <see cref="MovimentoDeEstoque.Reconstituir"/> — nunca
/// <see cref="MovimentoDeEstoque.Registrar"/>, que validaria de novo (R6). Idempotência é do
/// CHAMADOR: consultar <see cref="ExisteComChaveAsync"/> antes de montar o movimento.
/// </summary>
public sealed class SqliteMovimentoRepository(ILocalSqliteConnectionFactory connectionFactory, ILocalSessao sessao)
    : IMovimentoRepository
{
    private const string Colunas =
        """
        id, tenant_id, deposito_id, produto_id, tipo, quantidade_milesimos, custo_unitario_centavos, custo_unitario_moeda,
        origem_modulo, origem_id, origem_chave, chave_idempotencia, lote_id, motivo, operador_id, operador_nome, ocorrido_em
        """;

    public Task<bool> ExisteComChaveAsync(string chaveIdempotencia, CancellationToken ct = default)
        => ConsultarAsync(async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = "SELECT 1 FROM movimentos_de_estoque WHERE chave_idempotencia = $chave LIMIT 1;";
            cmd.Parameters.AddWithValue("$chave", chaveIdempotencia);

            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            return await reader.ReadAsync(ct).ConfigureAwait(false);
        }, ct);

    public Task SalvarAsync(MovimentoDeEstoque movimento, CancellationToken ct = default)
        => ExecutarAsync(async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText =
                """
                INSERT INTO movimentos_de_estoque
                    (id, tenant_id, deposito_id, produto_id, tipo, quantidade_milesimos, custo_unitario_centavos, custo_unitario_moeda,
                     origem_modulo, origem_id, origem_chave, chave_idempotencia, lote_id, motivo, operador_id, operador_nome, ocorrido_em)
                VALUES
                    ($id, $tenantId, $depositoId, $produtoId, $tipo, $quantidade, $custoUnitarioCentavos, $custoUnitarioMoeda,
                     $origemModulo, $origemId, $origemChave, $chaveIdempotencia, $loteId, $motivo, $operadorId, $operadorNome, $ocorridoEm)
                ON CONFLICT(id) DO NOTHING;
                """;
            cmd.Parameters.AddWithValue("$id", movimento.Id);
            cmd.Parameters.AddWithValue("$tenantId", movimento.TenantId);
            cmd.Parameters.AddWithValue("$depositoId", movimento.DepositoId);
            cmd.Parameters.AddWithValue("$produtoId", movimento.ProdutoId);
            cmd.Parameters.AddWithValue("$tipo", (int)movimento.Tipo);
            cmd.Parameters.AddWithValue("$quantidade", movimento.Quantidade.Milesimos);
            cmd.Parameters.AddWithValue("$custoUnitarioCentavos", movimento.CustoUnitario.Centavos);
            cmd.Parameters.AddWithValue("$custoUnitarioMoeda", movimento.CustoUnitario.Moeda);
            cmd.Parameters.AddWithValue("$origemModulo", movimento.Origem.Modulo);
            cmd.Parameters.AddWithValue("$origemId", movimento.Origem.Id);
            cmd.Parameters.AddWithValue("$origemChave", movimento.Origem.Chave);
            cmd.Parameters.AddWithValue("$chaveIdempotencia", movimento.ChaveIdempotencia);
            cmd.Parameters.AddWithValue("$loteId", (object?)movimento.LoteId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$motivo", movimento.Motivo);
            cmd.Parameters.AddWithValue("$operadorId", movimento.OperadorId);
            cmd.Parameters.AddWithValue("$operadorNome", movimento.OperadorNome);
            cmd.Parameters.AddWithValue("$ocorridoEm", Iso(movimento.OcorridoEm));

            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }, ct);

    public Task<IReadOnlyList<MovimentoDeEstoque>> ListarPorOrigemAsync(string tenantId, string sourceRefChave, CancellationToken ct = default)
        => ConsultarAsync(async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText =
                $"""
                SELECT {Colunas}
                FROM movimentos_de_estoque
                WHERE tenant_id = $tenantId AND origem_chave = $origemChave
                ORDER BY id;
                """;
            cmd.Parameters.AddWithValue("$tenantId", tenantId);
            cmd.Parameters.AddWithValue("$origemChave", sourceRefChave);

            return await ListarComandoAsync(cmd, ct).ConfigureAwait(false);
        }, ct);

    public Task<IReadOnlyList<MovimentoDeEstoque>> ListarPorProdutoAsync(string tenantId, string produtoId, string depositoId, CancellationToken ct = default)
        => ConsultarAsync(async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText =
                $"""
                SELECT {Colunas}
                FROM movimentos_de_estoque
                WHERE tenant_id = $tenantId AND produto_id = $produtoId AND deposito_id = $depositoId
                ORDER BY id;
                """;
            cmd.Parameters.AddWithValue("$tenantId", tenantId);
            cmd.Parameters.AddWithValue("$produtoId", produtoId);
            cmd.Parameters.AddWithValue("$depositoId", depositoId);

            return await ListarComandoAsync(cmd, ct).ConfigureAwait(false);
        }, ct);

    public Task<IReadOnlyList<MovimentoDeEstoque>> ListarPorPeriodoAsync(string tenantId, DateTimeOffset inicio, DateTimeOffset fim, CancellationToken ct = default)
        => ConsultarAsync(async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText =
                $"""
                SELECT {Colunas}
                FROM movimentos_de_estoque
                WHERE tenant_id = $tenantId AND ocorrido_em >= $inicio AND ocorrido_em <= $fim
                ORDER BY id;
                """;
            cmd.Parameters.AddWithValue("$tenantId", tenantId);
            cmd.Parameters.AddWithValue("$inicio", Iso(inicio));
            cmd.Parameters.AddWithValue("$fim", Iso(fim));

            return await ListarComandoAsync(cmd, ct).ConfigureAwait(false);
        }, ct);

    private static async Task<IReadOnlyList<MovimentoDeEstoque>> ListarComandoAsync(SqliteCommand cmd, CancellationToken ct)
    {
        var resultado = new List<MovimentoDeEstoque>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            resultado.Add(LerMovimento(reader));
        }

        return resultado;
    }

    private static MovimentoDeEstoque LerMovimento(SqliteDataReader reader)
        => MovimentoDeEstoque.Reconstituir(
            id: reader.GetString(0),
            tenantId: reader.GetString(1),
            depositoId: reader.GetString(2),
            produtoId: reader.GetString(3),
            tipo: (TipoMovimento)reader.GetInt32(4),
            quantidade: new Quantidade(reader.GetInt64(5)),
            custoUnitario: new Money(reader.GetInt64(6), reader.GetString(7)),
            origem: new SourceRef(reader.GetString(8), reader.GetString(9)),
            chaveIdempotencia: reader.GetString(11),
            loteId: reader.IsDBNull(12) ? null : reader.GetString(12),
            motivo: reader.GetString(13),
            operadorId: reader.GetString(14),
            operadorNome: reader.GetString(15),
            ocorridoEm: ParseData(reader.GetString(16)));

    private static string Iso(DateTimeOffset d) => d.ToString("O", CultureInfo.InvariantCulture);
    private static DateTimeOffset ParseData(string s) => DateTimeOffset.Parse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

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
