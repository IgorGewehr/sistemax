namespace SistemaX.Modules.Financeiro.Application.ReadModels;

internal static class DateOnlyExtensions
{
    public static DateTimeOffset InicioDoDia(this DateOnly data) => new(data.Year, data.Month, data.Day, 0, 0, 0, TimeSpan.Zero);

    public static DateTimeOffset FimDoDia(this DateOnly data) => data.InicioDoDia().AddDays(1).AddTicks(-1);

    /// <summary>Trunca um instante (UTC) para o dia calendário — usado pra agrupar
    /// <c>MovimentoFinanceiro</c> por dia sem depender de fuso local (docs/wiring/
    /// financeiro-telas-restantes.md §3, agregação semanal do Bancário).</summary>
    public static DateOnly ParaDateOnly(this DateTimeOffset instante) => DateOnly.FromDateTime(instante.UtcDateTime);
}
