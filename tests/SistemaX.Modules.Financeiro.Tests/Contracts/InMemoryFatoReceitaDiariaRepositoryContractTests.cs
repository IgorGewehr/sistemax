using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Infrastructure.InMemory;

namespace SistemaX.Modules.Financeiro.Tests.Contracts;

public sealed class InMemoryFatoReceitaDiariaRepositoryContractTests : FatoReceitaDiariaRepositoryContractTests
{
    protected override IFatoReceitaDiariaRepository CriarRepositorio() => new InMemoryFatoReceitaDiariaRepository();
}
