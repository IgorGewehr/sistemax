namespace SistemaX.Infrastructure.Hardware.Devices.Tef;

/// <summary>
/// Fábrica por nome de provedor — o resto do app só conhece <see cref="ITefAdapter"/>; trocar de
/// adquirente é configuração (ver docs/robustez §5). Adapters reais (PayGo/SiTef/Stone/Cappta/
/// ConnectTef) entram aqui quando integrados; até lá, provedores desconhecidos caem no Null Object.
/// </summary>
public sealed class TefProviderFactory
{
    public ITefAdapter Create(string provider)
    {
        return provider.ToLowerInvariant() switch
        {
            "mock" => new MockTefAdapter(),
            // "paygo" => new PayGoTefAdapter(...),
            // "sitef" => new SiTefAdapter(...),
            // "stone" => new StoneTefAdapter(...),
            // "cappta" => new CapptaTefAdapter(...),
            // "connecttef" => new ConnectTefAdapter(...),
            _ => new NullTefAdapter(provider)
        };
    }
}
