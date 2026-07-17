using System.Globalization;
using Microsoft.Data.Sqlite;
using SistemaX.Infrastructure.Local;
using SistemaX.Infrastructure.Local.UnitOfWork;
using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Domain.Caixa;
using SistemaX.Modules.Financeiro.Domain.Comum;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Infrastructure.Sqlite;

/// <summary>
/// Persistência REAL (SQLite) de <see cref="MovimentoFinanceiro"/> — IMUTÁVEL por invariante do
/// agregado (nunca editado/apagado; corrigir é <c>GerarEstorno</c>, um novo movimento), então
/// <see cref="SalvarAsync"/> é <c>INSERT ... ON CONFLICT(id) DO NOTHING</c> (insert-only), nunca
/// upsert com <c>DO UPDATE</c>. <see cref="CalcularSaldoAsync"/> soma em C# (não em SQL) — mais
/// simples e sem risco de bug sutil, espelhando exatamente <c>InMemoryMovimentoFinanceiroRepository</c>.
/// </summary>
public sealed class SqliteMovimentoFinanceiroRepository(ILocalSqliteConnectionFactory connectionFactory, ILocalSessao sessao)
    : IMovimentoFinanceiroRepository
{
    private const string Colunas =
        """
        id, business_id, conta_bancaria_caixa_id, forma_pagamento_id, parcela_id, conta_origem_id,
        tipo, valor_centavos, valor_moeda, data_movimento, origem_modulo, origem_id, reversal_of_id, criado_em, corrente
        """;

    public Task<MovimentoFinanceiro?> ObterPorIdAsync(string id, CancellationToken ct = default)
        => ConsultarAsync(async (connection, transaction) =>
        {
            var lista = await LerMovimentosAsync(connection, transaction, "WHERE id = $id",
                cmd => cmd.Parameters.AddWithValue("$id", id), ct).ConfigureAwait(false);
            return lista.Count > 0 ? lista[0] : null;
        }, ct);

    public Task<MovimentoFinanceiro?> BuscarPorOrigemAsync(string businessId, string sourceRefChave, CancellationToken ct = default)
        => ConsultarAsync(async (connection, transaction) =>
        {
            var lista = await LerMovimentosAsync(connection, transaction, "WHERE business_id = $biz AND origem_chave = $chave",
                cmd =>
                {
                    cmd.Parameters.AddWithValue("$biz", businessId);
                    cmd.Parameters.AddWithValue("$chave", sourceRefChave);
                }, ct).ConfigureAwait(false);
            return lista.Count > 0 ? lista[0] : null;
        }, ct);

    public Task<MovimentoFinanceiro?> BuscarEstornoDeAsync(string movimentoOriginalId, CancellationToken ct = default)
        => ConsultarAsync(async (connection, transaction) =>
        {
            var lista = await LerMovimentosAsync(connection, transaction, "WHERE reversal_of_id = $id",
                cmd => cmd.Parameters.AddWithValue("$id", movimentoOriginalId), ct).ConfigureAwait(false);
            return lista.Count > 0 ? lista[0] : null;
        }, ct);

    public Task<IReadOnlyList<MovimentoFinanceiro>> ListarPorContaOrigemAsync(string businessId, string contaOrigemId, CancellationToken ct = default)
        => ConsultarAsync(async (connection, transaction) =>
        {
            var lista = await LerMovimentosAsync(connection, transaction, "WHERE business_id = $biz AND conta_origem_id = $contaOrigem",
                cmd =>
                {
                    cmd.Parameters.AddWithValue("$biz", businessId);
                    cmd.Parameters.AddWithValue("$contaOrigem", contaOrigemId);
                }, ct).ConfigureAwait(false);
            return (IReadOnlyList<MovimentoFinanceiro>)lista;
        }, ct);

    public Task<IReadOnlyList<MovimentoFinanceiro>> ListarPorPeriodoAsync(string businessId, DateTimeOffset inicio, DateTimeOffset fim, CancellationToken ct = default)
        => ConsultarAsync(async (connection, transaction) =>
        {
            var lista = await LerMovimentosAsync(connection, transaction,
                "WHERE business_id = $biz AND data_movimento >= $inicio AND data_movimento <= $fim",
                cmd =>
                {
                    cmd.Parameters.AddWithValue("$biz", businessId);
                    cmd.Parameters.AddWithValue("$inicio", Iso(inicio));
                    cmd.Parameters.AddWithValue("$fim", Iso(fim));
                }, ct).ConfigureAwait(false);
            return (IReadOnlyList<MovimentoFinanceiro>)lista;
        }, ct);

    public Task<Money> CalcularSaldoAsync(string businessId, string? contaBancariaCaixaId, DateTimeOffset ateData, CancellationToken ct = default)
        => ConsultarAsync(async (connection, transaction) =>
        {
            var filtro = "WHERE business_id = $biz AND data_movimento <= $ate";
            if (contaBancariaCaixaId is not null) filtro += " AND conta_bancaria_caixa_id = $conta";

            var lista = await LerMovimentosAsync(connection, transaction, filtro,
                cmd =>
                {
                    cmd.Parameters.AddWithValue("$biz", businessId);
                    cmd.Parameters.AddWithValue("$ate", Iso(ateData));
                    if (contaBancariaCaixaId is not null) cmd.Parameters.AddWithValue("$conta", contaBancariaCaixaId);
                }, ct).ConfigureAwait(false);

            return lista.Aggregate(Money.Zero, (acumulado, m) =>
                m.Tipo == TipoMovimentoFinanceiro.Entrada ? acumulado + m.Valor : acumulado - m.Valor);
        }, ct);

    public Task SalvarAsync(MovimentoFinanceiro movimento, CancellationToken ct = default)
        => ExecutarAsync(async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText =
                """
                INSERT INTO movimentos_financeiros
                    (id, business_id, conta_bancaria_caixa_id, forma_pagamento_id, parcela_id, conta_origem_id,
                     tipo, valor_centavos, valor_moeda, data_movimento, origem_modulo, origem_id, origem_chave, reversal_of_id, criado_em, corrente)
                VALUES
                    ($id, $biz, $contaCaixa, $formaPag, $parcelaId, $contaOrigem,
                     $tipo, $valorCentavos, $valorMoeda, $dataMovimento, $origemModulo, $origemId, $origemChave, $reversalOfId, $criadoEm, $corrente)
                ON CONFLICT(id) DO NOTHING;
                """;
            cmd.Parameters.AddWithValue("$id", movimento.Id);
            cmd.Parameters.AddWithValue("$biz", movimento.BusinessId);
            cmd.Parameters.AddWithValue("$contaCaixa", movimento.ContaBancariaCaixaId);
            cmd.Parameters.AddWithValue("$formaPag", movimento.FormaPagamentoId);
            cmd.Parameters.AddWithValue("$parcelaId", movimento.ParcelaId);
            cmd.Parameters.AddWithValue("$contaOrigem", movimento.ContaOrigemId);
            cmd.Parameters.AddWithValue("$tipo", (int)movimento.Tipo);
            cmd.Parameters.AddWithValue("$valorCentavos", movimento.Valor.Centavos);
            cmd.Parameters.AddWithValue("$valorMoeda", movimento.Valor.Moeda);
            cmd.Parameters.AddWithValue("$dataMovimento", Iso(movimento.DataMovimento));
            cmd.Parameters.AddWithValue("$origemModulo", movimento.Origem.Modulo);
            cmd.Parameters.AddWithValue("$origemId", movimento.Origem.Id);
            cmd.Parameters.AddWithValue("$origemChave", movimento.Origem.Chave);
            cmd.Parameters.AddWithValue("$reversalOfId", (object?)movimento.ReversalOfId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$criadoEm", Iso(movimento.CriadoEm));
            cmd.Parameters.AddWithValue("$corrente", (object?)(movimento.Corrente is { } corrente ? (int)corrente : null) ?? DBNull.Value);

            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }, ct);

    private static async Task<List<MovimentoFinanceiro>> LerMovimentosAsync(
        SqliteConnection connection, SqliteTransaction? transaction, string whereClause,
        Action<SqliteCommand> configurarParametros, CancellationToken ct)
    {
        await using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = $"SELECT {Colunas} FROM movimentos_financeiros {whereClause};";
        configurarParametros(cmd);

        var resultado = new List<MovimentoFinanceiro>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            resultado.Add(MovimentoFinanceiro.Reconstituir(
                id: reader.GetString(0),
                businessId: reader.GetString(1),
                contaBancariaCaixaId: reader.GetString(2),
                formaPagamentoId: reader.GetString(3),
                parcelaId: reader.GetString(4),
                contaOrigemId: reader.GetString(5),
                tipo: (TipoMovimentoFinanceiro)reader.GetInt32(6),
                valor: new Money(reader.GetInt64(7), reader.GetString(8)),
                dataMovimento: ParseData(reader.GetString(9))!.Value,
                origem: new SourceRef(reader.GetString(10), reader.GetString(11)),
                reversalOfId: reader.IsDBNull(12) ? null : reader.GetString(12),
                criadoEm: ParseData(reader.GetString(13))!.Value,
                corrente: reader.IsDBNull(14) ? null : (CorrenteDeReceita)reader.GetInt32(14)));
        }
        return resultado;
    }

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
