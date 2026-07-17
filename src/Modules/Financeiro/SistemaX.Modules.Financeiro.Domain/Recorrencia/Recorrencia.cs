using SistemaX.Modules.Financeiro.Domain.Comum;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Domain.Recorrencia;

public enum FrequenciaRecorrencia
{
    Semanal,
    Mensal,
    Bimestral,
    Trimestral,
    Semestral,
    Anual
}

public enum TipoContaRecorrente
{
    APagar,
    AReceber
}

/// <summary>
/// Template gerador de <c>ContaAPagar</c>/<c>ContaAReceber</c> futuras (aluguel, salário,
/// assinatura). Escopo do MVP (docs/financeiro-features.md §4.12 — "não propor redesenho, já é
/// a parte mais madura"): frequência + dia fixo + data fim. Multa/juros pró-rata e ajuste de dia
/// útil ficam para Fase 2 — deixados como TODO explícito, não implementados aqui.
/// </summary>
public sealed class Recorrencia : AggregateRoot<string>
{
    public string BusinessId { get; }
    public string Descricao { get; }
    public TipoContaRecorrente Tipo { get; }
    public Money ValorPrevisto { get; }
    public string CategoriaId { get; }
    public int? DiaFixo { get; }
    public FrequenciaRecorrencia Frequencia { get; }
    public DateTimeOffset DataInicio { get; }
    public DateTimeOffset? DataFim { get; }
    public bool Ativa { get; private set; }
    public DateTimeOffset? UltimaGeracaoEm { get; private set; }

    private Recorrencia(
        string id, string businessId, string descricao, TipoContaRecorrente tipo, Money valorPrevisto,
        string categoriaId, int? diaFixo, FrequenciaRecorrencia frequencia, DateTimeOffset dataInicio, DateTimeOffset? dataFim)
    {
        Id = id;
        BusinessId = businessId;
        Descricao = descricao;
        Tipo = tipo;
        ValorPrevisto = valorPrevisto;
        CategoriaId = categoriaId;
        DiaFixo = diaFixo;
        Frequencia = frequencia;
        DataInicio = dataInicio;
        DataFim = dataFim;
        Ativa = true;
    }

    public static Result<Recorrencia> Criar(
        string businessId, string descricao, TipoContaRecorrente tipo, Money valorPrevisto,
        string categoriaId, FrequenciaRecorrencia frequencia, DateTimeOffset dataInicio,
        int? diaFixo = null, DateTimeOffset? dataFim = null)
    {
        if (!valorPrevisto.EhPositivo)
            return Result.Falhar<Recorrencia>(new Error("financeiro.recorrencia.valor_invalido", "Valor previsto da recorrência deve ser positivo."));

        if (diaFixo is < 1 or > 31)
            return Result.Falhar<Recorrencia>(new Error("financeiro.recorrencia.dia_fixo_invalido", "Dia fixo deve estar entre 1 e 31."));

        if (dataFim is { } fim && fim <= dataInicio)
            return Result.Falhar<Recorrencia>(new Error("financeiro.recorrencia.data_fim_invalida", "Data fim deve ser posterior à data de início."));

        return Result.Ok(new Recorrencia(IdGenerator.NovoId(), businessId, descricao, tipo, valorPrevisto, categoriaId, diaFixo, frequencia, dataInicio, dataFim));
    }

    /// <summary>REIDRATAÇÃO a partir do banco — não valida, não levanta evento.</summary>
    public static Recorrencia Reconstituir(
        string id, string businessId, string descricao, TipoContaRecorrente tipo, Money valorPrevisto,
        string categoriaId, int? diaFixo, FrequenciaRecorrencia frequencia, DateTimeOffset dataInicio,
        DateTimeOffset? dataFim, bool ativa, DateTimeOffset? ultimaGeracaoEm)
    {
        var recorrencia = new Recorrencia(id, businessId, descricao, tipo, valorPrevisto, categoriaId, diaFixo, frequencia, dataInicio, dataFim);
        recorrencia.Ativa = ativa;
        recorrencia.UltimaGeracaoEm = ultimaGeracaoEm;
        return recorrencia;
    }

    /// <summary>Calcula a próxima data de geração a partir da última geração (ou da data de início, se nunca gerou).</summary>
    public Result<DateTimeOffset> CalcularProximaOcorrencia()
    {
        if (!Ativa)
            return Result.Falhar<DateTimeOffset>(new Error("financeiro.recorrencia.inativa", "Recorrência inativa não gera novas contas."));

        var referencia = UltimaGeracaoEm ?? DataInicio;
        var proxima = Frequencia switch
        {
            FrequenciaRecorrencia.Semanal => referencia.AddDays(7),
            FrequenciaRecorrencia.Mensal => referencia.AddMonths(1),
            FrequenciaRecorrencia.Bimestral => referencia.AddMonths(2),
            FrequenciaRecorrencia.Trimestral => referencia.AddMonths(3),
            FrequenciaRecorrencia.Semestral => referencia.AddMonths(6),
            FrequenciaRecorrencia.Anual => referencia.AddYears(1),
            _ => throw new ArgumentOutOfRangeException(nameof(Frequencia), Frequencia, "Frequência de recorrência desconhecida.")
        };

        if (DiaFixo is { } dia) proxima = AjustarParaDiaFixo(proxima, dia);

        if (DataFim is { } fim && proxima > fim)
            return Result.Falhar<DateTimeOffset>(new Error("financeiro.recorrencia.encerrada", "Próxima ocorrência ultrapassa a data fim da recorrência."));

        return Result.Ok(proxima);
    }

    public void RegistrarGeracao(DateTimeOffset dataGerada) => UltimaGeracaoEm = dataGerada;

    public void Desativar() => Ativa = false;

    private static DateTimeOffset AjustarParaDiaFixo(DateTimeOffset data, int diaFixo)
    {
        var diaValido = Math.Min(diaFixo, DateTime.DaysInMonth(data.Year, data.Month));
        return new DateTimeOffset(data.Year, data.Month, diaValido, data.Hour, data.Minute, data.Second, data.Offset);
    }
}
