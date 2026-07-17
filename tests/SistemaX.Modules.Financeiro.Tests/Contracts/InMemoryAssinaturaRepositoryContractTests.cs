using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Infrastructure.InMemory;

namespace SistemaX.Modules.Financeiro.Tests.Contracts;

public sealed class InMemoryAssinaturaRepositoryContractTests : AssinaturaRepositoryContractTests
{
    protected override IAssinaturaRepository CriarRepositorio() => new InMemoryAssinaturaRepository();
}
