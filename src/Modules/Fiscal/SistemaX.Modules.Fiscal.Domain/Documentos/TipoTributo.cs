namespace SistemaX.Modules.Fiscal.Domain.Documentos;

public enum TipoTributo
{
    Icms,
    IcmsSt,

    /// <summary>Partilha de ICMS para o UF de destino em operação interestadual a consumidor
    /// final não-contribuinte (EC 87/2015/DIFAL). Resolvido pela MESMA <c>RegraFiscalPorOperacao</c>
    /// da operação (a chave já carrega <c>UfDestino</c>), nunca um cálculo à parte fora da
    /// tabela.</summary>
    IcmsDifal,

    /// <summary>Fundo de Combate à Pobreza — adicional de 1-2% que a maioria dos UFs cobra sobre
    /// a mesma base do DIFAL em operação interestadual a consumidor final. Dado por UF de
    /// destino, nunca hardcoded.</summary>
    Fcp,

    Ipi,
    Pis,
    Cofins,
    Iss

    // Ibs, Cbs — reserva Reforma Tributária (docs/fiscal/arquitetura.md §9).
}
