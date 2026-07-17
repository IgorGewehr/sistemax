using SistemaX.Modules.Fiscal.Application.Ports;
using SistemaX.Modules.Fiscal.Infrastructure.InMemory;

namespace SistemaX.Modules.Fiscal.Tests.Contracts;

public sealed class InMemoryReferenciaDevolucaoDocumentoFiscalRepositoryContractTests : ReferenciaDevolucaoDocumentoFiscalRepositoryContractTests
{
    protected override IReferenciaDevolucaoDocumentoFiscalRepository CriarRepositorio() => new InMemoryReferenciaDevolucaoDocumentoFiscalRepository();
}
