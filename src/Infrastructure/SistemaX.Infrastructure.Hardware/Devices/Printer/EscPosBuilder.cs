using System.Text;

namespace SistemaX.Infrastructure.Hardware.Devices.Printer;

/// <summary>
/// Converte a lista de <see cref="PrintCommand"/> (DTO portável, sem conhecimento de transporte)
/// em bytes ESC/POS concretos. Isolar isto num builder puro (sem I/O) é o que torna o adapter de
/// transporte (TCP, serial, USB) trivial — ele só manda os bytes prontos.
///
/// Codepage: impressoras térmicas ESC/POS no Brasil tipicamente usam CP860 ou CP850 para
/// acentuação; usamos <c>IBM860</c> por padrão (o mais comum em firmwares POS nacionais), com
/// fallback de caractere '?' para o que não mapear — nunca lança por causa de um caractere
/// especial no texto de um cupom.
/// </summary>
public static class EscPosBuilder
{
    private const byte Esc = 0x1B;
    private const byte Gs = 0x1D;

    static EscPosBuilder()
    {
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
    }

    public static byte[] Build(IReadOnlyList<PrintCommand> commands, int codePage = 860)
    {
        var encoding = ResolveEncoding(codePage);
        using var buffer = new MemoryStream();

        WriteInitialize(buffer);

        foreach (var command in commands)
        {
            switch (command)
            {
                case PrintCommand.Texto texto:
                    WriteTexto(buffer, encoding, texto);
                    break;
                case PrintCommand.LinhaSeparadora linha:
                    WriteLinha(buffer, encoding, linha);
                    break;
                case PrintCommand.CodigoBarras barras:
                    WriteBarcode(buffer, encoding, barras);
                    break;
                case PrintCommand.QrCode qr:
                    WriteQrCode(buffer, encoding, qr);
                    break;
                case PrintCommand.Avanco avanco:
                    buffer.WriteByte(Esc);
                    buffer.WriteByte((byte)'d');
                    buffer.WriteByte((byte)Math.Clamp(avanco.Linhas, 0, 255));
                    break;
                case PrintCommand.Corte corte:
                    buffer.WriteByte(Gs);
                    buffer.WriteByte((byte)'V');
                    buffer.WriteByte((byte)(corte.Total ? 0 : 1));
                    break;
                case PrintCommand.Imagem imagem:
                    WriteImagemRaster(buffer, imagem);
                    break;
                case PrintCommand.AbrirGaveta:
                    WriteAbrirGaveta(buffer);
                    break;
            }
        }

        return buffer.ToArray();
    }

    /// <summary>Byte-sequence isolada, reaproveitada por <see cref="CashDrawer.PrinterDrivenCashDrawerAdapter"/> sem montar um cupom inteiro.</summary>
    public static byte[] BuildAbrirGavetaStandalone()
    {
        using var buffer = new MemoryStream();
        WriteAbrirGaveta(buffer);
        return buffer.ToArray();
    }

    private static void WriteInitialize(Stream buffer)
    {
        buffer.WriteByte(Esc);
        buffer.WriteByte((byte)'@'); // ESC @ — reset do estado da impressora (evita herdar negrito/largura dupla de um job anterior)
    }

    private static void WriteTexto(Stream buffer, Encoding encoding, PrintCommand.Texto texto)
    {
        buffer.WriteByte(Esc);
        buffer.WriteByte((byte)'a');
        buffer.WriteByte((byte)AlinhamentoParaByte(texto.Alinhamento));

        buffer.WriteByte(Esc);
        buffer.WriteByte((byte)'E');
        buffer.WriteByte((byte)(texto.Negrito ? 1 : 0));

        // GS ! — largura dupla é um bit da máscara (bit 5), nunca deixamos vazar pro próximo
        // comando: sempre resetamos pra 0x00 logo depois de escrever o texto.
        buffer.WriteByte(Gs);
        buffer.WriteByte((byte)'!');
        buffer.WriteByte((byte)(texto.DuplaLargura ? 0x20 : 0x00));

        var bytes = encoding.GetBytes(texto.Conteudo);
        buffer.Write(bytes, 0, bytes.Length);
        buffer.WriteByte((byte)'\n');

        // Reset defensivo: mesmo que o próximo comando não seja outro Texto, garante que negrito/
        // largura dupla não vazam para comandos subsequentes (ex.: uma LinhaSeparadora).
        buffer.WriteByte(Gs);
        buffer.WriteByte((byte)'!');
        buffer.WriteByte(0x00);
        buffer.WriteByte(Esc);
        buffer.WriteByte((byte)'E');
        buffer.WriteByte(0x00);
    }

    private static void WriteLinha(Stream buffer, Encoding encoding, PrintCommand.LinhaSeparadora linha)
    {
        var bytes = encoding.GetBytes(new string(linha.Caractere, 32));
        buffer.Write(bytes, 0, bytes.Length);
        buffer.WriteByte((byte)'\n');
    }

