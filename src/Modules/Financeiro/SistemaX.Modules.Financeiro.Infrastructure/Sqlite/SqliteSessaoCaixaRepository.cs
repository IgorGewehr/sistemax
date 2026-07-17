using System.Globalization;
using Microsoft.Data.Sqlite;
using SistemaX.Infrastructure.Local;
using SistemaX.Infrastructure.Local.UnitOfWork;
using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Domain.Caixa;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Infrastructure.Sqlite;

/// <summary>
/// Persistência REAL (SQLite) de <see cref="SessaoCaixa"/> — schema em
/// <see cref="FinanceiroSchemaMigrationV14"/>. Duas tabelas (sessão + movimentos, 1:N): toda
/// escrita de <see cref="SalvarAsync"/> faz upsert da sessão e substitui a lista de movimentos
/// inteira (DELETE + INSERT) — mais simples que diff incremental e seguro aqui porque
/// <see cref="MovimentoDeSessaoCaixa"/> é imutável (nunca edita uma linha já gravada, só adiciona
/// novas). Toda leitura filtra por <c>business_id</c> mesmo já tendo o <c>id</c> (R1).
/// </summary>
public sealed class SqliteSessaoCaixaRepository(ILocalSqliteConnectionFactory connectionFactory, ILocalSessao sessao)
    : ISessaoCaixaRepository
{
    private const string ColunasSessao =
        """
        id, business_id, conta_caixa_id, operador_id, operador_nome, aberta_em,
        saldo_abertura_centavos, saldo_abertura_moeda, status, fechada_em,
        saldo_informado_centavos, saldo_informado_moeda
        """;

    private const string ColunasMovimento =
        "id, sessao_id, tipo, valor_centavos, valor_moeda, motivo, registrado_em, operador_id, operador_nome";

    public Task<SessaoCaixa?> ObterPorIdAsync(string businessId, string id, CancellationToken ct = default)
        => ConsultarAsync(async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = $"SELECT {ColunasSessao} FROM sessoes_caixa WHERE business_id = $biz AND id = $id;";
            cmd.Parameters.AddWithValue("$biz", businessId);
            cmd.Parameters.AddWithValue("$id", id);

            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            if (!await reader.ReadAsync(ct).ConfigureAwait(false)) return null;

            var lida = LerSessaoSemMovimentos(reader);
            await reader.DisposeAsync().ConfigureAwait(false);

            var movimentos = await ListarMovimentosAsync(connection, transaction, id, ct).ConfigureAwait(false);
            return MontarComMovimentos(lida, movimentos);
        }, ct);

    public Task<SessaoCaixa?> ObterAbertaPorContaAsync(string businessId, string contaCaixaId, CancellationToken ct = default)
        => ConsultarAsync(async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText =
                $"""
                SELECT {ColunasSessao} FROM sessoes_caixa
                WHERE business_id = $biz AND conta_caixa_id = $conta AND status = $statusAberta
                ORDER BY aberta_em DESC
                LIMIT 1;
                """;
            cmd.Parameters.AddWithValue("$biz", businessId);
            cmd.Parameters.AddWithValue("$conta", contaCaixaId);
            cmd.Parameters.AddWithValue("$statusAberta", (int)StatusSessaoCaixa.Aberta);

            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            if (!await reader.ReadAsync(ct).ConfigureAwait(false)) return null;

            var lida = LerSessaoSemMovimentos(reader);
            await reader.DisposeAsync().ConfigureAwait(false);

            var movimentos = await ListarMovimentosAsync(connection, transaction, lida.Id, ct).ConfigureAwait(false);
            return MontarComMovimentos(lida, movimentos);
        }, ct);

    public Task<IReadOnlyList<SessaoCaixa>> ListarAsync(
        string businessId, string contaCaixaId, DateTimeOffset? de = null, DateTimeOffset? ate = null, CancellationToken ct = default)
        => ConsultarAsync(async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText =
                $"""
                SELECT {ColunasSessao} FROM sessoes_caixa
                WHERE business_id = $biz AND conta_caixa_id = $conta
                  AND ($de IS NULL OR aberta_em >= $de)
                  AND ($ate IS NULL OR aberta_em <= $ate)
                ORDER BY aberta_em DESC;
                """;
            cmd.Parameters.AddWithValue("$biz", businessId);
            cmd.Parameters.AddWithValue("$conta", contaCaixaId);
            cmd.Parameters.AddWithValue("$de", (object?)de?.ToString("O", CultureInfo.InvariantCulture) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$ate", (object?)ate?.ToString("O", CultureInfo.InvariantCulture) ?? DBNull.Value);

            var lidas = new List<SessaoSemMovimentos>();
            await using (var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false))
            {
                while (await reader.ReadAsync(ct).ConfigureAwait(false))
                {
                    lidas.Add(LerSessaoSemMovimentos(reader));
                }
            }

            var resultado = new List<SessaoCaixa>(lidas.Count);
            foreach (var lida in lidas)
            {
                var movimentos = await ListarMovimentosAsync(connection, transaction, lida.Id, ct).ConfigureAwait(false);
                resultado.Add(MontarComMovimentos(lida, movimentos));
            }

            return (IReadOnlyList<SessaoCaixa>)resultado;
        }, ct);

    public Task SalvarAsync(SessaoCaixa sessaoCaixa, CancellationToken ct = default)
        => ExecutarAsync(async (connection, transaction) =>
        {
            await using (var cmd = connection.CreateCommand())
            {
                cmd.Transaction = transaction;
                cmd.CommandText =
                    """
                    INSERT INTO sessoes_caixa
                        (id, business_id, conta_caixa_id, operador_id, operador_nome, aberta_em,
                         saldo_abertura_centavos, saldo_abertura_moeda, status, fechada_em,
                         saldo_informado_centavos, saldo_informado_moeda)
                    VALUES
                        ($id, $biz, $conta, $operadorId, $operadorNome, $abertaEm,
                         $saldoCentavos, $saldoMoeda, $status, $fechadaEm,
                         $informadoCentavos, $informadoMoeda)
                    ON CONFLICT(id) DO UPDATE SET
                        status                   = excluded.status,
                        fechada_em               = excluded.fechada_em,
                        saldo_informado_centavos = excluded.saldo_informado_centavos,
                        saldo_informado_moeda    = excluded.saldo_informado_moeda;
                    """;
                cmd.Parameters.AddWithValue("$id", sessaoCaixa.Id);
                cmd.Parameters.AddWithValue("$biz", sessaoCaixa.BusinessId);
                cmd.Parameters.AddWithValue("$conta", sessaoCaixa.ContaCaixaId);
                cmd.Parameters.AddWithValue("$operadorId", sessaoCaixa.OperadorId);
                cmd.Parameters.AddWithValue("$operadorNome", sessaoCaixa.OperadorNome);
                cmd.Parameters.AddWithValue("$abertaEm", Iso(sessaoCaixa.AbertaEm));
                cmd.Parameters.AddWithValue("$saldoCentavos", sessaoCaixa.SaldoAbertura.Centavos);
                cmd.Parameters.AddWithValue("$saldoMoeda", sessaoCaixa.SaldoAbertura.Moeda);
                cmd.Parameters.AddWithValue("$status", (int)sessaoCaixa.Status);
                cmd.Parameters.AddWithValue("$fechadaEm", (object?)(sessaoCaixa.FechadaEm is { } f ? Iso(f) : null) ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$informadoCentavos", (object?)sessaoCaixa.SaldoInformado?.Centavos ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$informadoMoeda", (object?)sessaoCaixa.SaldoInformado?.Moeda ?? DBNull.Value);

                await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }

            // Movimento é imutável (só INSERT, nunca UPDATE) — substituir a lista inteira por
            // DELETE+INSERT é seguro e mais simples que diff incremental (ver nota de tipo).
            await using (var del = connection.CreateCommand())
            {
                del.Transaction = transaction;
                del.CommandText = "DELETE FROM movimentos_sessao_caixa WHERE sessao_id = $sessaoId;";
                del.Parameters.AddWithValue("$sessaoId", sessaoCaixa.Id);
                await del.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }

            foreach (var movimento in sessaoCaixa.Movimentos)
            {
                await using var ins = connection.CreateCommand();
                ins.Transaction = transaction;
                ins.CommandText =
                    """
                    INSERT INTO movimentos_sessao_caixa
                        (id, sessao_id, tipo, valor_centavos, valor_moeda, motivo, registrado_em, operador_id, operador_nome)
                    VALUES
                        ($id, $sessaoId, $tipo, $valorCentavos, $valorMoeda, $motivo, $registradoEm, $operadorId, $operadorNome);
                    """;
                ins.Parameters.AddWithValue("$id", movimento.Id);
                ins.Parameters.AddWithValue("$sessaoId", sessaoCaixa.Id);
                ins.Parameters.AddWithValue("$tipo", (int)movimento.Tipo);
                ins.Parameters.AddWithValue("$valorCentavos", movimento.Valor.Centavos);
                ins.Parameters.AddWithValue("$valorMoeda", movimento.Valor.Moeda);
                ins.Parameters.AddWithValue("$motivo", (object?)movimento.Motivo ?? DBNull.Value);
                ins.Parameters.AddWithValue("$registradoEm", Iso(movimento.RegistradoEm));
                ins.Parameters.AddWithValue("$operadorId", movimento.OperadorId);
                ins.Parameters.AddWithValue("$operadorNome", movimento.OperadorNome);
                await ins.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }
        }, ct);

    /// <summary>Campos da tabela <c>sessoes_caixa</c> antes de juntar os movimentos — separado
    /// porque <see cref="SqliteDataReader"/> não pode ficar aberto enquanto uma segunda query
    /// (movimentos) roda na MESMA conexão.</summary>
    private readonly record struct SessaoSemMovimentos(
        string Id, string BusinessId, string ContaCaixaId, string OperadorId, string OperadorNome,
        DateTimeOffset AbertaEm, Money SaldoAbertura, StatusSessaoCaixa Status,
        DateTimeOffset? FechadaEm, Money? SaldoInformado);

    private static SessaoSemMovimentos LerSessaoSemMovimentos(SqliteDataReader reader) => new(
        Id: reader.GetString(0),
        BusinessId: reader.GetString(1),
        ContaCaixaId: reader.GetString(2),
        OperadorId: reader.GetString(3),
        OperadorNome: reader.GetString(4),
        AbertaEm: ParseData(reader.GetString(5)),
        SaldoAbertura: new Money(reader.GetInt64(6), reader.GetString(7)),
        Status: (StatusSessaoCaixa)reader.GetInt32(8),
        FechadaEm: reader.IsDBNull(9) ? null : ParseData(reader.GetString(9)),
        SaldoInformado: reader.IsDBNull(10) ? null : new Money(reader.GetInt64(10), reader.GetString(11)));

    private static SessaoCaixa MontarComMovimentos(SessaoSemMovimentos s, IReadOnlyList<MovimentoDeSessaoCaixa> movimentos)
        => SessaoCaixa.Reconstituir(
            s.Id, s.BusinessId, s.ContaCaixaId, s.OperadorId, s.OperadorNome, s.AbertaEm,
            s.SaldoAbertura, s.Status, movimentos, s.FechadaEm, s.SaldoInformado);

    private async Task<IReadOnlyList<MovimentoDeSessaoCaixa>> ListarMovimentosAsync(
        SqliteConnection connection, SqliteTransaction? transaction, string sessaoId, CancellationToken ct)
    {
        await using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = $"SELECT {ColunasMovimento} FROM movimentos_sessao_caixa WHERE sessao_id = $sessaoId ORDER BY registrado_em ASC;";
        cmd.Parameters.AddWithValue("$sessaoId", sessaoId);

        var resultado = new List<MovimentoDeSessaoCaixa>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            resultado.Add(MovimentoDeSessaoCaixa.Reconstituir(
                id: reader.GetString(0),
                tipo: (TipoMovimentoCaixa)reader.GetInt32(2),
                valor: new Money(reader.GetInt64(3), reader.GetString(4)),
                motivo: reader.IsDBNull(5) ? null : reader.GetString(5),
                registradoEm: ParseData(reader.GetString(6)),
                operadorId: reader.GetString(7),
                operadorNome: reader.GetString(8)));
        }
        return resultado;
    }

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
