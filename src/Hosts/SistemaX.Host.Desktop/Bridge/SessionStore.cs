using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace SistemaX.Host.Desktop.Bridge;

/// <summary>Uma sessão Bearer — token opaco, TTL deslizante. Carrega <c>UsuarioId</c>, NÃO
/// <c>Papel</c> (ADR-0003 §3): papel/status são resolvidos frescos do <c>IUsuarioRepository</c> a
/// cada request por <see cref="BearerSessionMiddleware"/> — nunca cacheados no token, para que
/// revogar/rebaixar alguém tenha efeito imediato, não preso ao TTL de 12h.</summary>
public sealed record Sessao(string Token, string BusinessId, string UsuarioId, DateTimeOffset ExpiraEm);

/// <summary>
/// Sessões Bearer em memória (singleton do processo — cai com o host, e está tudo bem: o PDV
/// standalone não sobrevive à queda do próprio processo mesmo) + rate-limit de tentativas de
/// login, mesmo piso do plano de produção §6.1 (5 tentativas seguidas → lockout 60s).
/// </summary>
public sealed class SessionStore
{
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(12);
    private const int MaxTentativasFalhas = 5;
    private static readonly TimeSpan JanelaLockout = TimeSpan.FromSeconds(60);

    private readonly ConcurrentDictionary<string, Sessao> _sessoes = new();
    private int _tentativasFalhas;
    private DateTimeOffset? _bloqueadoAte;

    /// <summary>Verifica o lockout de login SEM registrar uma nova tentativa.</summary>
    public bool EstaBloqueado(out TimeSpan restante)
    {
        if (_bloqueadoAte is { } ate && ate > DateTimeOffset.UtcNow)
        {
            restante = ate - DateTimeOffset.UtcNow;
            return true;
        }

        restante = TimeSpan.Zero;
        return false;
    }

    public void RegistrarTentativaFalha()
    {
        if (Interlocked.Increment(ref _tentativasFalhas) >= MaxTentativasFalhas)
        {
            _bloqueadoAte = DateTimeOffset.UtcNow.Add(JanelaLockout);
            _tentativasFalhas = 0;
        }
    }

    public Sessao Criar(string businessId, string usuarioId)
    {
        _tentativasFalhas = 0;
        _bloqueadoAte = null;

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var sessao = new Sessao(token, businessId, usuarioId, DateTimeOffset.UtcNow.Add(Ttl));
        _sessoes[token] = sessao;
        return sessao;
    }

    /// <summary>Revoga o token imediatamente — usado por <see cref="BearerSessionMiddleware"/>
    /// quando o usuário da sessão foi desativado (ADR-0003 §3: "desativei o funcionário" tem
    /// efeito imediato, não em até 12h).</summary>
    public void Revogar(string token) => _sessoes.TryRemove(token, out _);

    /// <summary>Valida o token e renova o TTL (sliding). Retorna <c>null</c> se ausente/expirado —
    /// nesse caso a entrada expirada é removida.</summary>
    public Sessao? Validar(string token)
    {
        if (!_sessoes.TryGetValue(token, out var sessao))
        {
            return null;
        }

        if (sessao.ExpiraEm <= DateTimeOffset.UtcNow)
        {
            _sessoes.TryRemove(token, out _);
            return null;
        }

        var renovada = sessao with { ExpiraEm = DateTimeOffset.UtcNow.Add(Ttl) };
        _sessoes[token] = renovada;
        return renovada;
    }
}
