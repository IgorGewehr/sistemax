using SistemaX.Modules.Financeiro.Application.Ports;

namespace SistemaX.Modules.Financeiro.Infrastructure.Relogio;

public sealed class RelogioSistema : IRelogio
{
    public DateTimeOffset Agora() => DateTimeOffset.UtcNow;
}
