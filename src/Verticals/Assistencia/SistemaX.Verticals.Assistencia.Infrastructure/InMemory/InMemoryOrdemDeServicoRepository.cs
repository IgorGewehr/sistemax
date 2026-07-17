using System.Collections.Concurrent;
using SistemaX.Verticals.Assistencia.Application.Ports;

namespace SistemaX.Verticals.Assistencia.Infrastructure.InMemory;

/// <summary>
/// Adapter direto in-memory — mesmo racional de <c>InMemoryVendaRepository</c>
/// (Vendas.Infrastructure): suficiente para rodar o vertical e os testes sem infraestrutura
/// externa. A OS muda só por transição de FSM (não por digitação item-a-item do carrinho do PDV),
/// então salvar o agregado inteiro a cada caso de uso é suficiente — nenhum requisito extra de
/// crash-safety linha-a-linha aqui. Thread-safe via <see cref="ConcurrentDictionary{TKey,TValue}"/>.
/// </summary>
public sealed class InMemoryOrdemDeServicoRepository : IOrdemDeServicoRepository
{
    private readonly ConcurrentDictionary<string, OrdemDeServico> _porId = new();

    public Task<OrdemDeServico?> ObterPorIdAsync(string id, CancellationToken ct = default)
        => Task.FromResult(_porId.GetValueOrDefault(id));

    public Task SalvarAsync(OrdemDeServico ordemDeServico, CancellationToken ct = default)
    {
        _porId[ordemDeServico.Id] = ordemDeServico;
        return Task.CompletedTask;
    }
}
