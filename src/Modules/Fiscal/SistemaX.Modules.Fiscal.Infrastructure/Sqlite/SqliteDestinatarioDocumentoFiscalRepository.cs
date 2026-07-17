using SistemaX.Infrastructure.Local;
using SistemaX.Infrastructure.Local.UnitOfWork;
using SistemaX.Modules.Fiscal.Application.Ports;

namespace SistemaX.Modules.Fiscal.Infrastructure.Sqlite;

/// <summary>Fecha o gap #1 (emissao-mapping.md §4.2/§11) para persistência SQLite. Sem FK para
/// <c>fiscal_documentos</c> de propósito: <see cref="VincularAsync"/> pode ser chamado pelo
/// módulo de origem (Vendas/PDV) antes ou depois do <c>DocumentoFiscal</c> em si estar
/// persistido — mesma flexibilidade do adapter InMemory.</summary>
public sealed class SqliteDestinatarioDocumentoFiscalRepository(ILocalSqliteConnectionFactory connectionFactory, ILocalSessao sessao)
    : IDestinatarioDocumentoFiscalRepository
{
    private const string Colunas =
        "cnpj, cpf, nome, email, inscricao_estadual, endereco_logradouro, endereco_numero, endereco_complemento, endereco_bairro, endereco_codigo_municipio, endereco_municipio, endereco_uf, endereco_cep";

    public Task<DestinatarioDocumentoFiscal?> ObterPorDocumentoAsync(string documentoFiscalId, CancellationToken ct = default)
        => SqliteSessaoHelper.ConsultarAsync(connectionFactory, sessao, async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = $"SELECT {Colunas} FROM fiscal_destinatarios_documento WHERE documento_fiscal_id = $documentoId;";
            cmd.Parameters.AddWithValue("$documentoId", documentoFiscalId);

            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            if (!await reader.ReadAsync(ct).ConfigureAwait(false)) return null;

            var temEndereco = !reader.IsDBNull(5);
            var endereco = temEndereco
                ? new EnderecoDestinatarioFiscal(
                    Logradouro: reader.GetString(5),
                    Numero: reader.GetString(6),
                    Complemento: reader.IsDBNull(7) ? null : reader.GetString(7),
                    Bairro: reader.GetString(8),
                    CodigoMunicipio: reader.GetString(9),
                    Municipio: reader.GetString(10),
                    Uf: reader.GetString(11),
                    Cep: reader.GetString(12))
                : null;

            return new DestinatarioDocumentoFiscal(
                Cnpj: reader.IsDBNull(0) ? null : reader.GetString(0),
                Cpf: reader.IsDBNull(1) ? null : reader.GetString(1),
                Nome: reader.GetString(2),
                Email: reader.IsDBNull(3) ? null : reader.GetString(3),
                InscricaoEstadual: reader.IsDBNull(4) ? null : reader.GetString(4),
                Endereco: endereco);
        }, ct);

    public Task VincularAsync(string documentoFiscalId, DestinatarioDocumentoFiscal destinatario, CancellationToken ct = default)
        => SqliteSessaoHelper.ExecutarAsync(connectionFactory, sessao, async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText =
                """
                INSERT INTO fiscal_destinatarios_documento
                    (documento_fiscal_id, cnpj, cpf, nome, email, inscricao_estadual,
                     endereco_logradouro, endereco_numero, endereco_complemento, endereco_bairro,
                     endereco_codigo_municipio, endereco_municipio, endereco_uf, endereco_cep)
                VALUES
                    ($documentoId, $cnpj, $cpf, $nome, $email, $inscricaoEstadual,
                     $enderecoLogradouro, $enderecoNumero, $enderecoComplemento, $enderecoBairro,
                     $enderecoCodigoMunicipio, $enderecoMunicipio, $enderecoUf, $enderecoCep)
                ON CONFLICT(documento_fiscal_id) DO UPDATE SET
                    cnpj = excluded.cnpj, cpf = excluded.cpf, nome = excluded.nome, email = excluded.email,
                    inscricao_estadual = excluded.inscricao_estadual,
                    endereco_logradouro = excluded.endereco_logradouro, endereco_numero = excluded.endereco_numero,
                    endereco_complemento = excluded.endereco_complemento, endereco_bairro = excluded.endereco_bairro,
                    endereco_codigo_municipio = excluded.endereco_codigo_municipio,
                    endereco_municipio = excluded.endereco_municipio, endereco_uf = excluded.endereco_uf,
                    endereco_cep = excluded.endereco_cep;
                """;
            cmd.Parameters.AddWithValue("$documentoId", documentoFiscalId);
            cmd.Parameters.AddWithValue("$cnpj", (object?)destinatario.Cnpj ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$cpf", (object?)destinatario.Cpf ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$nome", destinatario.Nome);
            cmd.Parameters.AddWithValue("$email", (object?)destinatario.Email ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$inscricaoEstadual", (object?)destinatario.InscricaoEstadual ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$enderecoLogradouro", (object?)destinatario.Endereco?.Logradouro ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$enderecoNumero", (object?)destinatario.Endereco?.Numero ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$enderecoComplemento", (object?)destinatario.Endereco?.Complemento ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$enderecoBairro", (object?)destinatario.Endereco?.Bairro ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$enderecoCodigoMunicipio", (object?)destinatario.Endereco?.CodigoMunicipio ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$enderecoMunicipio", (object?)destinatario.Endereco?.Municipio ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$enderecoUf", (object?)destinatario.Endereco?.Uf ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$enderecoCep", (object?)destinatario.Endereco?.Cep ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }, ct);
}