    private static void WriteBarcode(Stream buffer, Encoding encoding, PrintCommand.CodigoBarras barras)
    {
        var conteudo = barras.Simbologia == BarcodeSymbology.Code128
            ? "{B" + barras.Conteudo // seleciona Code Set B (ASCII) do Code128 — simplificação comum para conteúdo alfanumérico
            : barras.Conteudo;

        var dados = encoding.GetBytes(conteudo);
        var m = barras.Simbologia switch
        {
            BarcodeSymbology.Ean13 => (byte)67,
            BarcodeSymbology.Code39 => (byte)69,
            BarcodeSymbology.Code128 => (byte)73,
            _ => (byte)73
        };

        // GS k m n d1..dn — "function B": comprimento explícito, sem terminador NUL.
        buffer.WriteByte(Gs);
        buffer.WriteByte((byte)'k');
        buffer.WriteByte(m);
        buffer.WriteByte((byte)Math.Clamp(dados.Length, 0, 255));
        buffer.Write(dados, 0, dados.Length);
    }

    private static void WriteQrCode(Stream buffer, Encoding encoding, PrintCommand.QrCode qr)
    {
        var dados = encoding.GetBytes(qr.Conteudo);

        // Função 165 — seleciona o modelo (modelo 2, o mais comum).
        WriteGsK(buffer, [0x31, 0x41, 0x32, 0x00]);

        // Função 167 — tamanho do módulo (1-16; 6 é um bom padrão legível pra recibo estreito).
        WriteGsK(buffer, [0x31, 0x43, 0x06]);

        // Função 169 — nível de correção de erro (48=L, 49=M, 50=Q, 51=H).
        WriteGsK(buffer, [0x31, 0x45, 0x31]);

        // Função 180 — armazena os dados (comprimento = dados + 3 bytes de cabeçalho fixo).
        var storeArgs = new byte[3 + dados.Length];
        storeArgs[0] = 0x31;
        storeArgs[1] = 0x50;
        storeArgs[2] = 0x30;
        dados.CopyTo(storeArgs, 3);
        WriteGsK(buffer, storeArgs);

        // Função 181 — imprime o QR armazenado.
        WriteGsK(buffer, [0x31, 0x51, 0x30]);
    }

    private static void WriteGsK(Stream buffer, byte[] args)
    {
        var length = args.Length;
        buffer.WriteByte(Gs);
        buffer.WriteByte((byte)'(');
        buffer.WriteByte((byte)'k');
        buffer.WriteByte((byte)(length & 0xFF));         // pL
        buffer.WriteByte((byte)((length >> 8) & 0xFF));  // pH
        buffer.Write(args, 0, args.Length);
    }

    private static void WriteImagemRaster(Stream buffer, PrintCommand.Imagem imagem)
    {
        // GS v 0 — imprime bitmap raster monocromático (1 bit por pixel, já empacotado em bytes
        // pelo chamador). widthBytes = ceil(largura/8).
        var widthBytes = (imagem.LarguraPixels + 7) / 8;
        var heightLines = widthBytes == 0 ? 0 : imagem.BitmapMonocromatico.Length / widthBytes;

        buffer.WriteByte(Gs);
        buffer.WriteByte((byte)'v');
        buffer.WriteByte((byte)'0');
        buffer.WriteByte(0); // m: modo normal
        buffer.WriteByte((byte)(widthBytes & 0xFF));
        buffer.WriteByte((byte)((widthBytes >> 8) & 0xFF));
        buffer.WriteByte((byte)(heightLines & 0xFF));
        buffer.WriteByte((byte)((heightLines >> 8) & 0xFF));
        buffer.Write(imagem.BitmapMonocromatico, 0, imagem.BitmapMonocromatico.Length);
    }

    private static void WriteAbrirGaveta(Stream buffer)
    {
        // ESC p m t1 t2 — pulso na pino 2 (m=0), tempos de pulso padrão (t1=25, t2=250 => ~50ms/500ms).
        buffer.WriteByte(Esc);
        buffer.WriteByte((byte)'p');
        buffer.WriteByte(0);
        buffer.WriteByte(25);
        buffer.WriteByte(250);
    }

    private static int AlinhamentoParaByte(TextAlign alinhamento) => alinhamento switch
    {
        TextAlign.Esquerda => 0,
        TextAlign.Centro => 1,
        TextAlign.Direita => 2,
        _ => 0
    };

    private static Encoding ResolveEncoding(int codePage)
    {
        try
        {
            return Encoding.GetEncoding(codePage);
        }
        catch (ArgumentException)
        {
            // Codepage não suportada nesta plataforma/runtime — cai pro Latin1 (ASCII estendido),
            // que ao menos não lança; acentuação pode sair incorreta, mas o cupom sai.
            return Encoding.Latin1;
        }
    }
}
