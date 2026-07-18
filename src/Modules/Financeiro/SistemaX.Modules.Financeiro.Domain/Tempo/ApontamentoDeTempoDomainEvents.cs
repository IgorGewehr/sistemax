using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Domain.Tempo;

/// <summary>Evento de domínio de <see cref="ApontamentoDeTempo"/> — privado ao módulo (nada
/// cross-módulo acontece quando um apontamento é registrado, mesmo racional de
/// <c>ProjetoDomainEvents</c>).</summary>
public sealed record ApontamentoDeTempoRegistrado(
    string ApontamentoId, string BusinessId, string? ProjetoId, string? ClienteId, int Minutos, DateTimeOffset Data) : DomainEvent;
