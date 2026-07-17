namespace SistemaX.Modules.Financeiro.Application.Comum;

/// <summary>
/// Política de tempo ÚNICA para bucketing de séries diárias (F0 do plano de inteligência do
/// Financeiro — docs/financeiro/inteligencia-arquitetura.md §3.3/ADR-0005). Substitui o padrão
/// <c>DateOnly.FromDateTime(x.UtcDateTime)</c> — bug documentado no diagnóstico do plano: uma
/// venda das 22h (horário local) cai no "dia seguinte" quando bucketada em UTC-3, ~8-12% de ruído
/// sistemático numa série diária.
///
/// Fase 0 fixa TODOS os tenants no fuso de América/São Paulo — quando o produto precisar de fuso
/// configurável por tenant, este é o ÚNICO ponto que muda (nenhum fold deveria calcular a data
/// local por conta própria).
/// </summary>
public static class BucketingTemporalDoTenant
{
    private static readonly Lazy<TimeZoneInfo> FusoPadrao = new(ResolverFusoPadrao);

    /// <summary>Converte um instante absoluto no "dia" local do tenant, para bucketing de fact
    /// tables diárias (ex.: <c>fato_receita_diaria</c>, <c>fato_caixa_diario</c>).</summary>
    public static DateOnly DiaLocal(DateTimeOffset instante)
        => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instante, FusoPadrao.Value).DateTime);

    private static TimeZoneInfo ResolverFusoPadrao()
    {
        // IANA (macOS/Linux e Windows moderno com ICU) primeiro; nome legado do Windows como
        // segundo fallback; fuso fixo -03:00 (sem horário de verão desde 2019) como último
        // recurso determinístico — nunca deixa o fold quebrar por causa de tzdata ausente.
        foreach (var id in new[] { "America/Sao_Paulo", "E. South America Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
                // tenta o próximo id
            }
            catch (InvalidTimeZoneException)
            {
                // tenta o próximo id
            }
        }

        return TimeZoneInfo.CreateCustomTimeZone("BRT-fixo", TimeSpan.FromHours(-3), "Horário de Brasília (fixo)", "BRT");
    }
}
