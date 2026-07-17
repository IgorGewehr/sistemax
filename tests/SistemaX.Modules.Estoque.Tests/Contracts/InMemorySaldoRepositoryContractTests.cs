using SistemaX.Modules.Estoque.Application.Ports;
using SistemaX.Modules.Estoque.Infrastructure.InMemory;

namespace SistemaX.Modules.Estoque.Tests.Contracts;

/// <summary>Roda o contrato do port contra o adapter in-memory (produção hoje, quando
/// "persistencia" != "sqlite" na configuração da instalação).</summary>
public sealed class InMemorySaldoRepositoryContractTests : SaldoRepositoryContractTests
{
    protected override ISaldoRepository CriarRepositorio() => new InMemorySaldoRepository();
}
