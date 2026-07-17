using SistemaX.Modules.Financeiro.Application.Ports;

namespace SistemaX.Modules.Financeiro.Tests.Fakes;

/// <summary>Relógio determinístico — testes de vencimento/projeção nunca devem depender de DateTimeOffset.UtcNow real.</summary>
public sealed class FakeRelogio(DateTimeOffset agora) : IRelogio
{
    public DateTimeOffset Momento { get; set; } = agora;

    public DateTimeOffset Agora() => Momento;
}
