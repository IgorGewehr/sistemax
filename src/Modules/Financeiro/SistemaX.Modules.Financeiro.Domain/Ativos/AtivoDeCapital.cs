using SistemaX.Modules.Financeiro.Domain.Comum;
using SistemaX.Modules.Financeiro.Domain.Fsm;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Domain.Ativos;

/// <summary>
/// VALORES PINADOS (persistidos como INTEGER — nunca reordenar). <c>Intangivel = 0</c> é o
/// comportamento herdado do design-pai (docs/financeiro/design-analise-por-projeto.md §3.3, caso
/// DigiSat — licença) antes da generalização (docs/financeiro/design-imobilizado-roi.md §3.1).
/// </summary>
public enum NaturezaAtivo
{
    Intangivel = 0,
    Tangivel = 1
}

/// <summary>VALORES PINADOS — persistidos como INTEGER; nunca reordenar
/// (docs/financeiro/design-imobilizado-roi.md §3.1).</summary>
public enum CategoriaAtivo
{
    Equipamento = 0,
    Moveis = 1,
    ComunicacaoVisual = 2,
    Reforma = 3,
    Computador = 4,
    Veiculo = 5,
    LicencaSoftware = 6,
    Outro = 99
}

/// <summary>VALORES PINADOS — método do cronograma de reconhecimento. Só <c>Linear</c> hoje
/// (Hamilton via <c>Application.Quant.CronogramaLinear</c>) — extensível para saldo-decrescente
/// sem quebrar dado existente.</summary>
public enum MetodoDeCronograma
{
    Linear = 0
}

/// <summary>
/// VALORES PINADOS — persistidos como INTEGER; nunca reordenar. <see cref="Vendido"/> é a
/// alienação do bem (fatia I4 do design de Imobilizado, §3.2/§4.6) — <see cref="AtivoDeCapital.Baixar"/>
/// transiciona para ele quando chamado com <c>valorVenda</c> não nulo.
/// </summary>
public enum StatusAtivoDeCapital
{
    EmUso = 0,
    Encerrado = 1,
    Baixado = 2,
    Vendido = 3
}

/// <summary>
/// ATIVO DE CAPITAL — o agregado GERAL de custo diferido/depreciável (docs/financeiro/
/// design-imobilizado-roi.md §3.1): generalização do <c>AtivoAmortizavel</c> desenhado em
/// docs/financeiro/design-analise-por-projeto.md §3.3 para o caso DigiSat (licença intangível
/// tageada por projeto), ANTES de nascer, para que a fatia de Imobilizado (equipamentos/reforma/
/// móveis — <see cref="NaturezaAtivo.Tangivel"/>, sem projeto) reuse o MESMO agregado, MESMA FSM,
/// MESMO cron idempotente e MESMO <c>Application.Quant.CronogramaLinear</c> — nunca duas máquinas
/// paralelas para "depreciação" (bem tangível) e "amortização" (licença/intangível), que são a
/// MESMA mecânica contábil com nome diferente por natureza do bem.
///
/// Caixa ≠ competência (design-pai §4.1): <see cref="ContaAPagarId"/> é o trilho de CAIXA (parcelas
/// da compra, ex.: 7×R$985 do DigiSat); o cronograma de <see cref="ReconhecerCompetencia"/> é o
/// trilho de COMPETÊNCIA (36×R$191,53) — o DRE/painel leem o cronograma, nunca a conta a pagar.
///
/// O cronograma em si (Hamilton, Σ = <see cref="CustoAquisicao"/> − <see cref="ValorResidual"/>)
/// NÃO é recomputado aqui — vive em <c>Application.Quant.CronogramaLinear</c> (Domain não referencia
/// Application, ver grafo de projeto da solução). Este agregado só possui o CURSOR
/// (<see cref="UltimaCompetenciaReconhecida"/>) e a FSM; o valor de cada competência é calculado
/// pela Application e passado para <see cref="ReconhecerCompetencia"/> — mesmo padrão de
/// <c>Assinatura.GerarCobranca</c> não conhecer <c>ReceitaReconhecidaResolver</c>.
/// </summary>
public sealed class AtivoDeCapital : AggregateRoot<string>
{
    public string BusinessId { get; }

