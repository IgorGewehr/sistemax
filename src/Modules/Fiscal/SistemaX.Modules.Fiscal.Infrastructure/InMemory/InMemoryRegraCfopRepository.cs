using System.Collections.Concurrent;
using SistemaX.Modules.Fiscal.Application.Ports;
using SistemaX.Modules.Fiscal.Domain.Operacoes;
using SistemaX.Modules.Fiscal.Domain.Produtos;
using SistemaX.Modules.Fiscal.Domain.Regras;

namespace SistemaX.Modules.Fiscal.Infrastructure.InMemory;

/// <summary>Adapter direto in-memory — usado por padrão em teste (sem contexto SQLite). Chave
/// sintética IDÊNTICA à de <c>SqliteRegraCfopRepository.ChaveSintetica</c> para que
/// <see cref="SalvarAsync"/> seja upsert idempotente nos dois adapters igualmente (editar uma
/// linha existente é atualização, nunca append duplicado — contract test roda 2× esperando o
/// MESMO comportamento).</summary>
public sealed class InMemoryRegraCfopRepository : IRegraCfopRepository
{
    private readonly ConcurrentDictionary<string, RegraCfop> _regras = new();

    public Task<RegraCfop?> ResolverAsync(
        string tenantId, TipoOperacaoFiscal tipoOperacao, bool ehInterestadual,
        bool destinatarioContribuinteIcms, NaturezaOperacaoProduto natureza, CancellationToken ct = default)
    {
        var melhor = _regras.Values
            .Where(r => (r.TenantId is null || r.TenantId == tenantId)
                     && r.TipoOperacao == tipoOperacao
                     && r.EhInterestadual == ehInterestadual
                     && r.DestinatarioContribuinteIcms == destinatarioContribuinteIcms
                     && r.Natureza == natureza)
            .OrderByDescending(r => r.Especificidade)
            .FirstOrDefault();

        return Task.FromResult(melhor);
    }

    public Task SalvarAsync(RegraCfop regra, CancellationToken ct = default)
    {
        _regras[ChaveSintetica(regra)] = regra;
        return Task.CompletedTask;
    }

    /// <summary>Mesma semântica do branch SQL de <c>SqliteRegraCfopRepository.ListarAsync</c>:
    /// <paramref name="tenantId"/> <c>null</c> → só as linhas DEFAULT (<c>TenantId</c> nulo);
    /// não-nulo → defaults + as linhas específicas daquele tenant.</summary>
    public Task<IReadOnlyList<RegraCfop>> ListarAsync(string? tenantId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<RegraCfop>>(
            _regras.Values.Where(r => r.TenantId is null || (tenantId is not null && r.TenantId == tenantId)).ToList());

    private static string ChaveSintetica(RegraCfop r) =>
        $"{r.TenantId ?? "*"}:{r.TipoOperacao}:{r.EhInterestadual}:{r.DestinatarioContribuinteIcms}:{r.Natureza}";
}
