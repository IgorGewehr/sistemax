namespace SistemaX.Modules.Fiscal.Domain.Documentos;

/// <summary><c>NFSe</c> reserva de extensão — cálculo de ISS por município fora de escopo desta
/// fase (docs/fiscal/arquitetura.md §9). <c>MDFe</c> reutiliza o mesmo agregado com uma FSM mais
/// rica não detalhada aqui (§10).</summary>
public enum TipoDocumentoFiscal
{
    NFe,
    NFCe,
    NFSe,
    MDFe
}
