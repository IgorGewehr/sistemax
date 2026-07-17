using SistemaX.Modules.Fiscal.Application.Ports;
using SistemaX.Modules.Fiscal.Infrastructure.InMemory;

namespace SistemaX.Modules.Fiscal.Tests.Contracts;

public sealed class InMemoryCertificadoDigitalRepositoryContractTests : CertificadoDigitalRepositoryContractTests
{
    protected override ICertificadoDigitalRepository CriarRepositorio() => new InMemoryCertificadoDigitalRepository();
}
