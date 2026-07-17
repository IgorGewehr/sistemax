using SistemaX.Modules.Compras.Application.Ports;
using SistemaX.Modules.Compras.Infrastructure.InMemory;

namespace SistemaX.Modules.Compras.Tests.Contracts;

/// <summary>Roda o contrato do port contra o adapter in-memory (produção hoje, quando
/// "persistencia" != "sqlite" na configuração da instalação).</summary>
public sealed class InMemoryFornecedorRepositoryContractTests : FornecedorRepositoryContractTests
{
    protected override IFornecedorRepository CriarRepositorio() => new InMemoryFornecedorRepository();
}
