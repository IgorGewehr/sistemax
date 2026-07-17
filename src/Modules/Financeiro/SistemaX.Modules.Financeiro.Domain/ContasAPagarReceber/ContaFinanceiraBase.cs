using SistemaX.Modules.Financeiro.Domain.Comum;
using SistemaX.Modules.Financeiro.Domain.Eventos;
using SistemaX.Modules.Financeiro.Domain.Fsm;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Domain.ContasAPagarReceber;

/// <summary>
/// Base comum de <c>ContaAPagar</c>/<c>ContaAReceber</c> — visão de COMPETÊNCIA
/// (docs/financeiro/financeiro-datamodel.md §2.1, §3): nasce quando o fato gerador econômico
/// ocorre (venda, NF de compra, OS faturada), independente de o dinheiro já ter mudado de mãos.
/// <see cref="DataCompetencia"/> é a data que alimenta o DRE gerencial; a visão de CAIXA
/// (<c>MovimentoFinanceiro</c>) é uma entidade separada, ligada por <see cref="Parcela.Id"/>,
/// nunca um campo aqui dentro — é isso que permite parcelamento parcial e conciliação N:1.
///
/// <see cref="SourceRef"/> é a chave de idempotência: o caso de uso consulta o repositório por
/// ela antes de chamar <c>Criar</c> — reprocessar o mesmo evento de integração não deve gerar
/// uma segunda conta.
/// </summary>
public abstract class ContaFinanceiraBase : AggregateRoot<string>
{
    private readonly List<Parcela> _parcelas = [];

    public string BusinessId { get; }
    public SourceRef SourceRef { get; }
    public string Descricao { get; }
    public string CategoriaId { get; }
    public string? CentroDeCustoId { get; }
    public DateTimeOffset DataCompetencia { get; }
    public Money ValorTotal { get; }
    public StatusFinanceiro Status { get; private set; }
    public DateTimeOffset CriadoEm { get; }

    /// <summary>
    /// Dimensão "corrente de receita" (P0-1, docs/financeiro/revisao-domain-fit-cnpj.md) — qual das
    /// três correntes do CNPJ (<see cref="CorrenteDeReceita.Recorrente"/>/
    /// <see cref="CorrenteDeReceita.Servico"/>/<see cref="CorrenteDeReceita.Comercio"/>) esta conta
    /// representa. NULLABLE — aditivo, na mesma filosofia de <c>ReceitaRecorrente</c>/
    /// <c>ReceitaOperacional</c> do <c>DreGerencialService</c>: um lançamento manual/genérico
    /// (<c>LancarContaUseCase</c>, <c>Recorrencia</c> sem sinal claro de corrente) pode legitimamente
    /// não pertencer a nenhuma das três — ele continua contando para o TOTAL do DRE mas fica de fora
    /// da quebra <c>PorCorrente</c>. Todo criador de receita/custo direto que SABE a corrente (venda,
    /// OS, pedido, assinatura, comissão) marca explicitamente — ver os handlers de evento de
    /// integração e <c>Assinatura.GerarCobranca</c>.
    /// </summary>
    public CorrenteDeReceita? Corrente { get; }

    public IReadOnlyList<Parcela> Parcelas => _parcelas.AsReadOnly();

    /// <summary>Usado pelo motor de partida dobrada para saber se a contrapartida é Receita ou Custo/Despesa.</summary>
    public abstract bool EhContaAPagar { get; }

    protected ContaFinanceiraBase(
        string id, string businessId, SourceRef sourceRef, string descricao, string categoriaId,
        string? centroDeCustoId, DateTimeOffset dataCompetencia, Money valorTotal, IReadOnlyCollection<Parcela> parcelas,
        CorrenteDeReceita? corrente = null)
    {
        Id = id;
        BusinessId = businessId;
        SourceRef = sourceRef;
        Descricao = descricao;
        CategoriaId = categoriaId;
        CentroDeCustoId = centroDeCustoId;
        DataCompetencia = dataCompetencia;
        ValorTotal = valorTotal;
        CriadoEm = DateTimeOffset.UtcNow;
        Corrente = corrente;
        _parcelas.AddRange(parcelas);
        Status = StatusFinanceiro.Aberto;
    }

    /// <summary>Construtor de REIDRATAÇÃO — usado só pelos Reconstituir() de ContaAReceber/ContaAPagar.
    /// Recebe Status e CriadoEm já persistidos em vez de sempre nascer Aberto/agora. Não valida, não
    /// levanta evento (R6 — reidratação não é fato novo).</summary>
    protected ContaFinanceiraBase(
        string id, string businessId, SourceRef sourceRef, string descricao, string categoriaId,
        string? centroDeCustoId, DateTimeOffset dataCompetencia, Money valorTotal, StatusFinanceiro status,
        DateTimeOffset criadoEm, IReadOnlyCollection<Parcela> parcelas, CorrenteDeReceita? corrente = null)
    {
        Id = id;
        BusinessId = businessId;
        SourceRef = sourceRef;
        Descricao = descricao;
        CategoriaId = categoriaId;
        CentroDeCustoId = centroDeCustoId;
        DataCompetencia = dataCompetencia;
        ValorTotal = valorTotal;
        Status = status;
        CriadoEm = criadoEm;
        Corrente = corrente;
        _parcelas.AddRange(parcelas);
    }

