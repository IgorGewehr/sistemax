using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Infrastructure.InMemory;

namespace SistemaX.Modules.Financeiro.Tests.Contracts;

/// <summary>Roda o contrato do port contra o adapter in-memory (produção hoje, quando
/// "persistencia" != "sqlite" na configuração da instalação).</summary>
public sealed class InMemoryContaAReceberRepositoryContractTests : ContaAReceberRepositoryContractTests
{
    protected override IContaAReceberRepository CriarRepositorio() => new InMemoryContaAReceberRepository();
}
