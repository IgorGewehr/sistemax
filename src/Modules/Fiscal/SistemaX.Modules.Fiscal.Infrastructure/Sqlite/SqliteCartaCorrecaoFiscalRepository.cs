using SistemaX.Infrastructure.Local;
using SistemaX.Infrastructure.Local.UnitOfWork;
using SistemaX.Modules.Fiscal.Application.Ports;
using SistemaX.Modules.Fiscal.Domain.Documentos;

namespace SistemaX.Modules.Fiscal.Infrastructure.Sqlite;

/// <summary>Persistência REAL (SQLite) do log de CC-e (tabela <c>fiscal_cartas_correcao</c>,
/// migração V3) — mesmo molde de <c>SqliteFornecedorRepository</c> (Compras): schema vem SEMPRE de
/// uma migração dedicada, nunca de DDL no construtor.</summary>
public sealed class SqliteCartaCorrecaoFiscalRepository(ILocalSqliteConnectionFactory connectionFactory, ILocalSessao sessao)
    : ICartaCorrecaoFiscalRepository
{
    public Task<IReadOnlyList<CartaCorrecaoFiscal>> ListarPorDocumentoAsync(string documentoFiscalId, CancellationToken ct = default)
        => SqliteSessaoHelper.ConsultarAsync(connectionFactory, sessao, async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText =
                """
                SELECT id, tenant_id, documento_fiscal_id, chave_acesso, sequencia, texto, registrado_em
                FROM fiscal_cartas_correcao
                WHERE documento_fiscal_id = $documentoId
                ORDER BY sequencia;
                """;
            cmd.Parameters.AddWithValue("$documentoId", documentoFiscalId);

            var resultado = new List<CartaCorrecaoFiscal>();
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                resultado.Add(new CartaCorrecaoFiscal(
                    Id: reader.GetString(0),
                    TenantId: reader.GetString(1),
                    DocumentoFiscalId: reader.GetString(2),
                    ChaveDeAcesso: reader.GetString(3),
                    Sequencia: reader.GetInt32(4),
                    Texto: reader.GetString(5),
                    RegistradoEm: DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(6))));
            }

            return (IReadOnlyList<CartaCorrecaoFiscal>)resultado;
        }, ct);

    public Task SalvarAsync(CartaCorrecaoFiscal carta, CancellationToken ct = default)
        => SqliteSessaoHelper.ExecutarAsync(connectionFactory, sessao, async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText =
                """
                INSERT INTO fiscal_cartas_correcao (id, tenant_id, documento_fiscal_id, chave_acesso, sequencia, texto, registrado_em)
                VALUES ($id, $tenantId, $documentoId, $chaveAcesso, $sequencia, $texto, $registradoEm);
                """;
            cmd.Parameters.AddWithValue("$id", carta.Id);
            cmd.Parameters.AddWithValue("$tenantId", carta.TenantId);
            cmd.Parameters.AddWithValue("$documentoId", carta.DocumentoFiscalId);
            cmd.Parameters.AddWithValue("$chaveAcesso", carta.ChaveDeAcesso);
            cmd.Parameters.AddWithValue("$sequencia", carta.Sequencia);
            cmd.Parameters.AddWithValue("$texto", carta.Texto);
            cmd.Parameters.AddWithValue("$registradoEm", carta.RegistradoEm.ToUnixTimeMilliseconds());
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }, ct);
}
