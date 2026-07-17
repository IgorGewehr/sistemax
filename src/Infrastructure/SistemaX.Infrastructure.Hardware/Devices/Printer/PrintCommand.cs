using System.Text.Json.Serialization;

namespace SistemaX.Infrastructure.Hardware.Devices.Printer;

public enum TextAlign
{
    Esquerda,
    Centro,
    Direita
}

public enum BarcodeSymbology
{
    Code128,
    Ean13,
    Code39
}

/// <summary>
/// Comando de impressão como DTO tipado, desacoplado do transporte físico (rede/serial/USB) e
/// do fabricante — o mapeamento pra bytes ESC/POS concretos vive em <see cref="EscPosBuilder"/>.
/// Union type via record hierarchy (padrão do <c>types.ts</c> do Supermarket-OS, ver docs/robustez §5).
/// Os atributos <c>[JsonDerivedType]</c> habilitam serialização polimórfica via
/// <c>System.Text.Json</c> — necessário para persistir a lista de comandos como JSON na fila de
/// impressão (<see cref="PrintQueue.IPrintQueueStore"/>) e reidratar corretamente no retry.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$tipo")]
[JsonDerivedType(typeof(Texto), "texto")]
[JsonDerivedType(typeof(LinhaSeparadora), "linha")]
[JsonDerivedType(typeof(CodigoBarras), "barras")]
[JsonDerivedType(typeof(QrCode), "qrcode")]
[JsonDerivedType(typeof(Avanco), "avanco")]
[JsonDerivedType(typeof(Corte), "corte")]
[JsonDerivedType(typeof(Imagem), "imagem")]
[JsonDerivedType(typeof(AbrirGaveta), "abrir_gaveta")]
public abstract record PrintCommand
{
    private PrintCommand() { }

    public sealed record Texto(string Conteudo, bool Negrito = false, bool DuplaLargura = false, TextAlign Alinhamento = TextAlign.Esquerda) : PrintCommand;

    public sealed record LinhaSeparadora(char Caractere = '-') : PrintCommand;

    public sealed record CodigoBarras(string Conteudo, BarcodeSymbology Simbologia = BarcodeSymbology.Code128) : PrintCommand;

    public sealed record QrCode(string Conteudo) : PrintCommand;

    public sealed record Avanco(int Linhas = 1) : PrintCommand;

    public sealed record Corte(bool Total = true) : PrintCommand;

    public sealed record Imagem(byte[] BitmapMonocromatico, int LarguraPixels) : PrintCommand;

    /// <summary>
    /// Abre a gaveta de dinheiro — NÃO é um dispositivo próprio, é um comando ESC/POS enviado
    /// pela MESMA conexão da impressora (ver docs/robustez §5: "nem todo dispositivo é uma
    /// conexão física separada"). Ver <see cref="CashDrawer.PrinterDrivenCashDrawerAdapter"/>.
    /// </summary>
    public sealed record AbrirGaveta : PrintCommand;
}
