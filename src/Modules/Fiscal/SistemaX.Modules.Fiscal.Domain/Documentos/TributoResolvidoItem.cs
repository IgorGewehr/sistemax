using SistemaX.Modules.Fiscal.Domain.Comum;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Fiscal.Domain.Documentos;

/// <summary>
/// Bag genérico e extensível — cada linha é um tributo incidente sobre o item, snapshot IMUTÁVEL
/// do que foi efetivamente calculado. É o que fecha o gap de reinferência do gestao-raiz: a
/// situação tributária REAL fica gravada junto do valor, para sempre, sem re-inferência futura
/// (docs/fiscal/arquitetura.md §2.6).
/// </summary>
public sealed record TributoResolvidoItem(
    TipoTributo Tipo,
    string? SituacaoTributaria,
    Money Base,
    Percentual Aliquota,
    Money Valor,
    Percentual? ReducaoBaseCalculo = null,
    Percentual? Mva = null);
