using SistemaX.Modules.Financeiro.Domain.Comum;
using SistemaX.Modules.Financeiro.Domain.ContasAPagarReceber;
using SistemaX.Modules.Financeiro.Domain.Recorrencia;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Domain.Assinaturas;

/// <summary>
/// ASSINATURA — receita recorrente de um CLIENTE por um SERVIÇO. É a unificação de "projeto" +
/// "recorrência" que faltava no saas-erp (onde Project era só um badge morto e Recurrence ficava
/// presa numa transação). Aqui é um agregado que conhece seu MRR, sua data de início e de
/// cancelamento (o churn), e gera os recebíveis recorrentes. Existe só para negócios que vendem
/// serviço recorrente — em outros verticais a lente simplesmente não aparece.
///
/// MRR é a métrica-âncora: o valor por ciclo NORMALIZADO para mensal, para que planos anuais,
/// trimestrais e mensais somem na mesma base.
/// </summary>
public sealed class Assinatura : AggregateRoot<string>
{
    public string BusinessId { get; }
    public string ClienteId { get; }
    public string ClienteNome { get; }
    public string ServicoId { get; }
    public string ServicoNome { get; }
    public Money ValorPorCiclo { get; }
    public FrequenciaRecorrencia Ciclo { get; }
    public int DiaCobranca { get; }
    public StatusAssinatura Status { get; private set; }
    public DateTimeOffset DataInicio { get; }
    public DateTimeOffset? CanceladaEm { get; private set; }
    public string? MotivoCancelamento { get; private set; }
    public DateTimeOffset? UltimaCobrancaGeradaEm { get; private set; }

    /// <summary>MRR desta assinatura — <see cref="ValorPorCiclo"/> normalizado para mensal.</summary>
    public Money Mrr => NormalizarParaMensal(ValorPorCiclo, Ciclo);

    private Assinatura(
        string id, string businessId, string clienteId, string clienteNome, string servicoId,
        string servicoNome, Money valorPorCiclo, FrequenciaRecorrencia ciclo, int diaCobranca, DateTimeOffset dataInicio)
    {
        Id = id;
        BusinessId = businessId;
        ClienteId = clienteId;
        ClienteNome = clienteNome;
        ServicoId = servicoId;
        ServicoNome = servicoNome;
        ValorPorCiclo = valorPorCiclo;
        Ciclo = ciclo;
        DiaCobranca = diaCobranca;
        DataInicio = dataInicio;
        Status = StatusAssinatura.Ativa;
    }

    public static Result<Assinatura> Criar(
        string businessId, string clienteId, string clienteNome, string servicoId, string servicoNome,
        Money valorPorCiclo, FrequenciaRecorrencia ciclo, int diaCobranca, DateTimeOffset dataInicio)
    {
        if (!valorPorCiclo.EhPositivo)
            return Result.Falhar<Assinatura>(new Error("financeiro.assinatura.valor_invalido", "Valor por ciclo deve ser positivo."));
        if (diaCobranca is < 1 or > 31)
            return Result.Falhar<Assinatura>(new Error("financeiro.assinatura.dia_cobranca_invalido", "Dia de cobrança deve estar entre 1 e 31."));
        if (string.IsNullOrWhiteSpace(clienteId) || string.IsNullOrWhiteSpace(servicoId))
            return Result.Falhar<Assinatura>(new Error("financeiro.assinatura.vinculo_invalido", "Assinatura exige cliente e serviço."));

        var assinatura = new Assinatura(
            IdGenerator.NovoId(), businessId, clienteId, clienteNome, servicoId, servicoNome, valorPorCiclo, ciclo, diaCobranca, dataInicio);
        assinatura.Raise(new AssinaturaCriada(assinatura.Id, businessId, servicoId, assinatura.Mrr.Centavos, dataInicio));
        return Result.Ok(assinatura);
    }

