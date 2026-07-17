namespace SistemaX.Infrastructure.Local.Kv;

/// <summary>
/// Key-value simples e local (tabela <c>app_kv</c>) para estado interno de infraestrutura que
/// não merece uma tabela própria: timestamp do último <c>integrity_check</c>, identidade do
/// terminal, etc. Não é um cache de aplicação — é estado que precisa sobreviver a restart.
/// </summary>
public interface IAppKeyValueStore
{
    Task<string?> GetAsync(string key, CancellationToken ct = default);

    Task SetAsync(string key, string value, CancellationToken ct = default);
}
