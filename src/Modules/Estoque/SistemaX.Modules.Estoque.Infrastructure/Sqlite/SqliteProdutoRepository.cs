using Microsoft.Data.Sqlite;
using SistemaX.Infrastructure.Local;
using SistemaX.Infrastructure.Local.UnitOfWork;
using SistemaX.Modules.Estoque.Application.Ports;
using SistemaX.Modules.Estoque.Domain.Catalogo;
using SistemaX.Modules.Estoque.Domain.Comum;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Estoque.Infrastructure.Sqlite;

/// <summary>
/// Persistência REAL (SQLite) de <see cref="Produto"/> — segue o repositório-molde da F0
/// (<c>SqliteFornecedorRepository</c>, ver docs/persistencia/persistencia-sqlite.md). Os dois
/// filhos mutáveis (<see cref="CodigoDeBarras"/>, <see cref="ComponenteDeFicha"/>) são
/// delete-then-reinsert a cada <see cref="SalvarAsync"/>: <c>AdicionarCodigoDeBarras</c> pode ser
/// chamado qualquer número de vezes ao longo da vida do produto, então não há update incremental
/// que não duplique a lógica de dedupe que já vive no agregado. Reidratação usa
/// <see cref="Produto.Reconstituir"/> — nunca <see cref="Produto.Criar"/>, que validaria de novo e
/// dispararia regra de negócio (R6).
/// </summary>
public sealed class SqliteProdutoRepository(ILocalSqliteConnectionFactory connectionFactory, ILocalSessao sessao)
    : IProdutoRepository
{
    private const string ColunasCabecalho =
        """
        id, tenant_id, sku, nome, descricao, categoria, unidade, preco_venda_centavos, preco_venda_moeda,
        fiscal_ncm, fiscal_cest, estoque_minimo_milesimos, ponto_reposicao_milesimos, lote_economico_milesimos,
        lead_time_dias, localizacao, controla_estoque, controle_por_lote, valorizacao, ativo,
        fiscal_natureza_operacao, fiscal_cfop_override
        """;

    public Task<Produto?> ObterPorIdAsync(string id, CancellationToken ct = default)
        => ConsultarAsync(async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = $"SELECT {ColunasCabecalho} FROM produtos WHERE id = $id;";
            cmd.Parameters.AddWithValue("$id", id);

            var cabecalho = await LerCabecalhoUnicoAsync(cmd, ct).ConfigureAwait(false);
            return cabecalho is null ? null : await MontarProdutoAsync(connection, transaction, cabecalho, ct).ConfigureAwait(false);
        }, ct);

    public Task<Produto?> ObterPorSkuAsync(string tenantId, string sku, CancellationToken ct = default)
        => ConsultarAsync(async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = $"SELECT {ColunasCabecalho} FROM produtos WHERE tenant_id = $tenantId AND sku = $sku;";
            cmd.Parameters.AddWithValue("$tenantId", tenantId);
            cmd.Parameters.AddWithValue("$sku", sku);

            var cabecalho = await LerCabecalhoUnicoAsync(cmd, ct).ConfigureAwait(false);
            return cabecalho is null ? null : await MontarProdutoAsync(connection, transaction, cabecalho, ct).ConfigureAwait(false);
        }, ct);

    public Task<IReadOnlyList<Produto>> ListarAsync(string tenantId, CancellationToken ct = default)
        => ConsultarAsync(async (connection, transaction) =>
        {
            var cabecalhos = new List<ProdutoCabecalho>();
            await using (var cmd = connection.CreateCommand())
            {
                cmd.Transaction = transaction;
                cmd.CommandText = $"SELECT {ColunasCabecalho} FROM produtos WHERE tenant_id = $tenantId;";
                cmd.Parameters.AddWithValue("$tenantId", tenantId);

                await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
                while (await reader.ReadAsync(ct).ConfigureAwait(false))
                {
                    cabecalhos.Add(LerCabecalho(reader));
                }
            }

            var produtos = new List<Produto>(cabecalhos.Count);
            foreach (var cabecalho in cabecalhos)
            {
                produtos.Add(await MontarProdutoAsync(connection, transaction, cabecalho, ct).ConfigureAwait(false));
            }

            return (IReadOnlyList<Produto>)produtos;
        }, ct);

    public Task SalvarAsync(Produto produto, CancellationToken ct = default)
        => ExecutarAsync(async (connection, transaction) =>
        {
            await using (var cmd = connection.CreateCommand())
            {
                cmd.Transaction = transaction;
                cmd.CommandText =
                    """
                    INSERT INTO produtos
                        (id, tenant_id, sku, nome, descricao, categoria, unidade, preco_venda_centavos, preco_venda_moeda,
                         fiscal_ncm, fiscal_cest, estoque_minimo_milesimos, ponto_reposicao_milesimos, lote_economico_milesimos,
                         lead_time_dias, localizacao, controla_estoque, controle_por_lote, valorizacao, ativo,
                         fiscal_natureza_operacao, fiscal_cfop_override)
                    VALUES
                        ($id, $tenantId, $sku, $nome, $descricao, $categoria, $unidade, $precoVendaCentavos, $precoVendaMoeda,
                         $fiscalNcm, $fiscalCest, $estoqueMinimo, $pontoReposicao, $loteEconomico,
                         $leadTimeDias, $localizacao, $controlaEstoque, $controlePorLote, $valorizacao, $ativo,
                         $fiscalNaturezaOperacao, $fiscalCfopOverride)
                    ON CONFLICT(id) DO UPDATE SET
                        sku                       = excluded.sku,
                        nome                      = excluded.nome,
                        descricao                 = excluded.descricao,
                        categoria                 = excluded.categoria,
                        unidade                   = excluded.unidade,
                        preco_venda_centavos      = excluded.preco_venda_centavos,
                        preco_venda_moeda         = excluded.preco_venda_moeda,
                        fiscal_ncm                = excluded.fiscal_ncm,
                        fiscal_cest               = excluded.fiscal_cest,
                        estoque_minimo_milesimos  = excluded.estoque_minimo_milesimos,
                        ponto_reposicao_milesimos = excluded.ponto_reposicao_milesimos,
                        lote_economico_milesimos  = excluded.lote_economico_milesimos,
                        lead_time_dias            = excluded.lead_time_dias,
                        localizacao               = excluded.localizacao,
                        controla_estoque          = excluded.controla_estoque,
                        controle_por_lote         = excluded.controle_por_lote,
                        valorizacao               = excluded.valorizacao,
                        ativo                     = excluded.ativo,
                        fiscal_natureza_operacao  = excluded.fiscal_natureza_operacao,
                        fiscal_cfop_override      = excluded.fiscal_cfop_override;
                    """;
                cmd.Parameters.AddWithValue("$id", produto.Id);
                cmd.Parameters.AddWithValue("$tenantId", produto.TenantId);
                cmd.Parameters.AddWithValue("$sku", produto.Sku);
                cmd.Parameters.AddWithValue("$nome", produto.Nome);
                cmd.Parameters.AddWithValue("$descricao", (object?)produto.Descricao ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$categoria", (object?)produto.Categoria ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$unidade", (int)produto.Unidade);
                cmd.Parameters.AddWithValue("$precoVendaCentavos", produto.PrecoVenda.Centavos);
                cmd.Parameters.AddWithValue("$precoVendaMoeda", produto.PrecoVenda.Moeda);
                cmd.Parameters.AddWithValue("$fiscalNcm", (object?)produto.Fiscal.Ncm ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$fiscalCest", (object?)produto.Fiscal.Cest ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$fiscalNaturezaOperacao", produto.Fiscal.NaturezaOperacao.ParaCodigo());
                cmd.Parameters.AddWithValue("$fiscalCfopOverride", (object?)produto.Fiscal.CfopOverride ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$estoqueMinimo", produto.EstoqueMinimo.Milesimos);
                cmd.Parameters.AddWithValue("$pontoReposicao", (object?)produto.PontoDeReposicao?.Milesimos ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$loteEconomico", (object?)produto.LoteEconomico?.Milesimos ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$leadTimeDias", (object?)produto.LeadTimeDias ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$localizacao", (object?)produto.Localizacao ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$controlaEstoque", produto.ControlaEstoque);
                cmd.Parameters.AddWithValue("$controlePorLote", produto.ControlePorLote);
                cmd.Parameters.AddWithValue("$valorizacao", (int)produto.Valorizacao);
                cmd.Parameters.AddWithValue("$ativo", produto.Ativo);

                await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }

            await using (var cmd = connection.CreateCommand())
            {
                cmd.Transaction = transaction;
                cmd.CommandText = "DELETE FROM produto_codigos_de_barras WHERE produto_id = $produtoId;";
                cmd.Parameters.AddWithValue("$produtoId", produto.Id);
                await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }

            foreach (var codigo in produto.CodigosDeBarras)
            {
                await using var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText =
                    """
                    INSERT INTO produto_codigos_de_barras (id, produto_id, valor, tipo)
                    VALUES ($id, $produtoId, $valor, $tipo);
                    """;
                cmd.Parameters.AddWithValue("$id", $"{produto.Id}:{codigo.Valor}");
                cmd.Parameters.AddWithValue("$produtoId", produto.Id);
                cmd.Parameters.AddWithValue("$valor", codigo.Valor);
                cmd.Parameters.AddWithValue("$tipo", (int)codigo.Tipo);
                await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }

            await using (var cmd = connection.CreateCommand())
            {
                cmd.Transaction = transaction;
                cmd.CommandText = "DELETE FROM produto_ficha_tecnica WHERE produto_id = $produtoId;";
                cmd.Parameters.AddWithValue("$produtoId", produto.Id);
                await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }

            foreach (var componente in produto.FichaTecnica)
            {
                await using var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText =
                    """
                    INSERT INTO produto_ficha_tecnica (id, produto_id, produto_insumo_id, quantidade_milesimos)
                    VALUES ($id, $produtoId, $produtoInsumoId, $quantidade);
                    """;
                cmd.Parameters.AddWithValue("$id", $"{produto.Id}:{componente.ProdutoInsumoId}");
                cmd.Parameters.AddWithValue("$produtoId", produto.Id);
                cmd.Parameters.AddWithValue("$produtoInsumoId", componente.ProdutoInsumoId);
                cmd.Parameters.AddWithValue("$quantidade", componente.Quantidade.Milesimos);
                await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }
        }, ct);

    private static async Task<ProdutoCabecalho?> LerCabecalhoUnicoAsync(SqliteCommand cmd, CancellationToken ct)
    {
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            return null;
        }

        return LerCabecalho(reader);
    }

    private static ProdutoCabecalho LerCabecalho(SqliteDataReader reader)
        => new(
            Id: reader.GetString(0),
            TenantId: reader.GetString(1),
            Sku: reader.GetString(2),
            Nome: reader.GetString(3),
            Descricao: reader.IsDBNull(4) ? null : reader.GetString(4),
            Categoria: reader.IsDBNull(5) ? null : reader.GetString(5),
            Unidade: (UnidadeDeMedida)reader.GetInt32(6),
            PrecoVenda: new Money(reader.GetInt64(7), reader.GetString(8)),
            Fiscal: new DadosFiscaisProduto(
                reader.IsDBNull(9) ? null : reader.GetString(9),
                reader.IsDBNull(10) ? null : reader.GetString(10),
                NaturezaOperacaoProdutoExtensions.DeCodigo(reader.IsDBNull(20) ? null : reader.GetString(20)),
                reader.IsDBNull(21) ? null : reader.GetString(21)),
            EstoqueMinimo: new Quantidade(reader.GetInt64(11)),
            PontoDeReposicao: reader.IsDBNull(12) ? null : new Quantidade(reader.GetInt64(12)),
            LoteEconomico: reader.IsDBNull(13) ? null : new Quantidade(reader.GetInt64(13)),
            LeadTimeDias: reader.IsDBNull(14) ? null : reader.GetInt32(14),
            Localizacao: reader.IsDBNull(15) ? null : reader.GetString(15),
            ControlaEstoque: reader.GetBoolean(16),
            ControlePorLote: reader.GetBoolean(17),
            Valorizacao: (PoliticaDeValorizacao)reader.GetInt32(18),
            Ativo: reader.GetBoolean(19));

    private static async Task<Produto> MontarProdutoAsync(SqliteConnection connection, SqliteTransaction? transaction, ProdutoCabecalho cabecalho, CancellationToken ct)
    {
        var codigosDeBarras = new List<CodigoDeBarras>();
        await using (var cmd = connection.CreateCommand())
        {
            cmd.Transaction = transaction;
            cmd.CommandText = "SELECT valor, tipo FROM produto_codigos_de_barras WHERE produto_id = $produtoId;";
            cmd.Parameters.AddWithValue("$produtoId", cabecalho.Id);

            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                codigosDeBarras.Add(new CodigoDeBarras(reader.GetString(0), (TipoCodigoBarras)reader.GetInt32(1)));
            }
        }

        var fichaTecnica = new List<ComponenteDeFicha>();
        await using (var cmd = connection.CreateCommand())
        {
            cmd.Transaction = transaction;
            cmd.CommandText = "SELECT produto_insumo_id, quantidade_milesimos FROM produto_ficha_tecnica WHERE produto_id = $produtoId;";
            cmd.Parameters.AddWithValue("$produtoId", cabecalho.Id);

            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                fichaTecnica.Add(new ComponenteDeFicha(reader.GetString(0), new Quantidade(reader.GetInt64(1))));
            }
        }

        return Produto.Reconstituir(
            id: cabecalho.Id,
            tenantId: cabecalho.TenantId,
            sku: cabecalho.Sku,
            nome: cabecalho.Nome,
            descricao: cabecalho.Descricao,
            categoria: cabecalho.Categoria,
            unidade: cabecalho.Unidade,
            precoVenda: cabecalho.PrecoVenda,
            fiscal: cabecalho.Fiscal,
            estoqueMinimo: cabecalho.EstoqueMinimo,
            pontoDeReposicao: cabecalho.PontoDeReposicao,
            loteEconomico: cabecalho.LoteEconomico,
            leadTimeDias: cabecalho.LeadTimeDias,
            localizacao: cabecalho.Localizacao,
            controlaEstoque: cabecalho.ControlaEstoque,
            controlePorLote: cabecalho.ControlePorLote,
            valorizacao: cabecalho.Valorizacao,
            ativo: cabecalho.Ativo,
            codigosDeBarras: codigosDeBarras,
            fichaTecnica: fichaTecnica);
    }

    private sealed record ProdutoCabecalho(
        string Id, string TenantId, string Sku, string Nome, string? Descricao, string? Categoria,
        UnidadeDeMedida Unidade, Money PrecoVenda, DadosFiscaisProduto Fiscal, Quantidade EstoqueMinimo,
        Quantidade? PontoDeReposicao, Quantidade? LoteEconomico, int? LeadTimeDias, string? Localizacao,
        bool ControlaEstoque, bool ControlePorLote, PoliticaDeValorizacao Valorizacao, bool Ativo);

    /// <summary>Escreve dentro da sessão ambiente, se houver uma ativa; senão abre conexão própria
    /// e curta. Este método (e <see cref="ConsultarAsync{T}"/>) é o que TODO repositório SQLite
    /// novo deve reusar — nunca abrir conexão "na mão" espalhado pelos métodos do port.</summary>
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
