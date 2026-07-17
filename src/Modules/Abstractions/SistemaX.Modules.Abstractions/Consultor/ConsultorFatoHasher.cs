using System.Security.Cryptography;
using System.Text;

namespace SistemaX.Modules.Abstractions.Consultor;

/// <summary>
/// Hash estável dos <see cref="ConsultorFato.Facts"/> — a chave de "os inputs mudaram?" do cache do
/// Super Consultor (ADR-0005 §3.5: "Se sha256(Facts) == hash do último narrado da mesma rule →
/// reusa frase antiga (custo 0)"). Mesmo padrão de
/// <c>saas-erp/app/api/financial/consultor/route.ts</c> (<c>factsHash8</c>): SHA-256 sobre as
/// chaves ORDENADAS (nunca a ordem de inserção do dicionário, que não é garantida), truncado a 8
/// hex — colisão desprezível para o volume de regras deste sistema, e mais curto para logs/chaves
/// de cache.
/// </summary>
public static class ConsultorFatoHasher
{
    /// <summary>Delimitador entre pares chave=valor — sem ele, {"a":"1","bc":"2"} colidiria com
    /// {"ab":"1","c":"2"} na concatenação.</summary>
    private const string Separador = "|";

    public static string Hash(IReadOnlyDictionary<string, string> facts)
    {
        var normalizado = string.Join(
            Separador,
            facts.OrderBy(par => par.Key, StringComparer.Ordinal).Select(par => $"{par.Key}={par.Value}"));

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalizado));
        return Convert.ToHexString(bytes)[..8].ToLowerInvariant();
    }
}
