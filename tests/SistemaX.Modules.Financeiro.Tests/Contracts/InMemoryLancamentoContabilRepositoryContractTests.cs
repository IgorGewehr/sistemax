using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Infrastructure.InMemory;

namespace SistemaX.Modules.Financeiro.Tests.Contracts;

public sealed class InMemoryLancamentoContabilRepositoryContractTests : LancamentoContabilRepositoryContractTests
{
    protected override ILancamentoContabilRepository CriarRepositorio() => new InMemoryLancamentoContabilRepository();
}
