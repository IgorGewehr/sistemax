using SistemaX.Infrastructure.Local;
using SistemaX.Infrastructure.Local.UnitOfWork;
using SistemaX.Modules.Fiscal.Application.Ports;

namespace SistemaX.Modules.Fiscal.Infrastructure.Sqlite;

/// <summary>Fecha o gap #5 (emissao-mapping.md §4.6/§11) para persistência SQLite — mesma
/// convenção de <see cref="SqliteDestinatarioDocumentoFiscalRepository"/>: sem FK para
/// <c>fiscal_documentos</c> de propósito, <see cref="VincularAsync"/> pode ser chamado antes ou
/// depois do <c>DocumentoFiscal</c> de devolução em si estar persistido.</summary>
public sealed class SqliteReferenciaDevolucaoDocumentoFiscalRepository(ILocalSqliteConnectionFactory connectionFactory, ILocalSessao sessao)
    : IReferenciaDevolucaoDocumentoFiscalRepository
{
    public Task<string?> ObterRefNFeAsync(string documentoFiscalId, CancellationToken ct = default)
        => SqliteSessaoHelper.ConsultarAsync(connectionFactory, sessao, async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = "SELECT ref_nfe FROM fiscal_referencias_devolucao_documento WHERE documento_fiscal_id = $documentoId;";
            cmd.Parameters.AddWithValue("$documentoId", documentoFiscalId);

            var resultado = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
            return resultado as string;
        }, ct);

    public Task VincularAsync(string documentoFiscalId, string refNFe, CancellationToken ct = default)
        => SqliteSessaoHelper.ExecutarAsync(connectionFactory, sessao, async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText =
                """
                INSERT INTO fiscal_referencias_devolucao_documento (documento_fiscal_id, ref_nfe)
                VALUES ($documentoId, $refNFe)
                ON CONFLICT(documento_fiscal_id) DO UPDATE SET ref_nfe = excluded.ref_nfe;
                """;
            cmd.Parameters.AddWithValue("$documentoId", documentoFiscalId);
            cmd.Parameters.AddWithValue("$refNFe", refNFe);
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }, ct);
}
