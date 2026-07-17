using SistemaX.Modules.Estoque.Domain.Comum;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Estoque.Domain.Razao;

/// <summary>
/// O RAZÃO — a entidade central do módulo. IMUTÁVEL após gravado: sem <c>update</c>, sem
/// <c>delete</c>. Corrigiu errado? Novo movimento de estorno/ajuste; nunca se edita um já gravado.
///
/// Saldo em qualquer ponto do tempo é SEMPRE derivado — <c>saldo = Σ deltas</c> (a mesma política
/// de "contador de estoque = soma de delta" usada na resolução de conflito de sync multi-terminal).
/// Isso é o que dá merge sem conflito entre dois PDVs offline e permite POSIÇÃO RETROATIVA (replay
/// do razão até uma data de corte) sem recalcular nada além do próprio razão.
///
/// O que NÃO existe aqui de propósito: <c>previousStock</c>/<c>newStock</c>. Gravar o "saldo antes/
/// depois" no próprio movimento é uma condição de corrida entre dois terminais concorrentes — os
/// dois gravam o mesmo "previous" e o razão passa a mentir. <see cref="SaldoDeItem"/> (read-model)
/// é sempre recalculável a partir daqui; divergência entre os dois é bug DO CACHE, nunca do razão.
/// </summary>
public sealed class MovimentoDeEstoque : AggregateRoot<string>
{
    public string TenantId { get; private set; } = string.Empty;
    public string DepositoId { get; private set; } = string.Empty;
    public string ProdutoId { get; private set; } = string.Empty;
    public TipoMovimento Tipo { get; private set; }
    public Quantidade Quantidade { get; private set; }

    /// <summary>Entrada: custo da nota. Saída/Perda: custo médio (ou camada FIFO) VIGENTE no
    /// instante da baixa — congela o CMV da operação, o que torna o razão auditável sem
    /// recomputar histórico.</summary>
    public Money CustoUnitario { get; private set; } = Money.Zero;

    public SourceRef Origem { get; private set; } = null!;

    /// <summary>Índice único no repositório — reprocessar o evento de origem é no-op.</summary>
    public string ChaveIdempotencia { get; private set; } = string.Empty;

    public string? LoteId { get; private set; }
    public string Motivo { get; private set; } = string.Empty;
    public string OperadorId { get; private set; } = string.Empty;
    public string OperadorNome { get; private set; } = string.Empty;

    /// <summary>Do evento de ORIGEM, não do processamento — replay não pode reordenar o razão pela
    /// hora em que o handler rodou.</summary>
    public DateTimeOffset OcorridoEm { get; private set; }

    private MovimentoDeEstoque()
    {
    }

    public static Result<MovimentoDeEstoque> Registrar(
        string tenantId,
        string depositoId,
        string produtoId,
        TipoMovimento tipo,
        Quantidade quantidade,
        Money custoUnitario,
        SourceRef origem,
        string chaveIdempotencia,
        string motivo,
        string operadorId,
        string operadorNome,
        DateTimeOffset ocorridoEm,
        string? loteId = null)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            return Result.Falhar<MovimentoDeEstoque>(new Error("estoque.movimento.tenant_obrigatorio", "TenantId é obrigatório."));

        if (string.IsNullOrWhiteSpace(produtoId))
            return Result.Falhar<MovimentoDeEstoque>(new Error("estoque.movimento.produto_obrigatorio", "ProdutoId é obrigatório."));

        if (string.IsNullOrWhiteSpace(chaveIdempotencia))
            return Result.Falhar<MovimentoDeEstoque>(new Error("estoque.movimento.chave_obrigatoria", "ChaveIdempotencia é obrigatória — todo movimento nasce de um fato rastreável."));

        // Ajuste é o ÚNICO tipo com delta assinado; os demais sempre guardam quantidade positiva
        // e o sentido vem do Tipo (ver EfeitoFisico/EfeitoReservado).
        if (tipo == TipoMovimento.Ajuste)
        {
            if (quantidade.EhZero)
                return Result.Falhar<MovimentoDeEstoque>(new Error("estoque.movimento.ajuste_sem_delta", "Ajuste com delta zero não tem efeito — não registre."));
        }
        else if (!quantidade.EhPositiva)
        {
            return Result.Falhar<MovimentoDeEstoque>(new Error("estoque.movimento.quantidade_invalida", $"Quantidade de um movimento '{tipo}' deve ser positiva — o sinal vem do tipo, não do valor."));
        }

        if (custoUnitario.EhNegativo)
            return Result.Falhar<MovimentoDeEstoque>(new Error("estoque.movimento.custo_negativo", "Custo unitário não pode ser negativo."));

        if (tipo == TipoMovimento.Perda && string.IsNullOrWhiteSpace(motivo))
            return Result.Falhar<MovimentoDeEstoque>(new Error("estoque.movimento.perda_sem_motivo", "Registrar perda exige motivo."));

        var movimento = new MovimentoDeEstoque
        {
            Id = IdGenerator.NovoId(),
            TenantId = tenantId,
            DepositoId = depositoId,
            ProdutoId = produtoId,
            Tipo = tipo,
            Quantidade = quantidade,
            CustoUnitario = custoUnitario,
            Origem = origem,
            ChaveIdempotencia = chaveIdempotencia,
            LoteId = loteId,
            Motivo = motivo,
            OperadorId = operadorId,
            OperadorNome = operadorNome,
            OcorridoEm = ocorridoEm
        };

        return Result.Ok(movimento);
    }

    /// <summary>REIDRATAÇÃO a partir do banco — não valida, não levanta evento (R6).</summary>
    public static MovimentoDeEstoque Reconstituir(
        string id, string tenantId, string depositoId, string produtoId, TipoMovimento tipo,
        Quantidade quantidade, Money custoUnitario, SourceRef origem, string chaveIdempotencia,
        string? loteId, string motivo, string operadorId, string operadorNome, DateTimeOffset ocorridoEm)
        => new()
        {
            Id = id, TenantId = tenantId, DepositoId = depositoId, ProdutoId = produtoId, Tipo = tipo,
            Quantidade = quantidade, CustoUnitario = custoUnitario, Origem = origem,
            ChaveIdempotencia = chaveIdempotencia, LoteId = loteId, Motivo = motivo,
            OperadorId = operadorId, OperadorNome = operadorNome, OcorridoEm = ocorridoEm
        };

    /// <summary>Delta a aplicar sobre <c>SaldoDeItem.Fisico</c>.</summary>
    public Quantidade EfeitoFisico => Tipo switch
    {
        TipoMovimento.Entrada => Quantidade,
        TipoMovimento.Saida => -Quantidade,
        TipoMovimento.Perda => -Quantidade,
        TipoMovimento.Ajuste => Quantidade,
        _ => Quantidade.Zero // Reserva/LiberacaoReserva não tocam o físico
    };

    /// <summary>Delta a aplicar sobre <c>SaldoDeItem.Reservado</c>.</summary>
    public Quantidade EfeitoReservado => Tipo switch
    {
        TipoMovimento.Reserva => Quantidade,
        TipoMovimento.LiberacaoReserva => -Quantidade,
        _ => Quantidade.Zero
    };
}
