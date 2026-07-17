using System.Globalization;
using Microsoft.Data.Sqlite;
using SistemaX.Infrastructure.Local;
using SistemaX.Infrastructure.Local.UnitOfWork;
using SistemaX.Modules.Financeiro.Application.Analitico;
using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Application.Quant;

namespace SistemaX.Modules.Financeiro.Infrastructure.Sqlite;

/// <summary>
/// Persistência REAL (SQLite) de <c>fato_margem_produto</c> — schema em
/// <see cref="FinanceiroSchemaMigrationV10"/>. <see cref="AlocarCustoDeVendaAsync"/> é a operação
/// mais elaborada do módulo analítico: lê as linhas pendentes da venda, calcula o rateio
/// (<see cref="RateioProporcional"/>), grava o UPSERT de custo por produto/dia e apaga as linhas
/// pendentes consumidas — tudo dentro de UMA transação (a mesma que <c>ExecutarAsync</c> abre/
/// reusa), então um crash no meio nunca deixa custo alocado sem consumir a linha pendente (ou
/// vice-versa).
/// </summary>
public sealed class SqliteFatoMargemProdutoRepository(ILocalSqliteConnectionFactory connectionFactory, ILocalSessao sessao)
    : IFatoMargemProdutoRepository
{
    public Task RegistrarItensDeVendaAsync(string tenantId, string vendaId, DateOnly dia, IReadOnlyList<ItemMargemPendente> itens, CancellationToken ct = default)
        => ExecutarAsync(async (connection, transaction) =>
        {
            foreach (var item in itens)
            {
                await UpsertReceitaAsync(connection, transaction, tenantId, item.ProdutoId, dia, item.ReceitaItemCentavos, ct).ConfigureAwait(false);

                await using var cmdPendente = connection.CreateCommand();
                cmdPendente.Transaction = transaction;
                cmdPendente.CommandText =
                    """
                    INSERT INTO analitico_margem_pendente_itens_venda (tenant_id, venda_id, produto_id, dia, receita_item_centavos)
                    VALUES ($tenantId, $vendaId, $produtoId, $dia, $receita)
                    ON CONFLICT(tenant_id, venda_id, produto_id) DO UPDATE SET
                        receita_item_centavos = receita_item_centavos + excluded.receita_item_centavos;
                    """;
                cmdPendente.Parameters.AddWithValue("$tenantId", tenantId);
                cmdPendente.Parameters.AddWithValue("$vendaId", vendaId);
                cmdPendente.Parameters.AddWithValue("$produtoId", item.ProdutoId);
                cmdPendente.Parameters.AddWithValue("$dia", Iso(dia));
                cmdPendente.Parameters.AddWithValue("$receita", item.ReceitaItemCentavos);
                await cmdPendente.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }
        }, ct);

    public Task AlocarCustoDeVendaAsync(string tenantId, string vendaId, long custoTotalCentavos, CancellationToken ct = default)
        => ExecutarAsync(async (connection, transaction) =>
        {
            var pendentes = new List<(string ProdutoId, DateOnly Dia, long ReceitaCentavos)>();

            await using (var cmdSelect = connection.CreateCommand())
            {
                cmdSelect.Transaction = transaction;
                cmdSelect.CommandText =
                    """
                    SELECT produto_id, dia, receita_item_centavos
                    FROM analitico_margem_pendente_itens_venda
                    WHERE tenant_id = $tenantId AND venda_id = $vendaId;
                    """;
                cmdSelect.Parameters.AddWithValue("$tenantId", tenantId);
                cmdSelect.Parameters.AddWithValue("$vendaId", vendaId);

                await using var reader = await cmdSelect.ExecuteReaderAsync(ct).ConfigureAwait(false);
                while (await reader.ReadAsync(ct).ConfigureAwait(false))
                {
                    pendentes.Add((
                        reader.GetString(0),
                        DateOnly.Parse(reader.GetString(1), CultureInfo.InvariantCulture),
                        reader.GetInt64(2)));
                }
            }

            if (pendentes.Count == 0) return; // sem itens pendentes — no-op documentado (ver IFatoMargemProdutoRepository)

            var pesos = pendentes.Select(p => p.ReceitaCentavos).ToList();
            var alocado = RateioProporcional.Alocar(custoTotalCentavos, pesos);

            for (var i = 0; i < pendentes.Count; i++)
            {
                await UpsertCustoAsync(connection, transaction, tenantId, pendentes[i].ProdutoId, pendentes[i].Dia, alocado[i], ct).ConfigureAwait(false);
            }

            await using var cmdDelete = connection.CreateCommand();
            cmdDelete.Transaction = transaction;
            cmdDelete.CommandText = "DELETE FROM analitico_margem_pendente_itens_venda WHERE tenant_id = $tenantId AND venda_id = $vendaId;";
            cmdDelete.Parameters.AddWithValue("$tenantId", tenantId);
            cmdDelete.Parameters.AddWithValue("$vendaId", vendaId);
            await cmdDelete.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }, ct);

    public Task<FatoMargemProduto?> ObterAsync(string tenantId, string produtoId, DateOnly dia, CancellationToken ct = default)
        => ConsultarAsync(async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText =
                """
                SELECT tenant_id, produto_id, dia, receita_centavos, custo_centavos, atualizado_em_utc
                FROM fato_margem_produto WHERE tenant_id = $tenantId AND produto_id = $produtoId AND dia = $dia;
                """;
            cmd.Parameters.AddWithValue("$tenantId", tenantId);
            cmd.Parameters.AddWithValue("$produtoId", produtoId);
            cmd.Parameters.AddWithValue("$dia", Iso(dia));

            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            return await reader.ReadAsync(ct).ConfigureAwait(false) ? Ler(reader) : null;
        }, ct);

    public Task<IReadOnlyList<FatoMargemProduto>> ListarAsync(string tenantId, DateOnly de, DateOnly ate, CancellationToken ct = default)
        => ConsultarAsync(async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText =
                """
                SELECT tenant_id, produto_id, dia, receita_centavos, custo_centavos, atualizado_em_utc
                FROM fato_margem_produto
                WHERE tenant_id = $tenantId AND dia >= $de AND dia <= $ate
                ORDER BY dia ASC, produto_id ASC;
                """;
            cmd.Parameters.AddWithValue("$tenantId", tenantId);
            cmd.Parameters.AddWithValue("$de", Iso(de));
            cmd.Parameters.AddWithValue("$ate", Iso(ate));

            var resultado = new List<FatoMargemProduto>();
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false)) resultado.Add(Ler(reader));
            return (IReadOnlyList<FatoMargemProduto>)resultado;
        }, ct);

    public Task<IReadOnlyList<FatoMargemProduto>> ListarPorProdutoAsync(string tenantId, string produtoId, DateOnly de, DateOnly ate, CancellationToken ct = default)
        => ConsultarAsync(async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText =
                """
                SELECT tenant_id, produto_id, dia, receita_centavos, custo_centavos, atualizado_em_utc
                FROM fato_margem_produto
                WHERE tenant_id = $tenantId AND produto_id = $produtoId AND dia >= $de AND dia <= $ate
                ORDER BY dia ASC;
                """;
            cmd.Parameters.AddWithValue("$tenantId", tenantId);
            cmd.Parameters.AddWithValue("$produtoId", produtoId);
            cmd.Parameters.AddWithValue("$de", Iso(de));
            cmd.Parameters.AddWithValue("$ate", Iso(ate));

            var resultado = new List<FatoMargemProduto>();
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false)) resultado.Add(Ler(reader));
            return (IReadOnlyList<FatoMargemProduto>)resultado;
        }, ct);

    public Task ZerarTudoAsync(CancellationToken ct = default)
        => ExecutarAsync(async (connection, transaction) =>
        {
            await using var cmd1 = connection.CreateCommand();
            cmd1.Transaction = transaction;
            cmd1.CommandText = "DELETE FROM fato_margem_produto;";
            await cmd1.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

            await using var cmd2 = connection.CreateCommand();
            cmd2.Transaction = transaction;
            cmd2.CommandText = "DELETE FROM analitico_margem_pendente_itens_venda;";
            await cmd2.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }, ct);

    private static async Task UpsertReceitaAsync(
        SqliteConnection connection, SqliteTransaction? transaction, string tenantId, string produtoId, DateOnly dia, long deltaCentavos, CancellationToken ct)
    {
        await using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText =
            """
            INSERT INTO fato_margem_produto (tenant_id, produto_id, dia, receita_centavos, custo_centavos, atualizado_em_utc)
            VALUES ($tenantId, $produtoId, $dia, $delta, 0, $agora)
            ON CONFLICT(tenant_id, produto_id, dia) DO UPDATE SET
                receita_centavos  = receita_centavos + excluded.receita_centavos,
                atualizado_em_utc = excluded.atualizado_em_utc;
            """;
        cmd.Parameters.AddWithValue("$tenantId", tenantId);
        cmd.Parameters.AddWithValue("$produtoId", produtoId);
        cmd.Parameters.AddWithValue("$dia", Iso(dia));
        cmd.Parameters.AddWithValue("$delta", deltaCentavos);
        cmd.Parameters.AddWithValue("$agora", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private static async Task UpsertCustoAsync(
        SqliteConnection connection, SqliteTransaction? transaction, string tenantId, string produtoId, DateOnly dia, long deltaCentavos, CancellationToken ct)
    {
        await using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText =
            """
            INSERT INTO fato_margem_produto (tenant_id, produto_id, dia, receita_centavos, custo_centavos, atualizado_em_utc)
            VALUES ($tenantId, $produtoId, $dia, 0, $delta, $agora)
            ON CONFLICT(tenant_id, produto_id, dia) DO UPDATE SET
                custo_centavos    = custo_centavos + excluded.custo_centavos,
                atualizado_em_utc = excluded.atualizado_em_utc;
            """;
        cmd.Parameters.AddWithValue("$tenantId", tenantId);
        cmd.Parameters.AddWithValue("$produtoId", produtoId);
        cmd.Parameters.AddWithValue("$dia", Iso(dia));
        cmd.Parameters.AddWithValue("$delta", deltaCentavos);
        cmd.Parameters.AddWithValue("$agora", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private static FatoMargemProduto Ler(SqliteDataReader reader)
        => new(
            TenantId: reader.GetString(0),
            ProdutoId: reader.GetString(1),
            Dia: DateOnly.Parse(reader.GetString(2), CultureInfo.InvariantCulture),
            ReceitaCentavos: reader.GetInt64(3),
            CustoCentavos: reader.GetInt64(4),
            AtualizadoEmUtc: DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(5)));

    private static string Iso(DateOnly d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>Escreve dentro da sessão ambiente, se houver uma ativa; senão abre conexão própria
    /// e curta — mesmo molde de <c>SqliteContaAReceberRepository.ExecutarAsync</c> (que também
    /// grava header+parcelas em múltiplas instruções sem transação própria fora de sessão).</summary>
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
