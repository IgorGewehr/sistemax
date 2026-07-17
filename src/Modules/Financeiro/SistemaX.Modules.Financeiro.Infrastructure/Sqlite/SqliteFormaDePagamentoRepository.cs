using System.Globalization;
using Microsoft.Data.Sqlite;
using SistemaX.Infrastructure.Local;
using SistemaX.Infrastructure.Local.UnitOfWork;
using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Domain.Caixa;

namespace SistemaX.Modules.Financeiro.Infrastructure.Sqlite;

/// <summary>
/// Persistência REAL (SQLite) de <see cref="FormaDePagamento"/> — schema em
/// <see cref="FinanceiroSchemaMigrationV13"/>. <see cref="ObterPorNomeAsync"/> é o caminho que
/// <c>FatoRecebiveisProjection</c> percorre para resolver MDR/lag — comparação case-insensitive
/// feita em C# (poucas dezenas de formas por tenant, sem necessidade de <c>COLLATE NOCASE</c> em
/// SQL; mesma escolha de simplicidade de <c>SqliteMovimentoFinanceiroRepository.CalcularSaldoAsync</c>).
/// </summary>
public sealed class SqliteFormaDePagamentoRepository(ILocalSqliteConnectionFactory connectionFactory, ILocalSessao sessao)
    : IFormaDePagamentoRepository
{
    private const string Colunas =
        """
        id, business_id, nome, tipo, taxa_percentual, prazo_compensacao_dias, conta_liquidacao_id, ativo
        """;

    public Task<FormaDePagamento?> ObterPorIdAsync(string businessId, string id, CancellationToken ct = default)
        => ConsultarAsync(async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = $"SELECT {Colunas} FROM formas_pagamento WHERE business_id = $biz AND id = $id;";
            cmd.Parameters.AddWithValue("$biz", businessId);
            cmd.Parameters.AddWithValue("$id", id);

            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            return await reader.ReadAsync(ct).ConfigureAwait(false) ? Ler(reader) : null;
        }, ct);

    public Task<FormaDePagamento?> ObterPorNomeAsync(string businessId, string nome, CancellationToken ct = default)
        => ConsultarAsync(async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = $"SELECT {Colunas} FROM formas_pagamento WHERE business_id = $biz;";
            cmd.Parameters.AddWithValue("$biz", businessId);

            var alvo = nome.Trim();
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                var candidata = Ler(reader);
                if (string.Equals(candidata.Nome.Trim(), alvo, StringComparison.OrdinalIgnoreCase))
                {
                    return candidata;
                }
            }
            return null;
        }, ct);

    public Task<IReadOnlyList<FormaDePagamento>> ListarAsync(string businessId, bool apenasAtivas = false, CancellationToken ct = default)
        => ConsultarAsync(async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = apenasAtivas
                ? $"SELECT {Colunas} FROM formas_pagamento WHERE business_id = $biz AND ativo = 1 ORDER BY nome ASC;"
                : $"SELECT {Colunas} FROM formas_pagamento WHERE business_id = $biz ORDER BY nome ASC;";
            cmd.Parameters.AddWithValue("$biz", businessId);

            var resultado = new List<FormaDePagamento>();
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                resultado.Add(Ler(reader));
            }
            return (IReadOnlyList<FormaDePagamento>)resultado;
        }, ct);

    public Task SalvarAsync(FormaDePagamento forma, CancellationToken ct = default)
        => ExecutarAsync(async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText =
                """
                INSERT INTO formas_pagamento
                    (id, business_id, nome, tipo, taxa_percentual, prazo_compensacao_dias, conta_liquidacao_id, ativo)
                VALUES
                    ($id, $biz, $nome, $tipo, $taxa, $prazo, $contaLiquidacao, $ativo)
                ON CONFLICT(id) DO UPDATE SET
                    nome                    = excluded.nome,
                    tipo                    = excluded.tipo,
                    taxa_percentual         = excluded.taxa_percentual,
                    prazo_compensacao_dias  = excluded.prazo_compensacao_dias,
                    conta_liquidacao_id     = excluded.conta_liquidacao_id,
                    ativo                   = excluded.ativo;
                """;
            cmd.Parameters.AddWithValue("$id", forma.Id);
            cmd.Parameters.AddWithValue("$biz", forma.BusinessId);
            cmd.Parameters.AddWithValue("$nome", forma.Nome);
            cmd.Parameters.AddWithValue("$tipo", (int)forma.Tipo);
            cmd.Parameters.AddWithValue("$taxa", forma.TaxaPercentual.ToString(CultureInfo.InvariantCulture));
            cmd.Parameters.AddWithValue("$prazo", forma.PrazoCompensacaoDias);
            cmd.Parameters.AddWithValue("$contaLiquidacao", (object?)forma.ContaLiquidacaoId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$ativo", forma.Ativo ? 1 : 0);

            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }, ct);

    private static FormaDePagamento Ler(SqliteDataReader reader)
        => FormaDePagamento.Reconstituir(
            id: reader.GetString(0),
            businessId: reader.GetString(1),
            nome: reader.GetString(2),
            tipo: (TipoFormaPagamento)reader.GetInt32(3),
            taxaPercentual: decimal.Parse(reader.GetString(4), CultureInfo.InvariantCulture),
            prazoCompensacaoDias: reader.GetInt32(5),
            contaLiquidacaoId: reader.IsDBNull(6) ? null : reader.GetString(6),
            ativo: reader.GetInt32(7) != 0);

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
