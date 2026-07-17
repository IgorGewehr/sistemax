namespace SistemaX.Verticals.Assistencia;

/// <summary>Objeto de valor: o equipamento trazido pelo cliente. Vive só dentro de uma
/// <see cref="OrdemDeServico"/> — não é uma entidade própria neste vertical (1 OS = 1
/// equipamento; múltiplos aparelhos viram múltiplas OS — elimina a complexidade de "itens
/// de OS", ver §3 do plano).</summary>
/// <param name="SenhaAcesso">Necessidade real de bancada (desbloquear o aparelho para
/// testar). NUNCA sai em impressão, listagem ou log — ver <see cref="ToString"/>.</param>
public sealed record Equipamento(
    string Tipo,
    string Marca,
    string Modelo,
    string? NumeroSerie = null,
    string? SenhaAcesso = null,
    string? Acessorios = null,
    string? EstadoEntrada = null)
{
    /// <summary>
    /// Um <c>record</c> gera <c>ToString</c> automaticamente com TODAS as propriedades — isso
    /// vazaria <see cref="SenhaAcesso"/> em qualquer log/interpolação acidental
    /// (<c>$"{equipamento}"</c>, <c>logger.LogInformation("{Eq}", equipamento)</c>). Sobrescrever
    /// aqui é a única forma de tornar essa regra de produto ("senha nunca impressa")
    /// estruturalmente impossível de violar por descuido, em vez de confiar que toda camada de
    /// apresentação lembre de mascarar.
    /// </summary>
    public override string ToString() =>
        $"Equipamento {{ Tipo = {Tipo}, Marca = {Marca}, Modelo = {Modelo}, NumeroSerie = {NumeroSerie}, " +
        $"SenhaAcesso = {(SenhaAcesso is null ? "null" : "***")}, Acessorios = {Acessorios}, EstadoEntrada = {EstadoEntrada} }}";
}