    /// <summary>
    /// REIDRATAÇÃO a partir do banco — usada só pela camada de persistência (repositório).
    /// Não valida nem levanta evento: reconstrói o estado exato que foi persistido.
    /// </summary>
    public static Assinatura Reconstituir(
        string id, string businessId, string clienteId, string clienteNome, string servicoId, string servicoNome,
        Money valorPorCiclo, FrequenciaRecorrencia ciclo, int diaCobranca, StatusAssinatura status,
        DateTimeOffset dataInicio, DateTimeOffset? canceladaEm, string? motivoCancelamento, DateTimeOffset? ultimaCobrancaGeradaEm)
    {
        var a = new Assinatura(id, businessId, clienteId, clienteNome, servicoId, servicoNome, valorPorCiclo, ciclo, diaCobranca, dataInicio);
        a.Status = status;
        a.CanceladaEm = canceladaEm;
        a.MotivoCancelamento = motivoCancelamento;
        a.UltimaCobrancaGeradaEm = ultimaCobrancaGeradaEm;
        return a;
    }

    public Result Pausar()
    {
        if (Status != StatusAssinatura.Ativa)
            return Result.Falhar(new Error("financeiro.assinatura.pausar_invalido", $"Só uma assinatura ativa pode ser pausada (atual: {Status})."));
        Status = StatusAssinatura.Pausada;
        Raise(new AssinaturaPausada(Id));
        return Result.Ok();
    }

    public Result Reativar()
    {
        if (Status != StatusAssinatura.Pausada)
            return Result.Falhar(new Error("financeiro.assinatura.reativar_invalido", $"Só uma assinatura pausada pode ser reativada (atual: {Status})."));
        Status = StatusAssinatura.Ativa;
        Raise(new AssinaturaReativada(Id));
        return Result.Ok();
    }

    public Result Cancelar(string motivo, DateTimeOffset quando)
    {
        if (Status == StatusAssinatura.Cancelada)
            return Result.Falhar(new Error("financeiro.assinatura.ja_cancelada", "Assinatura já está cancelada."));
        Status = StatusAssinatura.Cancelada;
        CanceladaEm = quando;
        MotivoCancelamento = motivo;
        Raise(new AssinaturaCancelada(Id, BusinessId, ServicoId, Mrr.Centavos, quando, motivo));
        return Result.Ok();
    }

    /// <summary>
    /// Próxima COMPETÊNCIA (mês) em que esta assinatura deve ser cobrada, a partir de
    /// <see cref="UltimaCobrancaGeradaEm"/> (ou <see cref="DataInicio"/>, se nunca cobrou) + um
    /// <see cref="Ciclo"/> — o MESMO algoritmo que <c>AssinaturaDetalheService</c> usa pra projetar
    /// a "próxima cobrança" na UI (P0-3: um lar só pra essa regra, nunca duplicada). Granularidade
    /// de MÊS (não de dia): o dia exato do vencimento é <see cref="DiaCobranca"/>, resolvido só na
    /// hora de criar a <see cref="ContaAReceber"/> (<see cref="GerarCobranca"/>) — comparar por mês
    /// aqui é o que permite um catch-up disparado no dia 1 já reconhecer a competência do dia 5
    /// como devida.
    /// </summary>
    public DateTimeOffset ProximaCompetenciaDevida
        => InicioDoMes(AdicionarCiclo(UltimaCobrancaGeradaEm ?? DataInicio, Ciclo));

