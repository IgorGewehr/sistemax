using System.Collections.Concurrent;
using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Application.Quant;

namespace SistemaX.Modules.Financeiro.Infrastructure.InMemory;

/// <summary>Adapter in-memory de <see cref="IConfiguracaoRadarSimplesRepository"/> — suficiente
/// para testes e para rodar sem persistência sqlite (mesmo padrão dos demais repos InMemory deste
/// módulo). Ausência de chave = tenant não personalizou (o serviço cai para o padrão).</summary>
public sealed class InMemoryConfiguracaoRadarSimplesRepository : IConfiguracaoRadarSimplesRepository
{
    private readonly ConcurrentDictionary<string, IReadOnlyList<MapeamentoCorrenteAnexo>> _porTenant = new();

    public Task<IReadOnlyList<MapeamentoCorrenteAnexo>?> ObterAsync(string businessId, CancellationToken ct = default)
        => Task.FromResult(_porTenant.TryGetValue(businessId, out var mapeamento) ? mapeamento : null);

    public Task SalvarAsync(string businessId, IReadOnlyList<MapeamentoCorrenteAnexo> mapeamento, CancellationToken ct = default)
    {
        _porTenant[businessId] = mapeamento;
        return Task.CompletedTask;
    }
}
