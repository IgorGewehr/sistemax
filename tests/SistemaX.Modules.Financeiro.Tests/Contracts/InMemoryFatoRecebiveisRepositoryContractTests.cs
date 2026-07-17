using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Infrastructure.InMemory;

namespace SistemaX.Modules.Financeiro.Tests.Contracts;

public sealed class InMemoryFatoRecebiveisRepositoryContractTests : FatoRecebiveisRepositoryContractTests
{
    protected override IFatoRecebiveisRepository CriarRepositorio() => new InMemoryFatoRecebiveisRepository();
}
