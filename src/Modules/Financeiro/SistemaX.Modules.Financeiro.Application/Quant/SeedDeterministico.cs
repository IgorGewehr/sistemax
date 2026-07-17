using System.Text;

namespace SistemaX.Modules.Financeiro.Application.Quant;

/// <summary>
/// Seed determinística para qualquer simulação com aleatoriedade controlada — regra dura do plano
/// de inteligência do Financeiro (docs/financeiro/inteligencia-arquitetura.md §3.4/ADR-0005):
/// "aleatoriedade (bootstrap do fluxo de caixa) sempre com seed fixa derivada de tenantId+período
/// → mesmo input, mesmo output, sempre".
///
/// NUNCA use <c>string.GetHashCode()</c> aqui: por padrão o .NET randomiza o hash de <c>string</c>
/// por PROCESSO (proteção contra ataque de colisão de hash) — a MESMA string produz hashes
/// DIFERENTES em execuções diferentes do mesmo programa, o que quebraria a reprodutibilidade que
/// esta classe existe para garantir. FNV-1a 64 bits é um hash não-criptográfico determinístico e
/// estável entre processos/versões de runtime — exatamente o que "mesmo input, mesmo output"
/// pede, sem nenhuma pretensão de segurança.
/// </summary>
public static class SeedDeterministico
{
    private const ulong FnvOffsetBasis = 14695981039346656037UL;
    private const ulong FnvPrime = 1099511628211UL;

    /// <summary>
    /// Gera um <see cref="int"/> determinístico a partir de <paramref name="tenantId"/> +
    /// <paramref name="periodoRef"/> — pronto para alimentar <c>new Random(seed)</c>.
    /// <see cref="System.Random"/> só aceita seed <see cref="int"/> (sem overload de
    /// <see cref="long"/>); o dobramento XOR de 64→32 bits abaixo é a técnica padrão pra reduzir a
    /// largura sem introduzir uma fonte de aleatoriedade não-determinística no processo.
    /// </summary>
    public static int Gerar(string tenantId, string periodoRef)
    {
        var chave = $"{tenantId}:{periodoRef}";
        var hash = FnvOffsetBasis;
        foreach (var b in Encoding.UTF8.GetBytes(chave))
        {
            hash ^= b;
            hash *= FnvPrime;
        }

        return unchecked((int)(hash ^ (hash >> 32)));
    }
}
