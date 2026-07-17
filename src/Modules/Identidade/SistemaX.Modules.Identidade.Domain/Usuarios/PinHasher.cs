using System.Security.Cryptography;

namespace SistemaX.Modules.Identidade.Domain.Usuarios;

/// <summary>
/// Hash de PIN — PBKDF2-SHA256, 210k iterações (piso recomendado pelo plano de produção §6.1).
/// Sem dependência nova: <see cref="Rfc2898DeriveBytes"/> é BCL. Comparação em tempo constante
/// (<see cref="CryptographicOperations.FixedTimeEquals"/>) contra timing attack.
///
/// MOVIDO de <c>Host.Desktop/Bridge/PinHasher.cs</c> para cá (ADR-0003 §1) — é lógica de domínio
/// de <see cref="Usuario"/> (como o PIN vira hash), não HTTP plumbing. Zero linha de comportamento
/// muda (mesmo PBKDF2 210k, mesmo FixedTimeEquals); só o namespace/projeto. Motivo estrutural:
/// um módulo NUNCA pode depender do Host — é o Host que depende dos módulos — então
/// <c>Identidade.Domain</c> não pode chamar um tipo que mora em <c>Bridge/</c>.
/// </summary>
public static class PinHasher
{
    private const int Iteracoes = 210_000;
    private const int TamanhoSaltBytes = 16;
    private const int TamanhoHashBytes = 32;

    public static (string HashBase64, string SaltBase64) Hash(string pin)
    {
        var salt = RandomNumberGenerator.GetBytes(TamanhoSaltBytes);
        var hash = Rfc2898DeriveBytes.Pbkdf2(pin, salt, Iteracoes, HashAlgorithmName.SHA256, TamanhoHashBytes);
        return (Convert.ToBase64String(hash), Convert.ToBase64String(salt));
    }

    public static bool Verificar(string pin, string hashBase64, string saltBase64)
    {
        var salt = Convert.FromBase64String(saltBase64);
        var hashEsperado = Convert.FromBase64String(hashBase64);
        var hashCalculado = Rfc2898DeriveBytes.Pbkdf2(pin, salt, Iteracoes, HashAlgorithmName.SHA256, hashEsperado.Length);
        return CryptographicOperations.FixedTimeEquals(hashCalculado, hashEsperado);
    }
}
