using SistemaX.Modules.Financeiro.Domain.Comum;
using SistemaX.Modules.Financeiro.Domain.Fsm;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Domain.Caixa;

/// <summary>
/// Agregado raiz do RITUAL de caixa físico em espécie (a tela "Fluxo de Caixa" —
/// docs/wiring/financeiro-telas-restantes.md §4): abrir a gaveta com um fundo de troco, registrar
/// suprimento/sangria/venda em espécie durante o turno, fechar com contagem cega e apurar a
/// diferença. NÃO CONFUNDIR com <c>FluxoDeCaixaService</c>/<c>GET /financeiro/fluxo</c> — aquele é
/// a PROJEÇÃO de saldo diário da Visão Geral (regime de competência/previsão), este é o fato físico
/// de dinheiro que mudou de mão na gaveta (ver nota de colisão de nome no doc citado).
///
/// SALDO ESPERADO É DERIVADO — <see cref="SaldoAbertura"/> + entradas (suprimento + venda em
/// espécie) − saídas (sangria), nunca um campo armazenado, mesmo racional de
/// <c>ContaBancariaCaixa.SaldoInicial</c> + ledger: evita drift entre o que a gaveta "deveria" ter
/// e a soma real dos movimentos da sessão.
///
/// INVARIANTES do agregado (as que fazem sentido DENTRO de uma única instância):
///   • Não registra movimento (suprimento/sangria/venda) fora de <see cref="StatusSessaoCaixa.Aberta"/>.
///   • Sangria nunca excede o saldo esperado NO MOMENTO do registro (nunca fica negativo).
///   • Fechar exige uma contagem física (<see cref="SaldoInformado"/> passa a existir só ao fechar).
/// A invariante "não abrir 2 sessões simultâneas para o mesmo caixa" NÃO é checável aqui — depende
/// de consultar outras instâncias persistidas — e por isso vive na Application
/// (<c>AbrirSessaoCaixaUseCase</c>, via <c>ISessaoCaixaRepository.ObterAbertaPorContaAsync</c>).
/// </summary>
public sealed class SessaoCaixa : AggregateRoot<string>
{
    private readonly List<MovimentoDeSessaoCaixa> _movimentos = new();

    public string BusinessId { get; private set; } = string.Empty;
    public string ContaCaixaId { get; private set; } = string.Empty;
    public string OperadorId { get; private set; } = string.Empty;
    public string OperadorNome { get; private set; } = string.Empty;
    public DateTimeOffset AbertaEm { get; private set; }
    public Money SaldoAbertura { get; private set; }
    public StatusSessaoCaixa Status { get; private set; }
    public DateTimeOffset? FechadaEm { get; private set; }
    public Money? SaldoInformado { get; private set; }

    public IReadOnlyList<MovimentoDeSessaoCaixa> Movimentos => _movimentos.AsReadOnly();

    /// <summary>Soma de suprimentos + vendas em espécie — as entradas do turno.</summary>
    public Money TotalEntradas => _movimentos
        .Where(m => m.Tipo is TipoMovimentoCaixa.Suprimento or TipoMovimentoCaixa.VendaEmEspecie)
        .Aggregate(Money.Zero, static (acumulado, m) => acumulado + m.Valor);

    /// <summary>Soma das sangrias — as saídas do turno.</summary>
    public Money TotalSaidas => _movimentos
        .Where(m => m.Tipo == TipoMovimentoCaixa.Sangria)
        .Aggregate(Money.Zero, static (acumulado, m) => acumulado + m.Valor);

    /// <summary>Quanto a gaveta DEVERIA ter agora — abertura + entradas − saídas. Sempre
    /// recalculado a partir de <see cref="Movimentos"/>, nunca cacheado.</summary>
    public Money SaldoEsperado => SaldoAbertura + TotalEntradas - TotalSaidas;

    /// <summary>Contado (fechamento) − esperado. Positivo = sobra, negativo = falta. <c>null</c>
    /// enquanto a sessão está <see cref="StatusSessaoCaixa.Aberta"/> (contagem ainda não existe).</summary>
    public Money? Diferenca => SaldoInformado is { } informado ? informado - SaldoEsperado : null;

    private SessaoCaixa()
    {
    }

    public static Result<SessaoCaixa> Abrir(
        string businessId, string contaCaixaId, string operadorId, string operadorNome,
        Money saldoAbertura, DateTimeOffset abertaEm)
    {
        if (string.IsNullOrWhiteSpace(businessId))
            return Result.Falhar<SessaoCaixa>(new Error("financeiro.sessao_caixa.business_invalido", "BusinessId é obrigatório."));

        if (string.IsNullOrWhiteSpace(contaCaixaId))
            return Result.Falhar<SessaoCaixa>(new Error("financeiro.sessao_caixa.conta_invalida", "ContaCaixaId é obrigatório."));

        if (string.IsNullOrWhiteSpace(operadorId))
            return Result.Falhar<SessaoCaixa>(new Error("financeiro.sessao_caixa.operador_invalido", "Operador é obrigatório para abrir o caixa."));

        if (saldoAbertura.EhNegativo)
            return Result.Falhar<SessaoCaixa>(new Error("financeiro.sessao_caixa.abertura_negativa", "Saldo de abertura não pode ser negativo."));

        var sessao = new SessaoCaixa
        {
            Id = IdGenerator.NovoId(),
            BusinessId = businessId,
            ContaCaixaId = contaCaixaId,
            OperadorId = operadorId,
            OperadorNome = operadorNome,
            AbertaEm = abertaEm,
            SaldoAbertura = saldoAbertura,
            Status = StatusSessaoCaixa.Aberta
        };

        sessao.Raise(new CaixaAberto(sessao.Id, businessId, contaCaixaId, operadorId, saldoAbertura.Centavos));
        return Result.Ok(sessao);
    }

