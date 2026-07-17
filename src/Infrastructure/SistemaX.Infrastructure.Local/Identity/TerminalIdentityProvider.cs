using SistemaX.Infrastructure.Local.Ids;
using SistemaX.Infrastructure.Local.Kv;

namespace SistemaX.Infrastructure.Local.Identity;

/// <inheritdoc cref="ITerminalIdentity"/>
public sealed class TerminalIdentityProvider(IAppKeyValueStore kv) : ITerminalIdentity
{
    private const string Key = "terminal_id";

    private string? _cached;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<string> GetTerminalIdAsync(CancellationToken ct = default)
    {
        if (_cached is not null)
        {
            return _cached;
        }

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_cached is not null)
            {
                return _cached;
            }

            var existing = await kv.GetAsync(Key, ct).ConfigureAwait(false);
            if (existing is not null)
            {
                _cached = existing;
                return _cached;
            }

            var generated = UlidGenerator.NewUlid();
            await kv.SetAsync(Key, generated, ct).ConfigureAwait(false);
            _cached = generated;
            return _cached;
        }
        finally
        {
            _gate.Release();
        }
    }
}
