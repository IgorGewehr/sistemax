using System.Globalization;
using Microsoft.Data.Sqlite;
using SistemaX.Infrastructure.Local;
using SistemaX.Infrastructure.Local.UnitOfWork;
using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Domain.Comum;
using SistemaX.Modules.Financeiro.Domain.ContasAPagarReceber;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Infrastructure.Sqlite;

/// <summary>
/// Persistência REAL (SQLite) de <see cref="ContaAReceber"/> — segue o molde de
/// <c>SqliteFornecedorRepository</c> (docs/persistencia/persistencia-sqlite.md), com a variação de
/// ter uma coleção filha MUTÁVEL (<see cref="Parcela"/>): toda escrita faz
/// <c>DELETE FROM parcelas_a_receber WHERE conta_id = @id</c> seguido do reinsert de todas as
/// parcelas atuais, dentro da MESMA operação (<see cref="ExecutarAsync"/>) que o upsert do header
/// — atômico com ou sem sessão ambiente. Reidratação usa <see cref="ContaAReceber.Reconstituir"/>
/// e <see cref="Parcela.Reconstituir"/> — nunca os construtores de negócio.
/// </summary>
public sealed class SqliteContaAReceberRepository(ILocalSqliteConnectionFactory connectionFactory, ILocalSessao sessao)
    : IContaAReceberRepository
{
    private const string ColunasHeader =
        """
        id, business_id, source_ref_modulo, source_ref_id, descricao, categoria_id,
        centro_de_custo_id, data_competencia, valor_total_centavos, valor_total_moeda,
        status, criado_em, cliente_id, corrente, tecnico_id, valor_servico_centavos, valor_pecas_centavos,
        meses_de_reconhecimento, projeto_id
        """;

    public Task<ContaAReceber?> ObterPorIdAsync(string id, CancellationToken ct = default)
        => ConsultarAsync(async (connection, transaction) =>
        {
            var contas = await LerContasAsync(connection, transaction, "WHERE id = $id",
                cmd => cmd.Parameters.AddWithValue("$id", id), ct).ConfigureAwait(false);
            return contas.Count > 0 ? contas[0] : null;
        }, ct);

    public Task<ContaAReceber?> BuscarPorOrigemAsync(string businessId, string sourceRefChave, CancellationToken ct = default)
        => ConsultarAsync(async (connection, transaction) =>
        {
            var contas = await LerContasAsync(connection, transaction, "WHERE business_id = $biz AND source_ref_chave = $chave",
                cmd =>
                {
                    cmd.Parameters.AddWithValue("$biz", businessId);
                    cmd.Parameters.AddWithValue("$chave", sourceRefChave);
                }, ct).ConfigureAwait(false);
            return contas.Count > 0 ? contas[0] : null;
        }, ct);

    public Task<IReadOnlyList<ContaAReceber>> ListarPorCompetenciaAsync(string businessId, DateTimeOffset inicio, DateTimeOffset fim, CancellationToken ct = default)
        => ConsultarAsync(async (connection, transaction) =>
        {
            var contas = await LerContasAsync(connection, transaction,
                "WHERE business_id = $biz AND data_competencia >= $inicio AND data_competencia <= $fim",
                cmd =>
                {
                    cmd.Parameters.AddWithValue("$biz", businessId);
                    cmd.Parameters.AddWithValue("$inicio", Iso(inicio));
                    cmd.Parameters.AddWithValue("$fim", Iso(fim));
                }, ct).ConfigureAwait(false);
            return (IReadOnlyList<ContaAReceber>)contas;
        }, ct);

    /// <summary>Espelha o in-memory: conta entra se TEM alguma parcela aberta/parcial/atrasada
    /// (uma condição) E TEM alguma parcela vencendo até a referência (outra condição) — não
    /// necessariamente a MESMA parcela nas duas (dois <c>Any()</c> independentes no LINQ original).</summary>
    public Task<IReadOnlyList<ContaAReceber>> ListarAbertasAteAsync(string businessId, DateTimeOffset referencia, CancellationToken ct = default)
        => ConsultarAsync(async (connection, transaction) =>
        {
            var contas = await LerContasAsync(connection, transaction,
                """
                WHERE business_id = $biz
                  AND id IN (SELECT conta_id FROM parcelas_a_receber WHERE status IN ($aberto, $parcial, $atrasado))
                  AND id IN (SELECT conta_id FROM parcelas_a_receber WHERE vencimento <= $referencia)
                """,
                cmd =>
                {
                    cmd.Parameters.AddWithValue("$biz", businessId);
                    cmd.Parameters.AddWithValue("$aberto", (int)StatusFinanceiro.Aberto);
                    cmd.Parameters.AddWithValue("$parcial", (int)StatusFinanceiro.Parcial);
                    cmd.Parameters.AddWithValue("$atrasado", (int)StatusFinanceiro.Atrasado);
                    cmd.Parameters.AddWithValue("$referencia", Iso(referencia));
                }, ct).ConfigureAwait(false);
            return (IReadOnlyList<ContaAReceber>)contas;
        }, ct);

    public Task SalvarAsync(ContaAReceber conta, CancellationToken ct = default)
        => ExecutarAsync(async (connection, transaction) =>
        {
            await using (var cmd = connection.CreateCommand())
            {
                cmd.Transaction = transaction;
                cmd.CommandText =
                    """
                    INSERT INTO contas_a_receber
                        (id, business_id, source_ref_modulo, source_ref_id, source_ref_chave, descricao, categoria_id,
                         centro_de_custo_id, data_competencia, valor_total_centavos, valor_total_moeda, status, criado_em, cliente_id, corrente,
                         tecnico_id, valor_servico_centavos, valor_pecas_centavos, meses_de_reconhecimento, projeto_id)
                    VALUES
                        ($id, $biz, $srModulo, $srId, $srChave, $descricao, $categoriaId,
                         $centroDeCustoId, $dataCompetencia, $valorCentavos, $valorMoeda, $status, $criadoEm, $clienteId, $corrente,
                         $tecnicoId, $valorServicoCentavos, $valorPecasCentavos, $mesesDeReconhecimento, $projetoId)
                    ON CONFLICT(id) DO UPDATE SET
                        descricao                = excluded.descricao,
                        categoria_id             = excluded.categoria_id,
                        centro_de_custo_id       = excluded.centro_de_custo_id,
                        data_competencia         = excluded.data_competencia,
                        valor_total_centavos     = excluded.valor_total_centavos,
                        valor_total_moeda        = excluded.valor_total_moeda,
                        status                   = excluded.status,
                        cliente_id               = excluded.cliente_id,
                        corrente                 = excluded.corrente,
                        tecnico_id               = excluded.tecnico_id,
                        valor_servico_centavos   = excluded.valor_servico_centavos,
                        valor_pecas_centavos     = excluded.valor_pecas_centavos,
                        meses_de_reconhecimento  = excluded.meses_de_reconhecimento,
                        projeto_id               = excluded.projeto_id;
                    """;
                cmd.Parameters.AddWithValue("$id", conta.Id);
                cmd.Parameters.AddWithValue("$biz", conta.BusinessId);
                cmd.Parameters.AddWithValue("$srModulo", conta.SourceRef.Modulo);
                cmd.Parameters.AddWithValue("$srId", conta.SourceRef.Id);
                cmd.Parameters.AddWithValue("$srChave", conta.SourceRef.Chave);
                cmd.Parameters.AddWithValue("$descricao", conta.Descricao);
                cmd.Parameters.AddWithValue("$categoriaId", conta.CategoriaId);
                cmd.Parameters.AddWithValue("$centroDeCustoId", (object?)conta.CentroDeCustoId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$dataCompetencia", Iso(conta.DataCompetencia));
                cmd.Parameters.AddWithValue("$valorCentavos", conta.ValorTotal.Centavos);
                cmd.Parameters.AddWithValue("$valorMoeda", conta.ValorTotal.Moeda);
                cmd.Parameters.AddWithValue("$status", (int)conta.Status);
                cmd.Parameters.AddWithValue("$criadoEm", Iso(conta.CriadoEm));
                cmd.Parameters.AddWithValue("$clienteId", (object?)conta.ClienteId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$corrente", (object?)(conta.Corrente is { } corrente ? (int)corrente : null) ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$tecnicoId", (object?)conta.TecnicoId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$valorServicoCentavos", (object?)(conta.ValorServico is { } vs ? vs.Centavos : null) ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$valorPecasCentavos", (object?)(conta.ValorPecas is { } vp ? vp.Centavos : null) ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$mesesDeReconhecimento", (object?)conta.MesesDeReconhecimento ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$projetoId", (object?)conta.ProjetoId ?? DBNull.Value);

                await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }

            await using (var delCmd = connection.CreateCommand())
            {
                delCmd.Transaction = transaction;
                delCmd.CommandText = "DELETE FROM parcelas_a_receber WHERE conta_id = $contaId;";
                delCmd.Parameters.AddWithValue("$contaId", conta.Id);
                await delCmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }

            foreach (var parcela in conta.Parcelas)
            {
                await using var insCmd = connection.CreateCommand();
                insCmd.Transaction = transaction;
                insCmd.CommandText =
                    """
                    INSERT INTO parcelas_a_receber
                        (id, conta_id, numero, vencimento, valor_centavos, valor_moeda, valor_pago_centavos, status, data_liquidacao, forma_pagamento_id)
                    VALUES
                        ($id, $contaId, $numero, $vencimento, $valorCentavos, $valorMoeda, $valorPagoCentavos, $status, $dataLiquidacao, $formaPagamentoId);
                    """;
                insCmd.Parameters.AddWithValue("$id", parcela.Id);
                insCmd.Parameters.AddWithValue("$contaId", conta.Id);
                insCmd.Parameters.AddWithValue("$numero", parcela.Numero);
                insCmd.Parameters.AddWithValue("$vencimento", Iso(parcela.Vencimento));
                insCmd.Parameters.AddWithValue("$valorCentavos", parcela.Valor.Centavos);
                insCmd.Parameters.AddWithValue("$valorMoeda", parcela.Valor.Moeda);
                insCmd.Parameters.AddWithValue("$valorPagoCentavos", parcela.ValorPago.Centavos);
                insCmd.Parameters.AddWithValue("$status", (int)parcela.Status);
                insCmd.Parameters.AddWithValue("$dataLiquidacao", (object?)Iso(parcela.DataLiquidacao) ?? DBNull.Value);
                insCmd.Parameters.AddWithValue("$formaPagamentoId", (object?)parcela.FormaPagamentoId ?? DBNull.Value);

                await insCmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }
        }, ct);

    private static async Task<List<ContaAReceber>> LerContasAsync(
        SqliteConnection connection, SqliteTransaction? transaction, string whereClause,
        Action<SqliteCommand> configurarParametros, CancellationToken ct)
    {
        var headers = new List<HeaderRow>();
        await using (var cmd = connection.CreateCommand())
        {
            cmd.Transaction = transaction;
            cmd.CommandText = $"SELECT {ColunasHeader} FROM contas_a_receber {whereClause};";
            configurarParametros(cmd);

            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                headers.Add(new HeaderRow(
                    reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
                    reader.GetString(4), reader.GetString(5), reader.IsDBNull(6) ? null : reader.GetString(6),
                    reader.GetString(7), new Money(reader.GetInt64(8), reader.GetString(9)),
                    (StatusFinanceiro)reader.GetInt32(10), reader.GetString(11),
                    reader.IsDBNull(12) ? null : reader.GetString(12),
                    reader.IsDBNull(13) ? null : (CorrenteDeReceita)reader.GetInt32(13),
                    reader.IsDBNull(14) ? null : reader.GetString(14),
                    reader.IsDBNull(15) ? null : reader.GetInt64(15),
                    reader.IsDBNull(16) ? null : reader.GetInt64(16),
                    reader.IsDBNull(17) ? null : reader.GetInt32(17),
                    reader.IsDBNull(18) ? null : reader.GetString(18)));
            }
        }

        var resultado = new List<ContaAReceber>(headers.Count);
        foreach (var h in headers)
        {
            var parcelas = await LerParcelasAsync(connection, transaction, h.Id, ct).ConfigureAwait(false);
            resultado.Add(ContaAReceber.Reconstituir(
                h.Id, h.BusinessId, new SourceRef(h.SourceRefModulo, h.SourceRefId), h.Descricao, h.CategoriaId,
                h.CentroDeCustoId, ParseData(h.DataCompetencia)!.Value, h.ValorTotal, h.Status,
                ParseData(h.CriadoEm)!.Value, parcelas, h.ClienteId, h.Corrente, h.TecnicoId,
                h.ValorServicoCentavos is { } vsc ? new Money(vsc, h.ValorTotal.Moeda) : null,
                h.ValorPecasCentavos is { } vpc ? new Money(vpc, h.ValorTotal.Moeda) : null,
                h.MesesDeReconhecimento, h.ProjetoId));
        }
        return resultado;
    }

    private static async Task<List<Parcela>> LerParcelasAsync(SqliteConnection connection, SqliteTransaction? transaction, string contaId, CancellationToken ct)
    {
        await using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText =
            """
            SELECT id, numero, vencimento, valor_centavos, valor_moeda, valor_pago_centavos, status, data_liquidacao, forma_pagamento_id
            FROM parcelas_a_receber
            WHERE conta_id = $contaId
            ORDER BY numero;
            """;
        cmd.Parameters.AddWithValue("$contaId", contaId);

        var parcelas = new List<Parcela>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            parcelas.Add(Parcela.Reconstituir(
                id: reader.GetString(0),
                numero: reader.GetInt32(1),
                vencimento: ParseData(reader.GetString(2))!.Value,
                valor: new Money(reader.GetInt64(3), reader.GetString(4)),
                valorPago: new Money(reader.GetInt64(5), reader.GetString(4)),
                status: (StatusFinanceiro)reader.GetInt32(6),
                dataLiquidacao: reader.IsDBNull(7) ? null : ParseData(reader.GetString(7)),
                formaPagamentoId: reader.IsDBNull(8) ? null : reader.GetString(8)));
        }
        return parcelas;
    }

    private static string Iso(DateTimeOffset d) => d.ToString("O", CultureInfo.InvariantCulture);
    private static string? Iso(DateTimeOffset? d) => d?.ToString("O", CultureInfo.InvariantCulture);
    private static DateTimeOffset? ParseData(string? s) => s is null ? null : DateTimeOffset.Parse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    private sealed record HeaderRow(
        string Id, string BusinessId, string SourceRefModulo, string SourceRefId, string Descricao,
        string CategoriaId, string? CentroDeCustoId, string DataCompetencia, Money ValorTotal,
        StatusFinanceiro Status, string CriadoEm, string? ClienteId, CorrenteDeReceita? Corrente,
        string? TecnicoId, long? ValorServicoCentavos, long? ValorPecasCentavos, int? MesesDeReconhecimento,
        string? ProjetoId);

    /// <summary>Escreve dentro da sessão ambiente, se houver uma ativa; senão abre conexão própria
    /// e curta. Copiado literalmente de <c>SqliteFornecedorRepository</c> — molde da F0.</summary>
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
