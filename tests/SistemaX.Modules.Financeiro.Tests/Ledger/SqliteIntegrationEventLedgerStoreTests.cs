using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using SistemaX.Infrastructure.Local;
using SistemaX.Infrastructure.Local.Ledger;
using SistemaX.Infrastructure.Local.Migrations;

namespace SistemaX.Modules.Financeiro.Tests.Ledger;

/// <summary>
/// Contract test do ledger append-only <c>integration_events</c> — a peça nº 1 da F0 do plano de
/// inteligência do Financeiro (docs/financeiro/inteligencia-arquitetura.md/ADR-0005). Só SQLite
/// (sem par em memória — mesmo racional de <c>SqliteOutboxStore</c>: é a fonte de verdade
/// histórica, não um detalhe de adapter).
/// </summary>
public sealed class SqliteIntegrationEventLedgerStoreTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"sistemax-ledger-contract-{Guid.NewGuid():N}.db");
    private readonly LocalSqliteConnectionFactory _connectionFactory;
    private readonly SqliteIntegrationEventLedgerStore _ledger;

    public SqliteIntegrationEventLedgerStoreTests()
    {
        _connectionFactory = new LocalSqliteConnectionFactory(Options.Create(new LocalDatabaseOptions { DatabasePath = _dbPath }));

        using var connection = _connectionFactory.OpenConnection();
        using var transaction = connection.BeginTransaction();
        new IntegrationEventsSchemaMigration().AplicarAsync(connection, transaction, CancellationToken.None).GetAwaiter().GetResult();
        transaction.Commit();

        _ledger = new SqliteIntegrationEventLedgerStore(_connectionFactory);
    }

    [Fact]
    public async Task AppendAsync_evento_novo_retorna_true_e_ganha_cursor_crescente()
    {
        var inseriu1 = await _ledger.AppendAsync("VendaConcluida", "tenant-1", "{}", DateTimeOffset.UtcNow, "venda.concluida:1");
        var inseriu2 = await _ledger.AppendAsync("VendaConcluida", "tenant-1", "{}", DateTimeOffset.UtcNow, "venda.concluida:2");

        Assert.True(inseriu1);
        Assert.True(inseriu2);

        var lote = await _ledger.LerAPartirDoCursorAsync(0, 10);
        Assert.Equal(2, lote.Count);
        Assert.True(lote[0].Cursor < lote[1].Cursor);
    }

    [Fact]
    public async Task AppendAsync_mesma_chave_idempotencia_nao_duplica_e_retorna_false()
    {
        var primeira = await _ledger.AppendAsync("VendaConcluida", "tenant-1", "{\"v\":1}", DateTimeOffset.UtcNow, "venda.concluida:dup");
        var segunda = await _ledger.AppendAsync("VendaConcluida", "tenant-1", "{\"v\":2}", DateTimeOffset.UtcNow, "venda.concluida:dup");

        Assert.True(primeira);
        Assert.False(segunda); // replay — idempotente, não duplica a linha

        var lote = await _ledger.LerAPartirDoCursorAsync(0, 10);
        Assert.Single(lote);
        Assert.Equal("{\"v\":1}", lote[0].PayloadJson); // a linha original nunca é sobrescrita
    }

    [Fact]
    public async Task LerAPartirDoCursor_retorna_apenas_estritamente_apos_o_cursor_informado()
    {
        await _ledger.AppendAsync("A", "tenant-1", "{}", DateTimeOffset.UtcNow, "a:1");
        await _ledger.AppendAsync("B", "tenant-1", "{}", DateTimeOffset.UtcNow, "b:1");
        await _ledger.AppendAsync("C", "tenant-1", "{}", DateTimeOffset.UtcNow, "c:1");

        var primeiroLote = await _ledger.LerAPartirDoCursorAsync(0, 10);
        Assert.Equal(3, primeiroLote.Count);

        var cursorDoPrimeiro = primeiroLote[0].Cursor;
        var restante = await _ledger.LerAPartirDoCursorAsync(cursorDoPrimeiro, 10);

        Assert.Equal(2, restante.Count);
        Assert.DoesNotContain(restante, e => e.Cursor == cursorDoPrimeiro);
    }

    [Fact]
    public async Task ObterUltimoCursor_vazio_retorna_zero_e_avanca_a_cada_append()
    {
        Assert.Equal(0, await _ledger.ObterUltimoCursorAsync());

        await _ledger.AppendAsync("A", "tenant-1", "{}", DateTimeOffset.UtcNow, "a:1");
        var depoisDoPrimeiro = await _ledger.ObterUltimoCursorAsync();
        Assert.True(depoisDoPrimeiro > 0);

        await _ledger.AppendAsync("B", "tenant-1", "{}", DateTimeOffset.UtcNow, "b:1");
        Assert.True(await _ledger.ObterUltimoCursorAsync() > depoisDoPrimeiro);
    }

    [Fact]
    public async Task LerAPartirDoCursor_respeita_maxBatchSize()
    {
        for (var i = 0; i < 5; i++)
            await _ledger.AppendAsync("A", "tenant-1", "{}", DateTimeOffset.UtcNow, $"a:{i}");

        var lote = await _ledger.LerAPartirDoCursorAsync(0, 2);
        Assert.Equal(2, lote.Count);
    }

    public void Dispose()
    {
        using (var poolConnection = _connectionFactory.OpenConnection())
        {
            SqliteConnection.ClearPool(poolConnection);
        }
        File.Delete(_dbPath);
        File.Delete($"{_dbPath}-wal");
        File.Delete($"{_dbPath}-shm");
    }
}
