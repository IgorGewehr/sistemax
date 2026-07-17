using SistemaX.SharedKernel;

namespace SistemaX.Modules.Fiscal.Application.Ports;

/// <summary>
/// Autoridade de numeração fiscal — NUNCA CRDT, nunca "detectar colisão e renumerar depois"
/// (ADR-0002, docs/fiscal/arquitetura.md §5): um número de NF-e/NFC-e é um fato jurídico assim
/// que autorizado, não um identificador interno renumerável. NFC-e usa série dedicada por
/// terminal (autoridade = o próprio PDV); NF-e/MDF-e usam série única da loja (autoridade =
/// <c>Store.Server</c>). <c>Cloud.Api</c> nunca implementa este port com alocação real.
/// </summary>
public interface ISequenciaFiscalRepository
{
    /// <summary>Aloca o PRÓXIMO número via UPDATE atômico (ver
    /// <c>SistemaX.Infrastructure.Local.Sequences.ILocalSequenceAllocator</c>) — mesma lição de
    /// "a unidade de crash-safety é a transação do banco local" já registrada em
    /// docs/robustez/robustez-hardware-licoes.md §1. Falha (processo morre no meio) nunca deixa
    /// "número consumido, documento não gravado".</summary>
    Task<Result<long>> AlocarProximoAsync(string tenantId, string modelo, string serie, CancellationToken ct = default);
}
