using System.Security.Cryptography;

namespace SistemaX.Infrastructure.Local.Ids;

/// <summary>
/// Gerador de ULID (Universally Unique Lexicographically Sortable Identifier): 48 bits de
/// timestamp em milissegundos + 80 bits de aleatoriedade criptográfica, codificados em
/// Crockford Base32 (26 caracteres, ordenável por string = ordenável por tempo de criação).
///
/// POR QUÊ ISTO EXISTE (lição do Supermarket-OS, ver docs/robustez/robustez-hardware-licoes.md
/// §3): os triggers de outbox de lá geravam a chave de idempotência como
/// <c>&lt;entidade&gt;:&lt;id&gt;:&lt;segundo-unix&gt;:&lt;ação&gt;</c> — granularidade de 1
/// SEGUNDO. Duas escritas na mesma linha dentro do mesmo segundo (perfeitamente plausível em
/// um lote) colidiam com o UNIQUE INDEX da chave e derrubavam a transação inteira, inclusive a
/// venda que disparou o trigger. Este gerador nunca usa timestamp como parte determinante da
/// unicidade: mesmo dentro do MESMO milissegundo, a parte aleatória é incrementada
/// monotonicamente (nunca reamostrada do zero), então duas chamadas consecutivas jamais colidem,
/// independentemente da velocidade de geração.
/// </summary>
public static class UlidGenerator
{
    private const string CrockfordAlphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    private static readonly object Gate = new();
    private static long _lastTimestampMs = -1;
    private static readonly byte[] LastRandom = new byte[10];

    /// <summary>Gera o próximo ULID. Thread-safe; monotônico mesmo sob alta concorrência.</summary>
    public static string NewUlid()
    {
        Span<byte> bytes = stackalloc byte[16];

        lock (Gate)
        {
            long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            if (nowMs > _lastTimestampMs)
            {
                _lastTimestampMs = nowMs;
                RandomNumberGenerator.Fill(LastRandom);
            }
            else
            {
                // Mesmo milissegundo do último ID gerado (ou relógio andou pra trás):
                // incrementa a parte aleatória em vez de reamostrar — garante que o novo ID
                // é estritamente maior que o anterior (monotonicidade) e nunca colide, mesmo
                // gerando milhares de IDs no mesmo tick de clock.
                IncrementRandomPart();
            }

            WriteTimestamp(bytes, _lastTimestampMs);
            LastRandom.CopyTo(bytes[6..]);
        }

        return Encode(bytes);
    }

    private static void IncrementRandomPart()
    {
        for (int i = LastRandom.Length - 1; i >= 0; i--)
        {
            if (++LastRandom[i] != 0)
            {
                return;
            }
            // overflow neste byte: propaga o carry pro byte mais significativo (loop continua)
        }
        // Os 80 bits de aleatoriedade deram a volta inteira (praticamente impossível em uso
        // real: exigiria 2^80 gerações no mesmo milissegundo) — a próxima chamada com um novo
        // timestamp resolve naturalmente.
    }

    private static void WriteTimestamp(Span<byte> destination, long timestampMs)
    {
        // 48 bits big-endian nos primeiros 6 bytes.
        destination[0] = (byte)(timestampMs >> 40);
        destination[1] = (byte)(timestampMs >> 32);
        destination[2] = (byte)(timestampMs >> 24);
        destination[3] = (byte)(timestampMs >> 16);
        destination[4] = (byte)(timestampMs >> 8);
        destination[5] = (byte)timestampMs;
    }

    private static string Encode(ReadOnlySpan<byte> data)
    {
        // Lê os 128 bits em grupos de 5 bits (base32), MSB primeiro. 128 não é múltiplo de 5:
        // o último grupo fica com 3 bits válidos + 2 bits de padding à direita — dá exatamente
        // 26 caracteres, o tamanho fixo e bem conhecido de um ULID.
        Span<char> output = stackalloc char[26];
        int outIndex = 0;
        int bitsInBuffer = 0;
        int buffer = 0;

        foreach (byte b in data)
        {
            buffer = (buffer << 8) | b;
            bitsInBuffer += 8;

            while (bitsInBuffer >= 5)
            {
                bitsInBuffer -= 5;
                int index = (buffer >> bitsInBuffer) & 0x1F;
                output[outIndex++] = CrockfordAlphabet[index];
            }
        }

        if (bitsInBuffer > 0)
        {
            int index = (buffer << (5 - bitsInBuffer)) & 0x1F;
            output[outIndex++] = CrockfordAlphabet[index];
        }

        return new string(output[..outIndex]);
    }
}
