namespace SistemaX.Modules.Fiscal.Domain.Documentos;

/// <summary>
/// Registro de uma Carta de Correção Eletrônica (CC-e) — SIDE-CHANNEL de texto, nunca uma
/// transição de <see cref="StatusDocumentoFiscal"/> (docs/fiscal/arquitetura.md §2.6: um
/// <see cref="DocumentoFiscal"/> <c>Autorizado</c> é imutável por design; erro leve pós-autorização
/// — endereço, dado complementar — vira este log, não uma edição do agregado). Só um objeto de
/// valor simples (não um <c>AggregateRoot</c>): não tem FSM própria, não levanta evento de domínio,
/// não é mutável depois de criado — a "verdade" de uma CC-e é 100% o texto enviado à SEFAZ.
///
/// <see cref="Sequencia"/> é o número de ordem da correção NESTE documento (1, 2, 3... — a SEFAZ
/// aceita até 20 por chave de acesso, layout NFe); quem calcula o próximo valor é
/// <c>EmitirCartaCorrecaoUseCase</c>, consultando <see cref="Application.Ports.ICartaCorrecaoFiscalRepository.ListarPorDocumentoAsync"/>
/// antes de gravar — nunca inventado aqui.
/// </summary>
public sealed record CartaCorrecaoFiscal(
    string Id,
    string TenantId,
    string DocumentoFiscalId,
    string ChaveDeAcesso,
    int Sequencia,
    string Texto,
    DateTimeOffset RegistradoEm);
