using SistemaX.Modules.Identidade.Application.Ports;
using SistemaX.Modules.Identidade.Infrastructure.InMemory;

namespace SistemaX.Modules.Identidade.Tests.Contracts;

/// <summary>Roda o contrato do port contra o adapter in-memory (produção hoje, quando
/// "persistencia" != "sqlite" na configuração da instalação).</summary>
public sealed class InMemoryUsuarioRepositoryContractTests : UsuarioRepositoryContractTests
{
    protected override IUsuarioRepository CriarRepositorio() => new InMemoryUsuarioRepository();
}
