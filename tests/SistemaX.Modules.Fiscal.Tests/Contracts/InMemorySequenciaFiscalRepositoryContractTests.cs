using SistemaX.Modules.Fiscal.Application.Ports;
using SistemaX.Modules.Fiscal.Infrastructure.InMemory;

namespace SistemaX.Modules.Fiscal.Tests.Contracts;

public sealed class InMemorySequenciaFiscalRepositoryContractTests : SequenciaFiscalRepositoryContractTests
{
    protected override ISequenciaFiscalRepository CriarRepositorio() => new InMemorySequenciaFiscalRepository();
}
