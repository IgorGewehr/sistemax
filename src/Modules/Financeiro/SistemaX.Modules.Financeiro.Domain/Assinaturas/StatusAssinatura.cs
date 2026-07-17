namespace SistemaX.Modules.Financeiro.Domain.Assinaturas;

/// <summary>Ciclo de vida de uma <see cref="Assinatura"/>. Inadimplência NÃO é status aqui — é
/// derivada dos recebíveis em atraso da assinatura (uma assinatura ativa pode estar inadimplente).</summary>
public enum StatusAssinatura
{
    Ativa,
    Pausada,
    Cancelada
}
