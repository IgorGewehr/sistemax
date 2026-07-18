using System.Globalization;
using Microsoft.Data.Sqlite;
using SistemaX.Infrastructure.Local;
using SistemaX.Infrastructure.Local.UnitOfWork;
using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Domain.Ativos;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Infrastructure.Sqlite;

/// <summary>
/// Persistência REAL (SQLite) de <see cref="AtivoDeCapital"/> — mesmo molde de
/// <see cref="SqliteProjetoRepository"/>. Schema nasce de <see cref="FinanceiroSchemaMigrationV35"/>.
/// </summary>
public sealed class SqliteAtivoDeCapitalRepository(ILocalSqliteConnectionFactory connectionFactory, ILocalSessao sessao)
    : IAtivoDeCapitalRepository
{
    private const string Colunas =
        "id, business_id, projeto_id, nome, natureza, categoria, custo_aquisicao_centavos, valor_residual_centavos, " +
        "data_aquisicao, inicio_depreciacao, vida_util_meses, metodo, quantidade_unidades, conta_a_pagar_id, status, " +
        "ultima_competencia_reconhecida, encerrado_em, baixado_em, motivo_baixa, valor_reconhecido_na_baixa_centavos, " +
        "valor_venda_centavos, criado_em";

    public Task<AtivoDeCapital?> ObterPorIdAsync(string businessId, string ativoId, CancellationToken ct = default)
        => ConsultarAsync(async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = $"SELECT {Colunas} FROM ativos_de_capital WHERE business_id = $biz AND id = $id;";
            cmd.Parameters.AddWithValue("$biz", businessId);
            cmd.Parameters.AddWithValue("$id", ativoId);

            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            return await reader.ReadAsync(ct).ConfigureAwait(false) ? Ler(reader) : null;
        }, ct);

    public Task<IReadOnlyList<AtivoDeCapital>> ListarAsync(string businessId, string? projetoId = null, CancellationToken ct = default)
        => ConsultarAsync(async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = projetoId is null
                ? $"SELECT {Colunas} FROM ativos_de_capital WHERE business_id = $biz ORDER BY criado_em;"
                : $"SELECT {Colunas} FROM ativos_de_capital WHERE business_id = $biz AND projeto_id = $projeto ORDER BY criado_em;";
            cmd.Parameters.AddWithValue("$biz", businessId);
            if (projetoId is not null) cmd.Parameters.AddWithValue("$projeto", projetoId);

            var resultado = new List<AtivoDeCapital>();
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false)) resultado.Add(Ler(reader));
            return (IReadOnlyList<AtivoDeCapital>)resultado;
        }, ct);

    public Task<IReadOnlyList<AtivoDeCapital>> ListarEmUsoAsync(string businessId, CancellationToken ct = default)
        => ConsultarAsync(async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = $"SELECT {Colunas} FROM ativos_de_capital WHERE business_id = $biz AND status = $status ORDER BY criado_em;";
            cmd.Parameters.AddWithValue("$biz", businessId);
            cmd.Parameters.AddWithValue("$status", (int)StatusAtivoDeCapital.EmUso);

            var resultado = new List<AtivoDeCapital>();
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false)) resultado.Add(Ler(reader));
            return (IReadOnlyList<AtivoDeCapital>)resultado;
        }, ct);

    public Task SalvarAsync(AtivoDeCapital ativo, CancellationToken ct = default)
        => ExecutarAsync(async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText =
                """
                INSERT INTO ativos_de_capital (
                    id, business_id, projeto_id, nome, natureza, categoria, custo_aquisicao_centavos, valor_residual_centavos,
                    data_aquisicao, inicio_depreciacao, vida_util_meses, metodo, quantidade_unidades, conta_a_pagar_id, status,
                    ultima_competencia_reconhecida, encerrado_em, baixado_em, motivo_baixa, valor_reconhecido_na_baixa_centavos,
                    valor_venda_centavos, criado_em)
                VALUES (
                    $id, $biz, $projeto, $nome, $natureza, $categoria, $custo, $residual,
                    $dataAquisicao, $inicioDep, $vidaUtil, $metodo, $qtd, $contaAPagarId, $status,
                    $ultimaComp, $encerradoEm, $baixadoEm, $motivoBaixa, $valorReconhecidoNaBaixa, $valorVenda, $criadoEm)
                ON CONFLICT(id) DO UPDATE SET
                    projeto_id = excluded.projeto_id,
                    nome = excluded.nome,
                    conta_a_pagar_id = excluded.conta_a_pagar_id,
                    status = excluded.status,
                    ultima_competencia_reconhecida = excluded.ultima_competencia_reconhecida,
                    encerrado_em = excluded.encerrado_em,
                    baixado_em = excluded.baixado_em,
                    motivo_baixa = excluded.motivo_baixa,
                    valor_reconhecido_na_baixa_centavos = excluded.valor_reconhecido_na_baixa_centavos,
                    valor_venda_centavos = excluded.valor_venda_centavos;
                """;
            cmd.Parameters.AddWithValue("$id", ativo.Id);
            cmd.Parameters.AddWithValue("$biz", ativo.BusinessId);
            cmd.Parameters.AddWithValue("$projeto", (object?)ativo.ProjetoId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$nome", ativo.Nome);
            cmd.Parameters.AddWithValue("$natureza", (int)ativo.Natureza);
            cmd.Parameters.AddWithValue("$categoria", (int)ativo.Categoria);
            cmd.Parameters.AddWithValue("$custo", ativo.CustoAquisicao.Centavos);
            cmd.Parameters.AddWithValue("$residual", ativo.ValorResidual.Centavos);
            cmd.Parameters.AddWithValue("$dataAquisicao", IsoData(ativo.DataAquisicao));
            cmd.Parameters.AddWithValue("$inicioDep", IsoData(ativo.InicioDepreciacao));
            cmd.Parameters.AddWithValue("$vidaUtil", ativo.VidaUtilMeses);
            cmd.Parameters.AddWithValue("$metodo", (int)ativo.Metodo);
            cmd.Parameters.AddWithValue("$qtd", ativo.QuantidadeUnidades);
            cmd.Parameters.AddWithValue("$contaAPagarId", (object?)ativo.ContaAPagarId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$status", (int)ativo.Status);
            cmd.Parameters.AddWithValue("$ultimaComp", (object?)IsoInstante(ativo.UltimaCompetenciaReconhecida) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$encerradoEm", (object?)IsoInstante(ativo.EncerradoEm) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$baixadoEm", (object?)IsoInstante(ativo.BaixadoEm) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$motivoBaixa", (object?)ativo.MotivoBaixa ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$valorReconhecidoNaBaixa", (object?)ativo.ValorReconhecidoNaBaixaCentavos ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$valorVenda", (object?)ativo.ValorVenda?.Centavos ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$criadoEm", IsoInstante(ativo.CriadoEm)!);
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }, ct);

    private static AtivoDeCapital Ler(SqliteDataReader reader)
        => AtivoDeCapital.Reconstituir(
            id: reader.GetString(0),
            businessId: reader.GetString(1),
            projetoId: reader.IsDBNull(2) ? null : reader.GetString(2),
            nome: reader.GetString(3),
            natureza: (NaturezaAtivo)reader.GetInt32(4),
            categoria: (CategoriaAtivo)reader.GetInt32(5),
            custoAquisicao: new Money(reader.GetInt64(6)),
            valorResidual: new Money(reader.GetInt64(7)),
            dataAquisicao: ParseData(reader.GetString(8)),
            inicioDepreciacao: ParseData(reader.GetString(9)),
            vidaUtilMeses: reader.GetInt32(10),
            metodo: (MetodoDeCronograma)reader.GetInt32(11),
            quantidadeUnidades: reader.GetInt32(12),
            contaAPagarId: reader.IsDBNull(13) ? null : reader.GetString(13),
            status: (StatusAtivoDeCapital)reader.GetInt32(14),
            ultimaCompetenciaReconhecida: reader.IsDBNull(15) ? null : ParseInstante(reader.GetString(15)),
            encerradoEm: reader.IsDBNull(16) ? null : ParseInstante(reader.GetString(16)),
            baixadoEm: reader.IsDBNull(17) ? null : ParseInstante(reader.GetString(17)),
            motivoBaixa: reader.IsDBNull(18) ? null : reader.GetString(18),
            valorReconhecidoNaBaixaCentavos: reader.IsDBNull(19) ? null : reader.GetInt64(19),
            valorVenda: reader.IsDBNull(20) ? null : new Money(reader.GetInt64(20)),
            criadoEm: ParseInstante(reader.GetString(21))!.Value);

    private static string IsoData(DateOnly d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    private static DateOnly ParseData(string s) => DateOnly.ParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture);
    private static string? IsoInstante(DateTimeOffset? d) => d?.ToString("O", CultureInfo.InvariantCulture);
    private static DateTimeOffset? ParseInstante(string? s) => s is null ? null : DateTimeOffset.Parse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

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
