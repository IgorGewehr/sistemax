using SistemaX.Infrastructure.Local;
using SistemaX.Infrastructure.Local.UnitOfWork;
using SistemaX.Modules.Fiscal.Application.Ports;

namespace SistemaX.Modules.Fiscal.Infrastructure.Sqlite;

/// <summary>Fecha o gap #2 (emissao-mapping.md §4.6/§11) para persistência SQLite. ATENÇÃO: isto
/// grava o .pfx/senha em texto puro na mesma base local — aceitável para o modo MOCK/dev que este
/// fechamento de gateway cobre; um cofre criptografado (Storage) é obrigatório antes de qualquer
/// transmissão de verdade fora de MOCK (mesma ressalva que <see cref="InMemory.InMemoryCertificadoDigitalRepository"/>
/// já documentava).</summary>
public sealed class SqliteCertificadoDigitalRepository(ILocalSqliteConnectionFactory connectionFactory, ILocalSessao sessao)
    : ICertificadoDigitalRepository
{
    public Task<CertificadoDigital?> ObterAsync(string tenantId, CancellationToken ct = default)
        => SqliteSessaoHelper.ConsultarAsync(connectionFactory, sessao, async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = "SELECT pfx_base64, senha FROM fiscal_certificados_digitais WHERE tenant_id = $tenantId;";
            cmd.Parameters.AddWithValue("$tenantId", tenantId);

            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            if (!await reader.ReadAsync(ct).ConfigureAwait(false)) return null;

            return new CertificadoDigital(reader.GetString(0), reader.GetString(1));
        }, ct);

    public Task SalvarAsync(string tenantId, CertificadoDigital certificado, CancellationToken ct = default)
        => SqliteSessaoHelper.ExecutarAsync(connectionFactory, sessao, async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText =
                """
                INSERT INTO fiscal_certificados_digitais (tenant_id, pfx_base64, senha)
                VALUES ($tenantId, $pfxBase64, $senha)
                ON CONFLICT(tenant_id) DO UPDATE SET pfx_base64 = excluded.pfx_base64, senha = excluded.senha;
                """;
            cmd.Parameters.AddWithValue("$tenantId", tenantId);
            cmd.Parameters.AddWithValue("$pfxBase64", certificado.PfxBase64);
            cmd.Parameters.AddWithValue("$senha", certificado.Senha);
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }, ct);
}