    protected static Result ValidarParcelas(Money valorTotal, IReadOnlyCollection<Parcela> parcelas)
    {
        if (parcelas.Count == 0)
            return Result.Falhar(new Error("financeiro.conta.sem_parcelas", "Uma conta a pagar/receber precisa de ao menos uma parcela."));

        var somaParcelas = parcelas.Aggregate(Money.Zero, (acumulado, p) => acumulado + p.Valor);
        return somaParcelas == valorTotal
            ? Result.Ok()
            : Result.Falhar(new Error(
                "financeiro.conta.parcelas_nao_batem",
                $"Soma das parcelas ({somaParcelas.Formatado()}) é diferente do valor total da conta ({valorTotal.Formatado()})."));
    }

    /// <summary>Gera uma única parcela cobrindo o valor total inteiro — caso comum de pagamento à vista.</summary>
    public static IReadOnlyCollection<Parcela> ParcelaUnica(Money valorTotal, DateTimeOffset vencimento) => [Parcela.Criar(1, vencimento, valorTotal)];

    /// <summary>
    /// Registra a liquidação (total ou parcial) de uma parcela. NÃO cria o <c>MovimentoFinanceiro</c>
    /// de caixa correspondente — isso é responsabilidade do caso de uso da camada de aplicação, que
    /// orquestra as duas escritas (competência aqui + caixa em MovimentoFinanceiro) atomicamente.
    /// </summary>
    public Result RegistrarLiquidacaoParcela(string parcelaId, Money valorPago, DateTimeOffset dataPagamento, string formaPagamentoId)
    {
        var parcela = _parcelas.FirstOrDefault(p => p.Id == parcelaId);
        if (parcela is null)
            return Result.Falhar(new Error("financeiro.conta.parcela_nao_encontrada", $"Parcela '{parcelaId}' não pertence à conta '{Id}'."));

        var resultado = parcela.RegistrarPagamento(valorPago, dataPagamento, formaPagamentoId);
        if (resultado.Falha) return resultado;

        RecalcularStatusAgregado();
        Raise(new ParcelaLiquidada(Id, parcelaId, valorPago.Centavos, dataPagamento));
        return Result.Ok();
    }

    /// <summary>
    /// Reavalia todas as parcelas em aberto contra a data de referência — idempotente por
    /// natureza (rodar 2x no mesmo dia não duplica nada, ver docs/financeiro-datamodel.md §4.3.2):
    /// a segunda rodada já encontra as parcelas em 'Atrasado' e não gera evento de novo.
    /// </summary>
    public Result AvaliarVencimento(DateTimeOffset referencia)
    {
        foreach (var parcela in _parcelas.Where(p => p.Status is StatusFinanceiro.Aberto or StatusFinanceiro.Parcial))
        {
            var statusAntes = parcela.Status;
            var resultado = parcela.MarcarAtrasada(referencia);
            if (resultado.Falha) return resultado;

            if (parcela.Status == StatusFinanceiro.Atrasado && statusAntes != StatusFinanceiro.Atrasado)
                Raise(new ParcelaMarcadaVencida(Id, parcela.Id, parcela.Valor.Centavos, EhContaAPagar));
        }

        RecalcularStatusAgregado();
        return Result.Ok();
    }

    /// <summary>
    /// Anulação pura — só permitida se NENHUMA parcela tiver pagamento registrado (nenhum
    /// MovimentoFinanceiro pode existir para uma conta cancelada). Se já houver pagamento, a
    /// correção correta é um estorno em <c>MovimentoFinanceiro</c>/<c>LancamentoContabil</c>,
    /// nunca cancelar esta conta (docs/financeiro-datamodel.md §4.4.3).
    /// </summary>
    public Result Cancelar(string motivo)
    {
        if (_parcelas.Any(p => p.TemPagamentoRegistrado))
            return Result.Falhar(new Error(
                "financeiro.conta.cancelar_com_pagamento",
                "Conta com parcela já paga não pode ser cancelada — o fato é imutável. Lance um estorno em vez disso."));

        foreach (var parcela in _parcelas)
        {
            var resultado = parcela.Cancelar();
            if (resultado.Falha) return resultado;
        }

        var transicao = StatusFinanceiroFsm.AssertirTransicao(Status, StatusFinanceiro.Cancelado);
        if (transicao.Falha) return transicao;

        Status = StatusFinanceiro.Cancelado;
        Raise(new ContaCancelada(Id, motivo));
        return Result.Ok();
    }

    /// <summary>Status da conta é sempre DERIVADO do status das parcelas — nunca transicionado manualmente.</summary>
    private void RecalcularStatusAgregado()
    {
        if (_parcelas.All(p => p.Status == StatusFinanceiro.Cancelado))
        {
            Status = StatusFinanceiro.Cancelado;
            return;
        }

        var relevantes = _parcelas.Where(p => p.Status != StatusFinanceiro.Cancelado).ToList();

        if (relevantes.Count > 0 && relevantes.All(p => p.Status == StatusFinanceiro.Pago))
        {
            Status = StatusFinanceiro.Pago;
            return;
        }

        if (relevantes.Any(p => p.Status is StatusFinanceiro.Pago or StatusFinanceiro.Parcial))
        {
            Status = StatusFinanceiro.Parcial;
            return;
        }

        Status = relevantes.Any(p => p.Status == StatusFinanceiro.Atrasado)
            ? StatusFinanceiro.Atrasado
            : StatusFinanceiro.Aberto;
    }
}
