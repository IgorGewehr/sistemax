using System.Collections.Concurrent;
using SistemaX.Modules.Fiscal.Application.Ports;
using SistemaX.Modules.Fiscal.Domain.Operacoes;
using SistemaX.Modules.Fiscal.Domain.Regras;
using SistemaX.Modules.Fiscal.Domain.Regimes;

namespace SistemaX.Modules.Fiscal.Infrastructure.InMemory;

/// <summary>Adapter direto in-memory — usado por padrão em teste (sem contexto SQLite). Chave
/// sintética IDÊNTICA à de <c>SqliteRegraFiscalPorOperacaoRepository.ChaveSintetica</c> para que
/// <see cref="SalvarAsync"/> seja upsert idempotente nos dois adapters igualmente (contract test
/// roda 2× esperando o MESMO comportamento).</summary>
public sealed class InMemoryRegraFiscalPorOperacaoRepository : IRegraFiscalPorOperacaoRepository
{
    private readonly ConcurrentDictionary<string, RegraFiscalPorOperacao> _regras = new();

    public Task<RegraFiscalPorOperacao?> ResolverAsync(
        string tenantId, RegimeTributario regime, TipoOperacaoFiscal tipoOperacao,
        string ufOrigem, string ufDestino, bool indicadorSt, CancellationToken ct = default)
    {
        var melhor = _regras.Values
            .Where(r => (r.TenantId is null || r.TenantId == tenantId)
                     && r.Regime == regime
                     && r.TipoOperacao == tipoOperacao
                     && string.Equals(r.UfOrigem, ufOrigem, StringComparison.OrdinalIgnoreCase)
                     && (r.UfDestino is null || string.Equals(r.UfDestino, ufDestino, StringComparison.OrdinalIgnoreCase))
                     && r.IndicadorSt == indicadorSt)
            .OrderByDescending(r => r.Especificidade)
            .FirstOrDefault();

        return Task.FromResult(melhor);
    }

    public Task SalvarAsync(RegraFiscalPorOperacao regra, CancellationToken ct = default)
    {
        _regras[ChaveSintetica(regra)] = regra;
        return Task.CompletedTask;
    }

    /// <summary>Mesma semântica do branch SQL de
    /// <c>SqliteRegraFiscalPorOperacaoRepository.ListarAsync</c>: <paramref name="tenantId"/>
    /// <c>null</c> → só as linhas DEFAULT (<c>TenantId</c> nulo); não-nulo → defaults + as linhas
    /// específicas daquele tenant.</summary>
    public Task<IReadOnlyList<RegraFiscalPorOperacao>> ListarAsync(string? tenantId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<RegraFiscalPorOperacao>>(
            _regras.Values.Where(r => r.TenantId is null || (tenantId is not null && r.TenantId == tenantId)).ToList());

    private static string ChaveSintetica(RegraFiscalPorOperacao r) =>
        $"{r.TenantId ?? "*"}:{r.Regime}:{r.TipoOperacao}:{r.UfOrigem}:{r.UfDestino ?? "*"}:{r.IndicadorSt}";
}
