namespace SistemaX.Verticals.Assistencia;

/// <summary>Origem de uma <see cref="PecaAplicada"/> — distingue peça que já estava no
/// orçamento aprovado (sem efeito no valor combinado) de peça extra descoberta durante a
/// execução (mexe no total aprovado — exige <c>clienteAvisado</c>, ver guarda de valor em
/// <see cref="OrdemDeServico.AdicionarPecaExtra"/>).</summary>
public enum OrigemPeca
{
    Orcada,
    Extra
}
