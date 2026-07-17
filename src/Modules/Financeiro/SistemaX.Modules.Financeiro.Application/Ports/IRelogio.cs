namespace SistemaX.Modules.Financeiro.Application.Ports;

/// <summary>
/// Abstração de tempo — nunca chame <c>DateTimeOffset.UtcNow</c> direto num caso de uso.
/// Torna determinístico testar cenários de "hoje" (vencimento, projeção de caixa).
/// </summary>
public interface IRelogio
{
    DateTimeOffset Agora();
}
