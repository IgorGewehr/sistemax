using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Infrastructure.InMemory;

namespace SistemaX.Modules.Financeiro.Tests.Contracts;

public sealed class InMemoryAporteDeCapitalRepositoryContractTests : AporteDeCapitalRepositoryContractTests
{
    protected override IAporteDeCapitalRepository CriarRepositorio() => new InMemoryAporteDeCapitalRepository();
}
