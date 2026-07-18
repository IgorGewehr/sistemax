using Microsoft.Data.Sqlite;
using SistemaX.Infrastructure.Local;
using SistemaX.Infrastructure.Local.UnitOfWork;
using SistemaX.Modules.Fiscal.Application.Ports;
using SistemaX.Modules.Fiscal.Domain.Comum;
using SistemaX.Modules.Fiscal.Domain.Documentos;
using SistemaX.Modules.Fiscal.Domain.Ncm;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Fiscal.Infrastructure.Sqlite;

/// <summary>
/// Persistência do agregado central. <c>Itens</c>/<c>Tributos</c> são delete-then-reinsert a cada
/// <see cref="SalvarAsync"/> (mesmo padrão de <c>SqliteProdutoRepository</c> para coleções-filho
/// sem chave própria) — seguro porque <c>DocumentoFiscal.Autorizado</c> é imutável por invariante
/// de domínio (nenhum código chama <c>AdicionarItemResolvido</c> depois de autorizado), então o
/// reinsert nunca acontece num documento que já saiu do estado editável.
/// </summary>
public sealed class SqliteDocumentoFiscalRepository(ILocalSqliteConnectionFactory connectionFactory, ILocalSessao sessao)
    : IDocumentoFiscalRepository
{
    private const string ColunasDocumento =
        "id, tenant_id, tipo, origem_modulo, origem_id, status, serie, numero, chave_acesso, protocolo, motivo, criado_em";

    private const string ColunasItem =
        "id, ordem, produto_id, descricao, ncm, cest, origem_mercadoria, cfop, quantidade_milesimos, preco_unitario_centavos, preco_unitario_moeda, desconto_centavos, desconto_moeda";

    private const string ColunasTributo =
        "tipo_tributo, situacao_tributaria, base_centavos, base_moeda, aliquota_milionesimos, valor_centavos, valor_moeda, reducao_base_milionesimos, mva_milionesimos";

    public Task<DocumentoFiscal?> ObterPorIdAsync(string id, CancellationToken ct = default)
        => ConsultarAsync(async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = $"SELECT {ColunasDocumento} FROM fiscal_documentos WHERE id = $id;";
            cmd.Parameters.AddWithValue("$id", id);

            var cabecalho = await LerCabecalhoUnicoAsync(cmd, ct).ConfigureAwait(false);
            return cabecalho is null ? null : await MontarAsync(connection, transaction, cabecalho, ct).ConfigureAwait(false);
        }, ct);

    public Task<DocumentoFiscal?> ObterPorOrigemAsync(string tenantId, string origemChave, CancellationToken ct = default)
        => ConsultarAsync(async (connection, transaction) =>
        {
            var partes = origemChave.Split(':', 2);
            if (partes.Length != 2) return null;

            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = $"SELECT {ColunasDocumento} FROM fiscal_documentos WHERE tenant_id = $tenantId AND origem_modulo = $modulo AND origem_id = $origemId;";
            cmd.Parameters.AddWithValue("$tenantId", tenantId);
            cmd.Parameters.AddWithValue("$modulo", partes[0]);
            cmd.Parameters.AddWithValue("$origemId", partes[1]);

            var cabecalho = await LerCabecalhoUnicoAsync(cmd, ct).ConfigureAwait(false);
            return cabecalho is null ? null : await MontarAsync(connection, transaction, cabecalho, ct).ConfigureAwait(false);
        }, ct);

    public Task<IReadOnlyList<DocumentoFiscal>> ListarNumeroAlocadoAntesDeAsync(string tenantId, DateTimeOffset antesDe, CancellationToken ct = default)
        => ConsultarAsync(async (connection, transaction) =>
        {
            var cabecalhos = new List<DocumentoCabecalho>();
            await using (var cmd = connection.CreateCommand())
            {
                cmd.Transaction = transaction;
                cmd.CommandText =
                    $"""
                    SELECT {ColunasDocumento} FROM fiscal_documentos
                    WHERE tenant_id = $tenantId AND status = $status AND criado_em < $antesDe;
                    """;
                cmd.Parameters.AddWithValue("$tenantId", tenantId);
                cmd.Parameters.AddWithValue("$status", (int)StatusDocumentoFiscal.NumeroAlocado);
                cmd.Parameters.AddWithValue("$antesDe", antesDe.ToUnixTimeMilliseconds());

                await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
                while (await reader.ReadAsync(ct).ConfigureAwait(false))
                    cabecalhos.Add(LerCabecalho(reader));
            }

            var documentos = new List<DocumentoFiscal>(cabecalhos.Count);
            foreach (var cabecalho in cabecalhos)
                documentos.Add(await MontarAsync(connection, transaction, cabecalho, ct).ConfigureAwait(false));

            return (IReadOnlyList<DocumentoFiscal>)documentos;
        }, ct);

    public Task<IReadOnlyList<DocumentoFiscal>> ListarAsync(string tenantId, StatusDocumentoFiscal? status = null, CancellationToken ct = default)
        => ConsultarAsync(async (connection, transaction) =>
        {
            var cabecalhos = new List<DocumentoCabecalho>();
            await using (var cmd = connection.CreateCommand())
            {
                cmd.Transaction = transaction;
                cmd.CommandText = status is null
                    ? $"SELECT {ColunasDocumento} FROM fiscal_documentos WHERE tenant_id = $tenantId ORDER BY criado_em DESC;"
                    : $"SELECT {ColunasDocumento} FROM fiscal_documentos WHERE tenant_id = $tenantId AND status = $status ORDER BY criado_em DESC;";
                cmd.Parameters.AddWithValue("$tenantId", tenantId);
                if (status is not null) cmd.Parameters.AddWithValue("$status", (int)status.Value);

                await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
                while (await reader.ReadAsync(ct).ConfigureAwait(false))
                    cabecalhos.Add(LerCabecalho(reader));
            }

            var documentos = new List<DocumentoFiscal>(cabecalhos.Count);
            foreach (var cabecalho in cabecalhos)
                documentos.Add(await MontarAsync(connection, transaction, cabecalho, ct).ConfigureAwait(false));

            return (IReadOnlyList<DocumentoFiscal>)documentos;
        }, ct);

    public Task SalvarAsync(DocumentoFiscal documento, CancellationToken ct = default)
        => ExecutarAsync(async (connection, transaction) =>
        {
            await using (var cmd = connection.CreateCommand())
            {
                cmd.Transaction = transaction;
                cmd.CommandText =
                    """
                    INSERT INTO fiscal_documentos (id, tenant_id, tipo, origem_modulo, origem_id, status, serie, numero, chave_acesso, protocolo, motivo, criado_em)
                    VALUES ($id, $tenantId, $tipo, $origemModulo, $origemId, $status, $serie, $numero, $chaveAcesso, $protocolo, $motivo, $criadoEm)
                    ON CONFLICT(id) DO UPDATE SET
                        status = excluded.status, serie = excluded.serie, numero = excluded.numero,
                        chave_acesso = excluded.chave_acesso, protocolo = excluded.protocolo, motivo = excluded.motivo;
                    """;
                cmd.Parameters.AddWithValue("$id", documento.Id);
                cmd.Parameters.AddWithValue("$tenantId", documento.TenantId);
                cmd.Parameters.AddWithValue("$tipo", (int)documento.Tipo);
                cmd.Parameters.AddWithValue("$origemModulo", documento.Origem.Modulo);
                cmd.Parameters.AddWithValue("$origemId", documento.Origem.Id);
                cmd.Parameters.AddWithValue("$status", (int)documento.Status);
                cmd.Parameters.AddWithValue("$serie", (object?)documento.Serie ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$numero", (object?)documento.Numero ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$chaveAcesso", (object?)documento.ChaveDeAcesso ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$protocolo", (object?)documento.Protocolo ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$motivo", (object?)documento.MotivoBloqueioOuRejeicaoOuDenegacao ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$criadoEm", documento.CriadoEm.ToUnixTimeMilliseconds());
                await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }

            await using (var cmd = connection.CreateCommand())
            {
                cmd.Transaction = transaction;
                cmd.CommandText = "DELETE FROM fiscal_itens_documento WHERE documento_fiscal_id = $documentoId;";
                cmd.Parameters.AddWithValue("$documentoId", documento.Id);
                await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }

            var ordem = 0;
            foreach (var item in documento.Itens)
            {
                var itemId = $"{documento.Id}:{ordem}";

                await using (var cmd = connection.CreateCommand())
                {
                    cmd.Transaction = transaction;
                    cmd.CommandText =
                        """
                        INSERT INTO fiscal_itens_documento
                            (id, documento_fiscal_id, ordem, produto_id, descricao, ncm, cest, origem_mercadoria, cfop,
                             quantidade_milesimos, preco_unitario_centavos, preco_unitario_moeda, desconto_centavos, desconto_moeda)
                        VALUES
                            ($id, $documentoId, $ordem, $produtoId, $descricao, $ncm, $cest, $origem, $cfop,
                             $quantidade, $precoCentavos, $precoMoeda, $descontoCentavos, $descontoMoeda);
                        """;
                    cmd.Parameters.AddWithValue("$id", itemId);
                    cmd.Parameters.AddWithValue("$documentoId", documento.Id);
                    cmd.Parameters.AddWithValue("$ordem", ordem);
                    cmd.Parameters.AddWithValue("$produtoId", item.ProdutoId);
                    cmd.Parameters.AddWithValue("$descricao", item.Descricao);
                    cmd.Parameters.AddWithValue("$ncm", item.Ncm);
                    cmd.Parameters.AddWithValue("$cest", (object?)item.Cest ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("$origem", (int)item.Origem);
                    cmd.Parameters.AddWithValue("$cfop", item.Cfop);
                    cmd.Parameters.AddWithValue("$quantidade", item.Quantidade.Milesimos);
                    cmd.Parameters.AddWithValue("$precoCentavos", item.PrecoUnitario.Centavos);
                    cmd.Parameters.AddWithValue("$precoMoeda", item.PrecoUnitario.Moeda);
                    cmd.Parameters.AddWithValue("$descontoCentavos", item.Desconto.Centavos);
                    cmd.Parameters.AddWithValue("$descontoMoeda", item.Desconto.Moeda);
                    await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                }

                foreach (var tributo in item.Tributos)
                {
                    await using var cmd = connection.CreateCommand();
                    cmd.Transaction = transaction;
                    cmd.CommandText =
                        """
                        INSERT INTO fiscal_tributos_item
                            (item_id, tipo_tributo, situacao_tributaria, base_centavos, base_moeda,
                             aliquota_milionesimos, valor_centavos, valor_moeda, reducao_base_milionesimos, mva_milionesimos)
                        VALUES
                            ($itemId, $tipo, $situacao, $baseCentavos, $baseMoeda,
                             $aliquota, $valorCentavos, $valorMoeda, $reducaoBase, $mva);
                        """;
                    cmd.Parameters.AddWithValue("$itemId", itemId);
                    cmd.Parameters.AddWithValue("$tipo", (int)tributo.Tipo);
                    cmd.Parameters.AddWithValue("$situacao", (object?)tributo.SituacaoTributaria ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("$baseCentavos", tributo.Base.Centavos);
                    cmd.Parameters.AddWithValue("$baseMoeda", tributo.Base.Moeda);
                    cmd.Parameters.AddWithValue("$aliquota", tributo.Aliquota.Milionesimos);
                    cmd.Parameters.AddWithValue("$valorCentavos", tributo.Valor.Centavos);
                    cmd.Parameters.AddWithValue("$valorMoeda", tributo.Valor.Moeda);
                    cmd.Parameters.AddWithValue("$reducaoBase", (object?)tributo.ReducaoBaseCalculo?.Milionesimos ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("$mva", (object?)tributo.Mva?.Milionesimos ?? DBNull.Value);
                    await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                }

                ordem++;
            }
        }, ct);

    private static async Task<DocumentoCabecalho?> LerCabecalhoUnicoAsync(SqliteCommand cmd, CancellationToken ct)
    {
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return !await reader.ReadAsync(ct).ConfigureAwait(false) ? null : LerCabecalho(reader);
    }

    private static DocumentoCabecalho LerCabecalho(SqliteDataReader reader) => new(
        Id: reader.GetString(0),
        TenantId: reader.GetString(1),
        Tipo: (TipoDocumentoFiscal)reader.GetInt32(2),
        Origem: new SourceRef(reader.GetString(3), reader.GetString(4)),
        Status: (StatusDocumentoFiscal)reader.GetInt32(5),
        Serie: reader.IsDBNull(6) ? null : reader.GetString(6),
        Numero: reader.IsDBNull(7) ? null : reader.GetInt64(7),
        ChaveDeAcesso: reader.IsDBNull(8) ? null : reader.GetString(8),
        Protocolo: reader.IsDBNull(9) ? null : reader.GetString(9),
        Motivo: reader.IsDBNull(10) ? null : reader.GetString(10),
        CriadoEm: DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(11)));

    private static async Task<DocumentoFiscal> MontarAsync(SqliteConnection connection, SqliteTransaction? transaction, DocumentoCabecalho cabecalho, CancellationToken ct)
    {
        var itensPorId = new List<(string ItemId, ItemDocumentoFiscal Item)>();

        await using (var cmd = connection.CreateCommand())
        {
            cmd.Transaction = transaction;
            cmd.CommandText = $"SELECT {ColunasItem} FROM fiscal_itens_documento WHERE documento_fiscal_id = $documentoId ORDER BY ordem;";
            cmd.Parameters.AddWithValue("$documentoId", cabecalho.Id);

            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                var itemId = reader.GetString(0);
                var item = new ItemDocumentoFiscal(
                    ProdutoId: reader.GetString(2),
                    Descricao: reader.GetString(3),
                    Ncm: reader.GetString(4),
                    Cest: reader.IsDBNull(5) ? null : reader.GetString(5),
                    Origem: (OrigemMercadoria)reader.GetInt32(6),
                    Cfop: reader.GetString(7),
                    Quantidade: new Quantidade(reader.GetInt64(8)),
                    PrecoUnitario: new Money(reader.GetInt64(9), reader.GetString(10)),
                    Desconto: new Money(reader.GetInt64(11), reader.GetString(12)),
                    Tributos: Array.Empty<TributoResolvidoItem>());
                itensPorId.Add((itemId, item));
            }
        }

        var itensFinal = new List<ItemDocumentoFiscal>(itensPorId.Count);
        foreach (var (itemId, item) in itensPorId)
        {
            var tributos = new List<TributoResolvidoItem>();
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = $"SELECT {ColunasTributo} FROM fiscal_tributos_item WHERE item_id = $itemId;";
            cmd.Parameters.AddWithValue("$itemId", itemId);

            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                tributos.Add(new TributoResolvidoItem(
                    Tipo: (TipoTributo)reader.GetInt32(0),
                    SituacaoTributaria: reader.IsDBNull(1) ? null : reader.GetString(1),
                    Base: new Money(reader.GetInt64(2), reader.GetString(3)),
                    Aliquota: new Percentual(reader.GetInt64(4)),
                    Valor: new Money(reader.GetInt64(5), reader.GetString(6)),
                    ReducaoBaseCalculo: reader.IsDBNull(7) ? null : new Percentual(reader.GetInt64(7)),
                    Mva: reader.IsDBNull(8) ? null : new Percentual(reader.GetInt64(8))));
            }

            itensFinal.Add(item with { Tributos = tributos });
        }

        return DocumentoFiscal.Reconstituir(
            cabecalho.Id, cabecalho.TenantId, cabecalho.Tipo, cabecalho.Origem, cabecalho.Status,
            cabecalho.Serie, cabecalho.Numero, cabecalho.ChaveDeAcesso, cabecalho.Protocolo, cabecalho.Motivo,
            cabecalho.CriadoEm, itensFinal);
    }

    private sealed record DocumentoCabecalho(
        string Id, string TenantId, TipoDocumentoFiscal Tipo, SourceRef Origem, StatusDocumentoFiscal Status,
        string? Serie, long? Numero, string? ChaveDeAcesso, string? Protocolo, string? Motivo, DateTimeOffset CriadoEm);

    private Task ExecutarAsync(Func<SqliteConnection, SqliteTransaction?, Task> acao, CancellationToken ct)
        => SqliteSessaoHelper.ExecutarAsync(connectionFactory, sessao, acao, ct);

    private Task<T> ConsultarAsync<T>(Func<SqliteConnection, SqliteTransaction?, Task<T>> consulta, CancellationToken ct)
        => SqliteSessaoHelper.ConsultarAsync(connectionFactory, sessao, consulta, ct);
}
