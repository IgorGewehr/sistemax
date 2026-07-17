using SistemaX.Modules.Fiscal.Application.Ports;
using SistemaX.Modules.Fiscal.Infrastructure.InMemory;

namespace SistemaX.Modules.Fiscal.Tests.Contracts;

public sealed class InMemoryDocumentoFiscalRepositoryContractTests : DocumentoFiscalRepositoryContractTests
{
    protected override IDocumentoFiscalRepository CriarRepositorio() => new InMemoryDocumentoFiscalRepository();
}
