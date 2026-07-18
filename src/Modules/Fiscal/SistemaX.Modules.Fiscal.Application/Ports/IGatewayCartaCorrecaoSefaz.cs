using SistemaX.SharedKernel;

namespace SistemaX.Modules.Fiscal.Application.Ports;

/// <summary>
/// Porta irmã de <see cref="IGatewayEmissaoSefaz"/> — Carta de Correção Eletrônica é operação
/// SEFAZ distinta de emissão (mapeia para <c>/nfe/carta-correcao</c>), proposta original em
/// docs/fiscal/emissao-mapping.md §9/§11 (gap #11: "Cancelamento/Inutilização/CCe fora do escopo
/// de <c>IGatewayEmissaoSefaz</c> por design"). Erro leve pós-autorização (endereço, dado
/// complementar) — CCe é só texto, NUNCA muda nenhum campo de <c>DocumentoFiscal</c> nem sua FSM
/// (<c>Autorizado</c> continua imutável); o chamador (<c>EmitirCartaCorrecaoUseCase</c>) garante
/// isso validando <c>documento.Status == Autorizado</c> ANTES de chamar este port.
/// </summary>
public interface IGatewayCartaCorrecaoSefaz
{
    /// <summary>Assina/transmite uma CC-e. <paramref name="sequencia"/> é o número de ordem desta
    /// correção NESTE documento (1..20, layout NFe) — calculado pelo caller a partir do histórico
    /// já persistido, nunca inferido aqui. <paramref name="tenantId"/> resolve o certificado
    /// digital do emitente (mesmo insumo que toda operação SEFAZ deste gateway usa) — diferente da
    /// assinatura original proposta em emissao-mapping.md §9 porque aqui, ao contrário de
    /// <see cref="IGatewayEmissaoSefaz"/>, não há um <c>DocumentoFiscal</c> completo para derivar o
    /// tenant (só a chave de acesso já emitida).</summary>
    Task<Result> RegistrarCorrecaoAsync(
        string tenantId, string chaveAcesso, string correcao, string ufEmitente, int sequencia, CancellationToken ct = default);
}
