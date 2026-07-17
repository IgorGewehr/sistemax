using Microsoft.Data.Sqlite;
using SistemaX.Infrastructure.Local;
using SistemaX.Infrastructure.Local.UnitOfWork;
using SistemaX.Modules.Fiscal.Application.Ports;

namespace SistemaX.Modules.Fiscal.Infrastructure.Sqlite;

/// <summary>Fecha o gap #4 (emissao-mapping.md §3/§11) para persistência SQLite — mesmo dado
/// cadastral que <see cref="InMemory.InMemoryCadastroFiscalEmitenteRepository"/> guarda em
/// memória, seed manual via Settings até o módulo dono do cadastro (Empresa/Tenant) existir.</summary>
public sealed class SqliteCadastroFiscalEmitenteRepository(ILocalSqliteConnectionFactory connectionFactory, ILocalSessao sessao)
    : ICadastroFiscalEmitenteRepository
{
    private const string Colunas =
        "tenant_id, cnpj, razao_social, nome_fantasia, inscricao_estadual, inscricao_municipal, logradouro, numero, complemento, bairro, codigo_municipio, municipio, cep, telefone";

    public Task<CadastroFiscalEmitente?> ObterAsync(string tenantId, CancellationToken ct = default)
        => SqliteSessaoHelper.ConsultarAsync(connectionFactory, sessao, async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = $"SELECT {Colunas} FROM fiscal_cadastros_emitente WHERE tenant_id = $tenantId;";
            cmd.Parameters.AddWithValue("$tenantId", tenantId);

            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            if (!await reader.ReadAsync(ct).ConfigureAwait(false)) return null;

            return new CadastroFiscalEmitente(
                TenantId: reader.GetString(0),
                Cnpj: reader.GetString(1),
                RazaoSocial: reader.GetString(2),
                NomeFantasia: reader.IsDBNull(3) ? null : reader.GetString(3),
                InscricaoEstadual: reader.GetString(4),
                InscricaoMunicipal: reader.IsDBNull(5) ? null : reader.GetString(5),
                Logradouro: reader.GetString(6),
                Numero: reader.GetString(7),
                Complemento: reader.IsDBNull(8) ? null : reader.GetString(8),
                Bairro: reader.GetString(9),
                CodigoMunicipio: reader.GetString(10),
                Municipio: reader.GetString(11),
                Cep: reader.GetString(12),
                Telefone: reader.IsDBNull(13) ? null : reader.GetString(13));
        }, ct);

    public Task SalvarAsync(CadastroFiscalEmitente cadastro, CancellationToken ct = default)
        => SqliteSessaoHelper.ExecutarAsync(connectionFactory, sessao, async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText =
                """
                INSERT INTO fiscal_cadastros_emitente
                    (tenant_id, cnpj, razao_social, nome_fantasia, inscricao_estadual, inscricao_municipal,
                     logradouro, numero, complemento, bairro, codigo_municipio, municipio, cep, telefone)
                VALUES
                    ($tenantId, $cnpj, $razaoSocial, $nomeFantasia, $inscricaoEstadual, $inscricaoMunicipal,
                     $logradouro, $numero, $complemento, $bairro, $codigoMunicipio, $municipio, $cep, $telefone)
                ON CONFLICT(tenant_id) DO UPDATE SET
                    cnpj = excluded.cnpj, razao_social = excluded.razao_social, nome_fantasia = excluded.nome_fantasia,
                    inscricao_estadual = excluded.inscricao_estadual, inscricao_municipal = excluded.inscricao_municipal,
                    logradouro = excluded.logradouro, numero = excluded.numero, complemento = excluded.complemento,
                    bairro = excluded.bairro, codigo_municipio = excluded.codigo_municipio, municipio = excluded.municipio,
                    cep = excluded.cep, telefone = excluded.telefone;
                """;
            cmd.Parameters.AddWithValue("$tenantId", cadastro.TenantId);
            cmd.Parameters.AddWithValue("$cnpj", cadastro.Cnpj);
            cmd.Parameters.AddWithValue("$razaoSocial", cadastro.RazaoSocial);
            cmd.Parameters.AddWithValue("$nomeFantasia", (object?)cadastro.NomeFantasia ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$inscricaoEstadual", cadastro.InscricaoEstadual);
            cmd.Parameters.AddWithValue("$inscricaoMunicipal", (object?)cadastro.InscricaoMunicipal ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$logradouro", cadastro.Logradouro);
            cmd.Parameters.AddWithValue("$numero", cadastro.Numero);
            cmd.Parameters.AddWithValue("$complemento", (object?)cadastro.Complemento ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$bairro", cadastro.Bairro);
            cmd.Parameters.AddWithValue("$codigoMunicipio", cadastro.CodigoMunicipio);
            cmd.Parameters.AddWithValue("$municipio", cadastro.Municipio);
            cmd.Parameters.AddWithValue("$cep", cadastro.Cep);
            cmd.Parameters.AddWithValue("$telefone", (object?)cadastro.Telefone ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }, ct);
}