    /// <summary>Dimensão "Projeto" (docs/financeiro/design-analise-por-projeto.md §3.3) — nullable:
    /// um ativo sem projeto é legal e comum (reforma da loja, equipamento genérico da Imobilizado).</summary>
    public string? ProjetoId { get; }

    public string Nome { get; }
    public NaturezaAtivo Natureza { get; }
    public CategoriaAtivo Categoria { get; }
    public Money CustoAquisicao { get; }
    public Money ValorResidual { get; }
    public DateOnly DataAquisicao { get; }
    public DateOnly InicioDepreciacao { get; }
    public int VidaUtilMeses { get; }
    public MetodoDeCronograma Metodo { get; }

    /// <summary>Capacidade (design-pai §3.3/§9.6, Decisão D2) — só faz sentido para licença
    /// (ex.: 5 licenças DigiSat numa única compra); default 1 para bem tangível comum.</summary>
    public int QuantidadeUnidades { get; }

    /// <summary>Link ao trilho de CAIXA — a <c>ContaAPagar</c> parcelada da compra, categoria
    /// <c>ativo-de-capital</c> (§4.4). <c>null</c> = investimento pago fora do sistema (só a
    /// competência entra no cronograma). Vinculável DEPOIS de <see cref="Criar"/> via
    /// <see cref="VincularContaAPagar"/> — o gesto único "Registrar investimento" cria o ativo E a
    /// conta a pagar no mesmo caso de uso, mas a conta só existe depois de o ativo já ter um Id
    /// (a <c>SourceRef</c> da conta usa o Id do ativo).</summary>
    public string? ContaAPagarId { get; private set; }

    public StatusAtivoDeCapital Status { get; private set; }

    /// <summary>Cursor do cron — espelho de <c>Assinatura.UltimaCobrancaGeradaEm</c>. <c>null</c> =
    /// nenhuma competência reconhecida ainda.</summary>
    public DateTimeOffset? UltimaCompetenciaReconhecida { get; private set; }

    public DateTimeOffset? EncerradoEm { get; private set; }
    public DateTimeOffset? BaixadoEm { get; private set; }
    public string? MotivoBaixa { get; private set; }

    /// <summary>Valor CONTÁBIL (resíduo inclusive) no instante da saída — mesmo insumo para
    /// <see cref="StatusAtivoDeCapital.Baixado"/> (write-off) e <see cref="StatusAtivoDeCapital.Vendido"/>
    /// (venda), mas com leitura diferente em <c>Application.Ativos.AtivoDeCapitalQuant.SomaNaJanela</c>:
    /// no write-off SUBSTITUI a fatia linear daquele mês (perda real, §4.5); na venda é só o insumo
    /// de <see cref="ResultadoAlienacaoCentavos"/> — o D&A daquele mês continua a fatia linear normal
    /// (§4.6, DI6: resultado da venda é linha fora do resultado operacional). <c>null</c> fora de
    /// <see cref="StatusAtivoDeCapital.Baixado"/>/<see cref="StatusAtivoDeCapital.Vendido"/>.</summary>
    public long? ValorReconhecidoNaBaixaCentavos { get; private set; }

    /// <summary>Preço de venda do bem (fatia I4, §4.6) — preenchido só na transição para
    /// <see cref="StatusAtivoDeCapital.Vendido"/> via <see cref="Baixar"/>.</summary>
    public Money? ValorVenda { get; private set; }

    /// <summary>Ganho (positivo) ou perda (negativo) na alienação — <c>ValorVenda − ValorContábil(T)</c>
    /// (§4.6, DI6): informativo, calculado on-the-fly a partir de <see cref="ValorVenda"/> e
    /// <see cref="ValorReconhecidoNaBaixaCentavos"/> (o valor contábil capturado no instante da
    /// venda, ANTES da baixa) — nunca persistido separadamente. <c>null</c> fora de
    /// <see cref="StatusAtivoDeCapital.Vendido"/>.</summary>
    public long? ResultadoAlienacaoCentavos
        => Status == StatusAtivoDeCapital.Vendido && ValorVenda is { } venda && ValorReconhecidoNaBaixaCentavos is { } valorContabil
            ? venda.Centavos - valorContabil
            : null;

    public DateTimeOffset CriadoEm { get; }

