using SistemaX.Infrastructure.Local;
using SistemaX.Infrastructure.Local.UnitOfWork;
using SistemaX.Modules.Fiscal.Application.Ports;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Fiscal.Infrastructure.Sqlite;

/// <summary>Fecha o gap #3 (emissao-mapping.md §4.5/§11) para persistência SQLite. Lista
/// delete-then-reinsert a cada <see cref="VincularAsync"/> (mesmo padrão dos itens de
/// <see cref="SqliteDocumentoFiscalRepository"/>) — nunca persistido no agregado fiscal, só canal
/// auxiliar entre quem sabe a forma de pagamento (Vendas/PDV) e o mapper de emissão.</summary>
public sealed class SqliteFormaPagamentoDocumentoFiscalRepository(ILocalSqliteConnectionFactory connectionFactory, ILocalSessao sessao)
    : IFormaPagamentoDocumentoFiscalRepository
{
    public Task<IReadOnlyList<FormaPagamentoParaEmitir>> ObterPorDocumentoAsync(string documentoFiscalId, CancellationToken ct = default)
        => SqliteSessaoHelper.ConsultarAsync(connectionFactory, sessao, async (connection, transaction) =>
        {
            var pagamentos = new List<FormaPagamentoParaEmitir>();

            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText =
                "SELECT metodo, valor_centavos, valor_moeda FROM fiscal_formas_pagamento_documento WHERE documento_fiscal_id = $documentoId ORDER BY ordem;";
            cmd.Parameters.AddWithValue("$documentoId", documentoFiscalId);

            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
                pagamentos.Add(new FormaPagamentoParaEmitir(reader.GetString(0), new Money(reader.GetInt64(1), reader.GetString(2))));

            return (IReadOnlyList<FormaPagamentoParaEmitir>)pagamentos;
        }, ct);

    public Task VincularAsync(string documentoFiscalId, IReadOnlyList<FormaPagamentoParaEmitir> pagamentos, CancellationToken ct = default)
        => SqliteSessaoHelper.ExecutarAsync(connectionFactory, sessao, async (connection, transaction) =>
        {
            await using (var cmd = connection.CreateCommand())
            {
                cmd.Transaction = transaction;
                cmd.CommandText = "DELETE FROM fiscal_formas_pagamento_documento WHERE documento_fiscal_id = $documentoId;";
                cmd.Parameters.AddWithValue("$documentoId", documentoFiscalId);
                await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }

            var ordem = 0;
            foreach (var pagamento in pagamentos)
            {
                await using var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText =
                    """
                    INSERT INTO fiscal_formas_pagamento_documento (documento_fiscal_id, ordem, metodo, valor_centavos, valor_moeda)
                    VALUES ($documentoId, $ordem, $metodo, $valorCentavos, $valorMoeda);
                    """;
                cmd.Parameters.AddWithValue("$documentoId", documentoFiscalId);
                cmd.Parameters.AddWithValue("$ordem", ordem);
                cmd.Parameters.AddWithValue("$metodo", pagamento.Metodo);
                cmd.Parameters.AddWithValue("$valorCentavos", pagamento.Valor.Centavos);
                cmd.Parameters.AddWithValue("$valorMoeda", pagamento.Valor.Moeda);
                await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                ordem++;
            }
        }, ct);
}
