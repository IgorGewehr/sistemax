using SistemaX.Modules.Vendas.Application.Ports;
using SistemaX.Modules.Vendas.Infrastructure.InMemory;

namespace SistemaX.Modules.Vendas.Tests.Contracts;

/// <summary>Roda o contrato do port contra o adapter in-memory (produção hoje, quando
/// "persistencia" != "sqlite" na configuração da instalação).</summary>
public sealed class InMemoryVendaRepositoryContractTests : VendaRepositoryContractTests
{
    protected override IVendaRepository CriarRepositorio() => new InMemoryVendaRepository();
}