    /// <summary>Base depreciável/amortizável — o total que o cronograma (Hamilton) espalha pelas
    /// competências: <see cref="CustoAquisicao"/> − <see cref="ValorResidual"/> (§4.2 dos dois
    /// designs).</summary>
    public Money BaseDepreciavel => CustoAquisicao - ValorResidual;

    /// <summary>Próxima competência (mês) devida de reconhecimento — a partir de
    /// <see cref="UltimaCompetenciaReconhecida"/> (ou <see cref="InicioDepreciacao"/>, se nunca
    /// reconheceu nenhuma) — mesmo racional de <c>Assinatura.ProximaCompetenciaDevida</c>.</summary>
    public DateOnly ProximaCompetenciaDevida
    {
        get
        {
            if (UltimaCompetenciaReconhecida is { } ultima)
            {
                return new DateOnly(ultima.Year, ultima.Month, 1).AddMonths(1);
            }
            return new DateOnly(InicioDepreciacao.Year, InicioDepreciacao.Month, 1);
        }
    }

    private AtivoDeCapital(
        string id, string businessId, string? projetoId, string nome, NaturezaAtivo natureza, CategoriaAtivo categoria,
        Money custoAquisicao, Money valorResidual, DateOnly dataAquisicao, DateOnly inicioDepreciacao, int vidaUtilMeses,
        MetodoDeCronograma metodo, int quantidadeUnidades, string? contaAPagarId, DateTimeOffset criadoEm)
    {
        Id = id;
        BusinessId = businessId;
        ProjetoId = projetoId;
        Nome = nome;
        Natureza = natureza;
        Categoria = categoria;
        CustoAquisicao = custoAquisicao;
        ValorResidual = valorResidual;
        DataAquisicao = dataAquisicao;
        InicioDepreciacao = inicioDepreciacao;
        VidaUtilMeses = vidaUtilMeses;
        Metodo = metodo;
        QuantidadeUnidades = quantidadeUnidades;
        ContaAPagarId = contaAPagarId;
        CriadoEm = criadoEm;
        Status = StatusAtivoDeCapital.EmUso;
    }

    public static Result<AtivoDeCapital> Criar(
        string businessId, string nome, NaturezaAtivo natureza, CategoriaAtivo categoria,
        Money custoAquisicao, Money valorResidual, DateOnly dataAquisicao, DateOnly inicioDepreciacao,
        int vidaUtilMeses, DateTimeOffset criadoEm, int quantidadeUnidades = 1, string? projetoId = null,
        string? contaAPagarId = null, MetodoDeCronograma metodo = MetodoDeCronograma.Linear)
    {
        if (string.IsNullOrWhiteSpace(businessId))
            return Result.Falhar<AtivoDeCapital>(new Error("financeiro.ativo.business_obrigatorio", "BusinessId é obrigatório."));

        if (string.IsNullOrWhiteSpace(nome))
            return Result.Falhar<AtivoDeCapital>(new Error("financeiro.ativo.nome_obrigatorio", "Nome do ativo é obrigatório."));

        if (!custoAquisicao.EhPositivo)
            return Result.Falhar<AtivoDeCapital>(new Error("financeiro.ativo.custo_invalido", "Custo de aquisição deve ser positivo."));

        if (valorResidual.EhNegativo)
            return Result.Falhar<AtivoDeCapital>(new Error("financeiro.ativo.residual_invalido", "Valor residual não pode ser negativo."));

        if (valorResidual.Centavos >= custoAquisicao.Centavos)
            return Result.Falhar<AtivoDeCapital>(new Error("financeiro.ativo.residual_maior_que_custo", "Valor residual deve ser menor que o custo de aquisição."));

        if (vidaUtilMeses < 1)
            return Result.Falhar<AtivoDeCapital>(new Error("financeiro.ativo.vida_util_invalida", "Vida útil deve ser de ao menos 1 mês."));

        if (quantidadeUnidades < 1)
            return Result.Falhar<AtivoDeCapital>(new Error("financeiro.ativo.quantidade_invalida", "Quantidade de unidades deve ser ao menos 1."));

        var primeiraCompetenciaPossivel = new DateOnly(dataAquisicao.Year, dataAquisicao.Month, 1);
        var inicioNormalizado = new DateOnly(inicioDepreciacao.Year, inicioDepreciacao.Month, 1);
        if (inicioNormalizado < primeiraCompetenciaPossivel)
            return Result.Falhar<AtivoDeCapital>(new Error(
                "financeiro.ativo.inicio_antes_da_aquisicao",
                "Início da depreciação/amortização não pode ser antes do mês de aquisição."));

        var ativo = new AtivoDeCapital(
            IdGenerator.NovoId(), businessId, projetoId, nome.Trim(), natureza, categoria, custoAquisicao, valorResidual,
            dataAquisicao, inicioNormalizado, vidaUtilMeses, metodo, quantidadeUnidades, contaAPagarId, criadoEm);

        ativo.Raise(new AtivoDeCapitalCriado(ativo.Id, businessId, projetoId, natureza, custoAquisicao.Centavos, criadoEm));
        return Result.Ok(ativo);
    }

