using System.Text;

namespace SistemaX.Infrastructure.Hardware.Devices.Scale;

/// <summary>
/// Parsers de protocolo de balança como funções PURAS <c>byte[] -&gt; Reading?</c> — cada
/// fabricante (Toledo, Filizola, Urano, Balmak, ...) tem seu formato de frame próprio; mapeia
/// 1:1 para o <c>Dictionary&lt;string, Func&lt;byte[], Reading?&gt;&gt;</c> (<c>PROTOCOL_PARSERS</c>)
/// do Supermarket-OS (ver docs/robustez §5). Adicionar um fabricante real = adicionar uma entrada
/// aqui seguindo o datasheet do protocolo dele, sem tocar em <see cref="SerialScaleAdapter"/>.
///
/// <see cref="GenericAsciiBcc"/> é o parser de REFERÊNCIA deste projeto: documenta uma estrutura
/// de frame com checksum (BCC = XOR de todos os bytes do payload) e VALIDA esse checksum antes de
/// aceitar a leitura — a fraqueza corrigida do Supermarket-OS (docs/robustez §5, fraqueza 3): lá,
/// o comentário do parser da Balmak citava checksum, mas o código real não validava o BCC,
/// deixando ruído de linha serial passar como leitura válida. Ao integrar um fabricante
/// específico, siga o datasheet exato dele, mas SEMPRE valide o checksum/CRC quando o protocolo
/// o definir — nunca aceite o frame sem essa checagem.
/// </summary>
public static class ScaleProtocolParsers
{
    public static readonly IReadOnlyDictionary<string, Func<byte[], Reading?>> Todos = new Dictionary<string, Func<byte[], Reading?>>(StringComparer.OrdinalIgnoreCase)
    {
        ["generic-ascii-bcc"] = GenericAsciiBcc
    };

    private const byte Stx = 0x02;
    private const byte Etx = 0x03;

    /// <summary>
    /// Frame de 11 bytes: STX(1) + sinal('+'/'-', 1) + peso em gramas (6 dígitos ASCII) +
    /// status('S'=estável/'I'=instável, 1) + ETX(1) + BCC(1, XOR de todos os bytes entre o sinal
    /// e o ETX, inclusive).
    /// </summary>
    public static Reading? GenericAsciiBcc(byte[] frame)
    {
        const int expectedLength = 11;
        if (frame.Length != expectedLength || frame[0] != Stx || frame[8] != Etx)
        {
            return null;
        }

        byte calculatedBcc = 0;
        for (var i = 1; i <= 8; i++) // do sinal (índice 1) até o ETX (índice 8), inclusive
        {
            calculatedBcc ^= frame[i];
        }

        if (calculatedBcc != frame[9])
        {
            // Checksum inválido — ruído de linha serial. NUNCA tratar como leitura real.
            return null;
        }

        var sign = frame[1] == (byte)'-' ? -1 : 1;
        var digits = Encoding.ASCII.GetString(frame, 2, 6);
        if (!int.TryParse(digits, out var grams))
        {
            return null;
        }

        var status = (char)frame[7];
        var estavel = status is 'S' or 's';

        return new Reading(sign * grams / 1000m, estavel, DateTimeOffset.UtcNow);
    }
}
