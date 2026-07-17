using System.Globalization;
using Microsoft.Data.Sqlite;
using SistemaX.Infrastructure.Local;
using SistemaX.Infrastructure.Local.UnitOfWork;
using SistemaX.Modules.Vendas.Application.Ports;
using SistemaX.Modules.Vendas.Domain;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Vendas.Infrastructure.Sqlite;

/// <summary>
/// Persistência REAL (SQLite) de <see cref="Venda"/> — segue o repositório-molde da F0
/// (<c>SqliteFornecedorRepository</c>, ver docs/persistencia/persistencia-sqlite.md), com a
/// diferença de que <see cref="Venda"/> tem DOIS filhos mutáveis (<see cref="Venda.Itens"/> e
/// <see cref="Venda.Pagamentos"/>): cada <see cref="SalvarAsync"/> faz upsert do cabeçalho e
/// DELETE+reinsert de ambas as tabelas filhas, tudo na MESMA conexão/transação — é isso que dá
/// crash-safety ao carrinho do PDV (ver nota de MONTAGEM vs PAGAMENTO em <c>Venda</c>): a venda
/// inteira é persistida a cada mudança, não só na conclusão.
///
/// Reidratação usa <see cref="Venda.Reconstituir"/>/<see cref="ItemDeVenda.Reconstituir"/>/
/// <see cref="PagamentoDeVenda.Reconstituir"/> — nunca os construtores de negócio, que
/// validariam de novo e (no caso de <see cref="Venda.Abrir"/>) mintariam um Id novo.
/// </summary>
public sealed class SqliteVendaRepository(ILocalSqliteConnectionFactory connectionFactory, ILocalSessao sessao)
    : IVendaRepository
{
    public Task<Venda?> ObterPorIdAsync(string id, CancellationToken ct = default)
        => ConsultarAsync(async (connection, transaction) =>
        {
            await using var cmdCabecalho = connection.CreateCommand();
            cmdCabecalho.Transaction = transaction;
            cmdCabecalho.CommandText =
                """
                SELECT id, tenant_id, status, desconto_venda_centavos, desconto_venda_moeda, motivo_desconto_venda, cliente_id
                FROM vendas
                WHERE id = $id;
                """;
            cmdCabecalho.Parameters.AddWithValue("$id", id);

            string tenantId;
            StatusVenda status;
            Money descontoVenda;
            string? motivoDescontoVenda;
            string? clienteId;

            await using (var reader = await cmdCabecalho.ExecuteReaderAsync(ct).ConfigureAwait(false))
            {
                if (!await reader.ReadAsync(ct).ConfigureAwait(false))
                {
                    return null;
                }

                tenantId = reader.GetString(1);
                status = (StatusVenda)reader.GetInt32(2);
                descontoVenda = new Money(reader.GetInt64(3), reader.GetString(4));
                motivoDescontoVenda = reader.IsDBNull(5) ? null : reader.GetString(5);
                clienteId = reader.IsDBNull(6) ? null : reader.GetString(6);
            }

            var itens = new List<ItemDeVenda>();
            await using (var cmdItens = connection.CreateCommand())
            {
                cmdItens.Transaction = transaction;
                cmdItens.CommandText =
                    """
                    SELECT id, produto_id, descricao, quantidade, preco_unit_centavos, preco_unit_moeda, desconto_centavos, desconto_moeda
                    FROM venda_itens
                    WHERE venda_id = $vendaId;
                    """;
                cmdItens.Parameters.AddWithValue("$vendaId", id);

                await using var reader = await cmdItens.ExecuteReaderAsync(ct).ConfigureAwait(false);
                while (await reader.ReadAsync(ct).ConfigureAwait(false))
                {
                    itens.Add(ItemDeVenda.Reconstituir(
                        id: reader.GetString(0),
                        produtoId: reader.GetString(1),
                        descricao: reader.GetString(2),
                        quantidade: reader.GetInt32(3),
                        precoUnitario: new Money(reader.GetInt64(4), reader.GetString(5)),
                        desconto: new Money(reader.GetInt64(6), reader.GetString(7))));
                }
            }

            var pagamentos = new List<PagamentoDeVenda>();
            await using (var cmdPagamentos = connection.CreateCommand())
            {
                cmdPagamentos.Transaction = transaction;
                cmdPagamentos.CommandText =
                    """
                    SELECT id, metodo, valor_centavos, valor_moeda, valor_recebido_centavos, valor_recebido_moeda, registrado_em
                    FROM venda_pagamentos
                    WHERE venda_id = $vendaId;
                    """;
                cmdPagamentos.Parameters.AddWithValue("$vendaId", id);

                await using var reader = await cmdPagamentos.ExecuteReaderAsync(ct).ConfigureAwait(false);
                while (await reader.ReadAsync(ct).ConfigureAwait(false))
                {
                    var valorRecebido = reader.IsDBNull(4)
                        ? (Money?)null
                        : new Money(reader.GetInt64(4), reader.GetString(5));

                    pagamentos.Add(PagamentoDeVenda.Reconstituir(
                        id: reader.GetString(0),
                        metodo: (MetodoPagamento)reader.GetInt32(1),
                        valor: new Money(reader.GetInt64(2), reader.GetString(3)),
                        valorRecebido: valorRecebido,
                        registradoEm: ParseData(reader.GetString(6))));
                }
            }

            return Venda.Reconstituir(id, tenantId, status, itens, pagamentos, descontoVenda, motivoDescontoVenda, clienteId);
        }, ct);

    public Task SalvarAsync(Venda venda, CancellationToken ct = default)
        => ExecutarAsync(async (connection, transaction) =>
        {
            await using (var cmdCabecalho = connection.CreateCommand())
            {
                cmdCabecalho.Transaction = transaction;
                cmdCabecalho.CommandText =
                    """
                    INSERT INTO vendas (id, tenant_id, status, desconto_venda_centavos, desconto_venda_moeda, motivo_desconto_venda, cliente_id)
                    VALUES ($id, $tenantId, $status, $descontoCentavos, $descontoMoeda, $motivoDesconto, $clienteId)
                    ON CONFLICT(id) DO UPDATE SET
                        status                  = excluded.status,
                        desconto_venda_centavos = excluded.desconto_venda_centavos,
                        desconto_venda_moeda    = excluded.desconto_venda_moeda,
                        motivo_desconto_venda   = excluded.motivo_desconto_venda,
                        cliente_id              = excluded.cliente_id;
                    """;
                cmdCabecalho.Parameters.AddWithValue("$id", venda.Id);
                cmdCabecalho.Parameters.AddWithValue("$tenantId", venda.TenantId);
                cmdCabecalho.Parameters.AddWithValue("$status", (int)venda.Status);
                cmdCabecalho.Parameters.AddWithValue("$descontoCentavos", venda.DescontoVenda.Centavos);
                cmdCabecalho.Parameters.AddWithValue("$descontoMoeda", venda.DescontoVenda.Moeda);
                cmdCabecalho.Parameters.AddWithValue("$motivoDesconto", (object?)venda.MotivoDescontoVenda ?? DBNull.Value);
                cmdCabecalho.Parameters.AddWithValue("$clienteId", (object?)venda.ClienteId ?? DBNull.Value);

                await cmdCabecalho.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }

            await using (var cmdDeleteItens = connection.CreateCommand())
            {
                cmdDeleteItens.Transaction = transaction;
                cmdDeleteItens.CommandText = "DELETE FROM venda_itens WHERE venda_id = $vendaId;";
                cmdDeleteItens.Parameters.AddWithValue("$vendaId", venda.Id);
                await cmdDeleteItens.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }

            foreach (var item in venda.Itens)
            {
                await using var cmdItem = connection.CreateCommand();
                cmdItem.Transaction = transaction;
                cmdItem.CommandText =
                    """
                    INSERT INTO venda_itens
                        (id, venda_id, produto_id, descricao, quantidade, preco_unit_centavos, preco_unit_moeda, desconto_centavos, desconto_moeda)
                    VALUES
                        ($id, $vendaId, $produtoId, $descricao, $quantidade, $precoCentavos, $precoMoeda, $descontoCentavos, $descontoMoeda);
                    """;
                cmdItem.Parameters.AddWithValue("$id", item.Id);
                cmdItem.Parameters.AddWithValue("$vendaId", venda.Id);
                cmdItem.Parameters.AddWithValue("$produtoId", item.ProdutoId);
                cmdItem.Parameters.AddWithValue("$descricao", item.Descricao);
                cmdItem.Parameters.AddWithValue("$quantidade", item.Quantidade);
                cmdItem.Parameters.AddWithValue("$precoCentavos", item.PrecoUnitario.Centavos);
                cmdItem.Parameters.AddWithValue("$precoMoeda", item.PrecoUnitario.Moeda);
                cmdItem.Parameters.AddWithValue("$descontoCentavos", item.Desconto.Centavos);
                cmdItem.Parameters.AddWithValue("$descontoMoeda", item.Desconto.Moeda);

                await cmdItem.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }

            await using (var cmdDeletePagamentos = connection.CreateCommand())
            {
                cmdDeletePagamentos.Transaction = transaction;
                cmdDeletePagamentos.CommandText = "DELETE FROM venda_pagamentos WHERE venda_id = $vendaId;";
                cmdDeletePagamentos.Parameters.AddWithValue("$vendaId", venda.Id);
                await cmdDeletePagamentos.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }

            foreach (var pagamento in venda.Pagamentos)
            {
                await using var cmdPagamento = connection.CreateCommand();
                cmdPagamento.Transaction = transaction;
                cmdPagamento.CommandText =
                    """
                    INSERT INTO venda_pagamentos
                        (id, venda_id, metodo, valor_centavos, valor_moeda, valor_recebido_centavos, valor_recebido_moeda, registrado_em)
                    VALUES
                        ($id, $vendaId, $metodo, $valorCentavos, $valorMoeda, $valorRecebidoCentavos, $valorRecebidoMoeda, $registradoEm);
                    """;
                cmdPagamento.Parameters.AddWithValue("$id", pagamento.Id);
                cmdPagamento.Parameters.AddWithValue("$vendaId", venda.Id);
                cmdPagamento.Parameters.AddWithValue("$metodo", (int)pagamento.Metodo);
                cmdPagamento.Parameters.AddWithValue("$valorCentavos", pagamento.Valor.Centavos);
                cmdPagamento.Parameters.AddWithValue("$valorMoeda", pagamento.Valor.Moeda);
                cmdPagamento.Parameters.AddWithValue("$valorRecebidoCentavos", (object?)pagamento.ValorRecebido?.Centavos ?? DBNull.Value);
                cmdPagamento.Parameters.AddWithValue("$valorRecebidoMoeda", (object?)pagamento.ValorRecebido?.Moeda ?? DBNull.Value);
                cmdPagamento.Parameters.AddWithValue("$registradoEm", Iso(pagamento.RegistradoEm));

                await cmdPagamento.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }
        }, ct);

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