    /// <summary>REIDRATAÇÃO a partir do banco — não valida, não levanta evento.</summary>
    public static AtivoDeCapital Reconstituir(
        string id, string businessId, string? projetoId, string nome, NaturezaAtivo natureza, CategoriaAtivo categoria,
        Money custoAquisicao, Money valorResidual, DateOnly dataAquisicao, DateOnly inicioDepreciacao, int vidaUtilMeses,
        MetodoDeCronograma metodo, int quantidadeUnidades, string? contaAPagarId, StatusAtivoDeCapital status,
        DateTimeOffset? ultimaCompetenciaReconhecida, DateTimeOffset? encerradoEm, DateTimeOffset? baixadoEm,
        string? motivoBaixa, long? valorReconhecidoNaBaixaCentavos, Money? valorVenda, DateTimeOffset criadoEm)
    {
        var ativo = new AtivoDeCapital(
            id, businessId, projetoId, nome, natureza, categoria, custoAquisicao, valorResidual, dataAquisicao,
            inicioDepreciacao, vidaUtilMeses, metodo, quantidadeUnidades, contaAPagarId, criadoEm)
        {
            Status = status,
            UltimaCompetenciaReconhecida = ultimaCompetenciaReconhecida,
            EncerradoEm = encerradoEm,
            BaixadoEm = baixadoEm,
            MotivoBaixa = motivoBaixa,
            ValorReconhecidoNaBaixaCentavos = valorReconhecidoNaBaixaCentavos,
            ValorVenda = valorVenda
        };
        return ativo;
    }

    /// <summary>Vincula a <c>ContaAPagar</c> do investimento DEPOIS de o ativo já existir (o gesto
    /// único "Registrar investimento" — <c>Application.Ativos.CriarAtivoDeCapitalUseCase</c>).
    /// Classificação de rastro, não fato contábil — sem FSM, sem evento (o rastro já nasce completo
    /// no mesmo request, nunca visível "a meio caminho").</summary>
    public void VincularContaAPagar(string contaAPagarId) => ContaAPagarId = contaAPagarId;

    /// <summary>
    /// Reconhece a competência <paramref name="competencia"/> (deve ser EXATAMENTE
    /// <see cref="ProximaCompetenciaDevida"/> — o chamador, igual a
    /// <c>GerarCobrancasAssinaturasUseCase</c>, itera uma competência por vez). O VALOR
    /// (<paramref name="valorCentavos"/>) é calculado pela Application via
    /// <c>CronogramaLinear.Gerar</c> sobre <see cref="BaseDepreciavel"/> — este método só valida o
    /// cursor, avança e transiciona a FSM automaticamente para <see cref="StatusAtivoDeCapital.Encerrado"/>
    /// quando a ÚLTIMA competência da vida útil é reconhecida (índice determinístico, sem precisar
    /// do cronograma completo).
    /// </summary>
    public Result ReconhecerCompetencia(DateOnly competencia, long valorCentavos, DateTimeOffset quando)
    {
        if (Status != StatusAtivoDeCapital.EmUso)
            return Result.Falhar(new Error("financeiro.ativo.nao_em_uso", $"Só um ativo em uso reconhece nova competência (atual: {Status})."));

        var devida = ProximaCompetenciaDevida;
        if (competencia != devida)
            return Result.Falhar(new Error(
                "financeiro.ativo.competencia_invalida",
                $"Competência informada ({competencia:yyyy-MM}) não é a próxima devida ({devida:yyyy-MM})."));

        UltimaCompetenciaReconhecida = new DateTimeOffset(competencia.Year, competencia.Month, 1, 0, 0, 0, TimeSpan.Zero);
        Raise(new AmortizacaoReconhecida(Id, BusinessId, ProjetoId, $"{competencia:yyyy-MM}", valorCentavos, quando));

        var indice = (competencia.Year - InicioDepreciacao.Year) * 12 + (competencia.Month - InicioDepreciacao.Month);
        if (indice >= VidaUtilMeses - 1)
        {
            var transicao = StatusAtivoDeCapitalFsm.AssertirTransicao(Status, StatusAtivoDeCapital.Encerrado);
            if (transicao.Falha) return transicao;

            Status = StatusAtivoDeCapital.Encerrado;
            EncerradoEm = quando;
            Raise(new AtivoDeCapitalEncerrado(Id, BusinessId, quando));
        }

        return Result.Ok();
    }