    /// <summary>REIDRATAÇÃO a partir do banco — não valida, não levanta evento (R6).</summary>
    public static SessaoCaixa Reconstituir(
        string id, string businessId, string contaCaixaId, string operadorId, string operadorNome,
        DateTimeOffset abertaEm, Money saldoAbertura, StatusSessaoCaixa status,
        IReadOnlyList<MovimentoDeSessaoCaixa> movimentos, DateTimeOffset? fechadaEm, Money? saldoInformado)
    {
        var sessao = new SessaoCaixa
        {
            Id = id,
            BusinessId = businessId,
            ContaCaixaId = contaCaixaId,
            OperadorId = operadorId,
            OperadorNome = operadorNome,
            AbertaEm = abertaEm,
            SaldoAbertura = saldoAbertura,
            Status = status,
            FechadaEm = fechadaEm,
            SaldoInformado = saldoInformado
        };
        sessao._movimentos.AddRange(movimentos);
        return sessao;
    }

    public Result RegistrarSuprimento(Money valor, string motivo, DateTimeOffset quando, string operadorId, string operadorNome)
        => RegistrarMovimento(TipoMovimentoCaixa.Suprimento, valor, motivo, quando, operadorId, operadorNome);

    /// <summary>Registra uma venda paga em espécie durante o turno. Ver nota de evolução futura
    /// (handler de <c>VendaConcluida</c>) em <see cref="TipoMovimentoCaixa.VendaEmEspecie"/>.</summary>
    public Result RegistrarVendaEmEspecie(Money valor, DateTimeOffset quando, string operadorId, string operadorNome, string? referencia = null)
        => RegistrarMovimento(TipoMovimentoCaixa.VendaEmEspecie, valor, referencia, quando, operadorId, operadorNome);

    /// <summary>Sangria nunca pode exceder o saldo esperado NO MOMENTO do registro — não existe
    /// gaveta negativa por retirada além do que ela tem.</summary>
    public Result RegistrarSangria(Money valor, string motivo, DateTimeOffset quando, string operadorId, string operadorNome)
    {
        var guarda = GarantirAberta();
        if (guarda.Falha) return guarda;

        if (valor.Centavos > SaldoEsperado.Centavos)
            return Result.Falhar(new Error(
                "financeiro.sessao_caixa.sangria_excede_saldo",
                $"Sangria de {valor.Formatado()} excede o saldo esperado de {SaldoEsperado.Formatado()} na gaveta."));

        var movimento = MovimentoDeSessaoCaixa.Registrar(TipoMovimentoCaixa.Sangria, valor, motivo, quando, operadorId, operadorNome);
        if (movimento.Falha) return movimento;

        _movimentos.Add(movimento.Valor);
        Raise(new SangriaRegistrada(Id, BusinessId, movimento.Valor.Id, valor.Centavos, motivo));
        return Result.Ok();
    }

    /// <summary>
    /// Fecha a sessão com a contagem física (cega) da gaveta. É AQUI que a diferença nasce —
    /// SaldoInformado (contado) menos SaldoEsperado (derivado dos movimentos) no instante do
    /// fechamento. Nunca recalculada depois: uma sessão Fechada é imutável (mesmo racional de
    /// <c>MovimentoFinanceiro</c> — corrigir é abrir uma NOVA sessão, nunca editar uma já fechada).
    /// </summary>
    public Result Fechar(Money saldoInformado, DateTimeOffset fechadaEm)
    {
        var transicao = StatusSessaoCaixaFsm.AssertirTransicao(Status, StatusSessaoCaixa.Fechada);
        if (transicao.Falha) return transicao;

        if (saldoInformado.EhNegativo)
            return Result.Falhar(new Error("financeiro.sessao_caixa.contagem_negativa", "Saldo contado não pode ser negativo."));

        var saldoEsperadoNoFechamento = SaldoEsperado;

        Status = StatusSessaoCaixa.Fechada;
        FechadaEm = fechadaEm;
        SaldoInformado = saldoInformado;

        var diferenca = saldoInformado - saldoEsperadoNoFechamento;
        Raise(new CaixaFechado(Id, BusinessId, ContaCaixaId, OperadorId, saldoEsperadoNoFechamento.Centavos, saldoInformado.Centavos, diferenca.Centavos));

        return Result.Ok();
    }

    private Result RegistrarMovimento(
        TipoMovimentoCaixa tipo, Money valor, string? motivo, DateTimeOffset quando, string operadorId, string operadorNome)
    {
        var guarda = GarantirAberta();
        if (guarda.Falha) return guarda;

        var movimento = MovimentoDeSessaoCaixa.Registrar(tipo, valor, motivo, quando, operadorId, operadorNome);
        if (movimento.Falha) return movimento;

        _movimentos.Add(movimento.Valor);

        switch (tipo)
        {
            case TipoMovimentoCaixa.Suprimento:
                Raise(new SuprimentoRegistrado(Id, BusinessId, movimento.Valor.Id, valor.Centavos, motivo ?? string.Empty));
                break;
            case TipoMovimentoCaixa.VendaEmEspecie:
                Raise(new VendaEmEspecieRegistrada(Id, BusinessId, movimento.Valor.Id, valor.Centavos));
                break;
        }

        return Result.Ok();
    }

    private Result GarantirAberta()
    {
        if (Status != StatusSessaoCaixa.Aberta)
            return Result.Falhar(new Error(
                "financeiro.sessao_caixa.status_invalido", $"Não é possível operar: a sessão está '{Status}', não 'Aberta'."));

        return Result.Ok();
    }
}