    /// <summary>
    /// Motor de recorrência: gera a <see cref="ContaAReceber"/> da assinatura para a competência
    /// informada — SÓ SE o ciclo já venceu (<see cref="ProximaCompetenciaDevida"/> — mensal cobra
    /// todo mês, trimestral a cada 3 meses, anual 1×/ano; nunca o valor cheio todo mês para ciclo
    /// não-mensal, o bug do P0-3). Idempotente por período — <see cref="SourceRef"/> determinística
    /// <c>assinatura:{id}:{yyyyMM}</c>, então rodar o gerador 2× no mesmo mês não duplica cobrança
    /// (a Application, além disso, confere <c>BuscarPorOrigemAsync</c> antes de persistir — dupla
    /// rede de segurança). Catch-up de vários ciclos atrasados é responsabilidade do CHAMADOR (loop
    /// enquanto <see cref="ProximaCompetenciaDevida"/> continuar <c>&lt;=</c> a data-limite),
    /// exatamente como <c>GerarContasRecorrentesUseCase</c> já faz para <c>Recorrencia</c>.
    /// A gravação (conta + lançamento contábil) é responsabilidade do caso de uso na Application.
    /// </summary>
    public Result<ContaAReceber> GerarCobranca(DateTimeOffset competencia, string categoriaId)
    {
        if (Status != StatusAssinatura.Ativa)
            return Result.Falhar<ContaAReceber>(new Error("financeiro.assinatura.nao_ativa", "Só assinatura ativa gera cobrança."));

        var devida = ProximaCompetenciaDevida;
        if (InicioDoMes(competencia) < devida)
            return Result.Falhar<ContaAReceber>(new Error(
                "financeiro.assinatura.ciclo_nao_vencido",
                $"Ciclo ({Ciclo}) ainda não venceu — próxima cobrança devida em {devida:yyyy-MM}."));

        var vencimento = AjustarDiaCobranca(competencia, DiaCobranca);
        var periodo = $"{competencia:yyyyMM}";
        var origem = new SourceRef("assinatura", $"{Id}:{periodo}");
        var parcelas = ContaFinanceiraBase.ParcelaUnica(ValorPorCiclo, vencimento);

        // Corrente é hardcoded (não parametrizada pelo chamador): TODA cobrança nascida de uma
        // Assinatura é, por definição de domínio, receita da corrente Recorrente (P0-1) — nunca
        // deveria ser possível marcar diferente por engano do caller.
        var conta = ContaAReceber.Criar(
            BusinessId, origem, $"{ServicoNome} — {ClienteNome}", categoriaId, competencia, ValorPorCiclo, parcelas,
            null, ClienteId, CorrenteDeReceita.Recorrente);
        if (conta.Sucesso)
            UltimaCobrancaGeradaEm = competencia;
        return conta;
    }

    private static Money NormalizarParaMensal(Money valor, FrequenciaRecorrencia ciclo)
    {
        decimal fatorMensal = ciclo switch
        {
            FrequenciaRecorrencia.Semanal => 52m / 12m,
            FrequenciaRecorrencia.Mensal => 1m,
            FrequenciaRecorrencia.Bimestral => 1m / 2m,
            FrequenciaRecorrencia.Trimestral => 1m / 3m,
            FrequenciaRecorrencia.Semestral => 1m / 6m,
            FrequenciaRecorrencia.Anual => 1m / 12m,
            _ => 1m
        };
        return new Money((long)Math.Round(valor.Centavos * fatorMensal, MidpointRounding.ToEven), valor.Moeda);
    }

    /// <summary>ÚNICO lar de "somar um ciclo a uma data" — reusado por <see cref="GerarCobranca"/>
    /// e por <c>AssinaturaDetalheService</c> (projeção de UI), que antes duplicava esta mesma
    /// lógica verbatim (P0-3: unificar a regra divergente).</summary>
    public static DateTimeOffset AdicionarCiclo(DateTimeOffset data, FrequenciaRecorrencia ciclo) => ciclo switch
    {
        FrequenciaRecorrencia.Semanal => data.AddDays(7),
        FrequenciaRecorrencia.Mensal => data.AddMonths(1),
        FrequenciaRecorrencia.Bimestral => data.AddMonths(2),
        FrequenciaRecorrencia.Trimestral => data.AddMonths(3),
        FrequenciaRecorrencia.Semestral => data.AddMonths(6),
        FrequenciaRecorrencia.Anual => data.AddYears(1),
        _ => data.AddMonths(1)
    };

    /// <summary>ÚNICO lar de "ajustar pro dia de cobrança, com clamp no fim do mês" — mesmo
    /// racional de <see cref="AdicionarCiclo"/> acima.</summary>
    public static DateTimeOffset AjustarDiaCobranca(DateTimeOffset data, int dia)
    {
        var diaValido = Math.Min(dia, DateTime.DaysInMonth(data.Year, data.Month));
        return new DateTimeOffset(data.Year, data.Month, diaValido, 0, 0, 0, data.Offset);
    }

    private static DateTimeOffset InicioDoMes(DateTimeOffset data) => new(data.Year, data.Month, 1, 0, 0, 0, data.Offset);
}