    /// <summary>
    /// Baixa antecipada (impairment/write-off — design-pai §4.6, generalizado em
    /// docs/financeiro/design-imobilizado-roi.md §4.5) OU alienação (venda — fatia I4, §4.6):
    /// reconhece de uma vez, na <paramref name="competencia"/> informada, o VALOR CONTÁBIL
    /// restante (<paramref name="valorContabilCentavos"/> — calculado pela Application como
    /// <c>CustoAquisicao − Σ competências já reconhecidas</c>, que inclui o residual nunca
    /// escalonado no cronograma) — o mesmo insumo nos dois casos, só o destino da FSM muda:
    /// <paramref name="valorVenda"/> <c>null</c> → <see cref="StatusAtivoDeCapital.Baixado"/>
    /// (write-off, perda real de capacidade, permanece DENTRO do D&A —
    /// <c>Application.Ativos.AtivoDeCapitalQuant.SomaNaJanela</c>); não-nulo →
    /// <see cref="StatusAtivoDeCapital.Vendido"/> (o valor contábil some do D&A — o resultado da
    /// venda vira linha informativa FORA do resultado operacional, DI6). Nenhuma competência
    /// posterior à baixa/venda reconhece mais nada (invariante de teste: "baixa antecipada
    /// reconhece o resto exato"). Permitida a partir de <see cref="StatusAtivoDeCapital.EmUso"/> OU
    /// <see cref="StatusAtivoDeCapital.Encerrado"/> (um bem 100% depreciado ainda pode ser
    /// baixado/vendido).
    /// </summary>
    public Result Baixar(string motivo, DateOnly competencia, long valorContabilCentavos, DateTimeOffset quando, Money? valorVenda = null)
    {
        if (string.IsNullOrWhiteSpace(motivo))
            return Result.Falhar(new Error("financeiro.ativo.motivo_obrigatorio", "Motivo da baixa é obrigatório."));

        var competenciaNormalizada = new DateOnly(competencia.Year, competencia.Month, 1);
        if (UltimaCompetenciaReconhecida is { } ultima && competenciaNormalizada < new DateOnly(ultima.Year, ultima.Month, 1))
            return Result.Falhar(new Error(
                "financeiro.ativo.baixa_competencia_invalida",
                "Competência da baixa não pode ser anterior à última competência já reconhecida."));

        var statusDestino = valorVenda is null ? StatusAtivoDeCapital.Baixado : StatusAtivoDeCapital.Vendido;
        var transicao = StatusAtivoDeCapitalFsm.AssertirTransicao(Status, statusDestino);
        if (transicao.Falha) return transicao;

        Status = statusDestino;
        BaixadoEm = quando;
        MotivoBaixa = motivo;
        ValorReconhecidoNaBaixaCentavos = valorContabilCentavos;
        ValorVenda = valorVenda;
        UltimaCompetenciaReconhecida = new DateTimeOffset(competenciaNormalizada.Year, competenciaNormalizada.Month, 1, 0, 0, 0, TimeSpan.Zero);

        if (valorVenda is { } venda)
            Raise(new AtivoDeCapitalVendido(Id, BusinessId, ProjetoId, valorContabilCentavos, venda.Centavos, quando));
        else
            Raise(new AtivoDeCapitalBaixadoAntecipadamente(Id, BusinessId, ProjetoId, valorContabilCentavos, quando));

        return Result.Ok();
    }
}
