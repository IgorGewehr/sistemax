using SistemaX.Modules.Abstractions;
using SistemaX.SharedKernel;

namespace SistemaX.Verticals.Assistencia;

/// <summary>
/// Agregado raiz do vertical Assistência Técnica — equipamento → defeito → diagnóstico →
/// orçamento (peças + mão de obra, aprovados JUNTOS) → execução → pronta → entrega, com uma
/// FSM explícita (ver <see cref="StatusOrdemServico"/>) e o mesmo padrão emissor de Vendas: ao
/// entregar, levanta um evento de domínio que a Application traduz para o evento de integração
/// <c>OsFaturada</c>, consumido pelo Financeiro.
///
/// Este é o exemplo trabalhado de "vertical" na arquitetura (ver
/// docs/arquitetura/COMO-CRIAR-UM-VERTICAL.md): um conceito que não existe para todo mundo (só
/// quem vende Assistência liga este módulo), mas que fala com o resto do sistema EXATAMENTE
/// pela mesma linguagem que um módulo core (Vendas) fala — eventos de domínio, eventos de
/// integração, <see cref="IModule"/>. Nada aqui é "especial" por ser vertical.
///
/// TIMESTAMPS EXPLÍCITOS: todo método que muda estado recebe <c>DateTimeOffset agora</c> como
/// parâmetro (nunca lê o relógio do sistema por conta própria) — mesmo padrão de
/// <c>Venda.RegistrarPagamento</c>. Isso mantém o domínio determinístico e testável sem mock de
/// tempo; a Application injeta o relógio real (ou um <c>IRelogio</c> de teste, como o Financeiro
/// já faz).
/// </summary>
public sealed class OrdemDeServico : AggregateRoot<string>
{
    private readonly List<PecaAplicada> _pecasAplicadas = new();
    private readonly List<HistoricoTransicaoOs> _historico = new();

    public string TenantId { get; private set; } = string.Empty;

    /// <summary>Número curto por tenant (ex.: "OS-0042") — o que se fala no telefone. Gerado
    /// pela Application (sequência local, fora do escopo deste agregado); <see cref="AggregateRoot{TId}.Id"/>
    /// (ULID) continua sendo a identidade real, interna.</summary>
    public string Numero { get; private set; } = string.Empty;

    public StatusOrdemServico Status { get; private set; }
    public ClienteRef Cliente { get; private set; } = null!;
    public Equipamento Equipamento { get; private set; } = null!;
    public string DefeitoRelatado { get; private set; } = string.Empty;
    public string? Diagnostico { get; private set; }
    public string? TecnicoId { get; private set; }
    public string? TecnicoNome { get; private set; }
    public DateTimeOffset AbertaEm { get; private set; }
    public DateTimeOffset? PrevisaoEntrega { get; private set; }

    /// <summary>Retorno em garantia: aponta para a OS original quando não nulo (nenhum estado
    /// novo na FSM — a OS de garantia percorre o mesmo funil, ver §5.4 do plano).</summary>
    public string? OsOrigemId { get; private set; }
    public bool EhRetornoDeGarantia => OsOrigemId is not null;

    public Orcamento? Orcamento { get; private set; }
    public RegistroAprovacao? Aprovacao { get; private set; }
    public string? MotivoReprovacao { get; private set; }

    /// <summary>Mão de obra final da execução — <c>null</c> até <see cref="IniciarExecucao"/>
    /// (nasce igual ao orçado; só editável para baixo sem re-aprovação, ver
    /// <see cref="AjustarMaoDeObraFinal"/>).</summary>
    public Money? MaoDeObraFinal { get; private set; }

    public string? MotivoCancelamento { get; private set; }

    public FormaPagamento? FormaPagamento { get; private set; }
    public Money Desconto { get; private set; } = Money.Zero;
    public int GarantiaDias { get; private set; }
    public DateTimeOffset? DataEntrega { get; private set; }
    public DateTimeOffset? GarantiaAte { get; private set; }

    public IReadOnlyList<PecaAplicada> PecasAplicadas => _pecasAplicadas.AsReadOnly();
    public IReadOnlyList<HistoricoTransicaoOs> Historico => _historico.AsReadOnly();

    public Money TotalPecasAplicadas =>
        _pecasAplicadas.Aggregate(Money.Zero, static (acumulado, peca) => acumulado + peca.Subtotal);

    /// <summary>Mão de obra "corrente": orçada antes da execução, final durante/depois dela.</summary>
    public Money MaoDeObraAtual => MaoDeObraFinal ?? Orcamento?.MaoDeObra ?? Money.Zero;

    /// <summary>Total realizado até agora — nunca cacheado, sempre recalculado (evita drift
    /// entre o total guardado e a soma real das linhas, mesmo cuidado de <c>Venda.Total</c>).</summary>
    public Money TotalGeral => MaoDeObraAtual + TotalPecasAplicadas;

    /// <summary>Reidratação (repositório da Infrastructure). Código de aplicação sempre entra
    /// por <see cref="Abrir"/>.</summary>
    private OrdemDeServico()
    {
    }

    public static OrdemDeServico Abrir(
        string tenantId,
        string numero,
        ClienteRef cliente,
        Equipamento equipamento,
        string defeitoRelatado,
        DateTimeOffset agora,
        DateTimeOffset? previsaoEntrega = null,
        string? osOrigemId = null)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            throw new ArgumentException("TenantId é obrigatório para abrir uma OS.", nameof(tenantId));

        if (string.IsNullOrWhiteSpace(numero))
            throw new ArgumentException("Número da OS é obrigatório.", nameof(numero));

        ArgumentNullException.ThrowIfNull(cliente);
        ArgumentNullException.ThrowIfNull(equipamento);

        if (string.IsNullOrWhiteSpace(defeitoRelatado))
            throw new ArgumentException("Defeito relatado é obrigatório para abrir uma OS.", nameof(defeitoRelatado));

        return new OrdemDeServico
        {
            Id = Ulid.NewUlid().ToString(),
            TenantId = tenantId,
            Numero = numero,
            Cliente = cliente,
            Equipamento = equipamento,
            DefeitoRelatado = defeitoRelatado,
            AbertaEm = agora,
            PrevisaoEntrega = previsaoEntrega,
            OsOrigemId = osOrigemId,
            Status = StatusOrdemServico.Aberta
        };
    }

    /// <summary>Atribuível a qualquer momento antes de um estado terminal — mas vira obrigatório
    /// na prática ao entrar em <see cref="StatusOrdemServico.EmDiagnostico"/> (ver
    /// <see cref="RegistrarDiagnostico"/>): é o técnico quem diagnostica.</summary>
    public Result AtribuirTecnico(string tecnicoId, string tecnicoNome)
    {
        if (EhTerminal(Status))
            return Result.Falhar(new Error(
                "os.status_terminal", $"Não é possível atribuir técnico: a OS está em estado terminal '{Status}'."));

        if (string.IsNullOrWhiteSpace(tecnicoId) || string.IsNullOrWhiteSpace(tecnicoNome))
            return Result.Falhar(new Error("os.tecnico_invalido", "Técnico exige id e nome."));

        TecnicoId = tecnicoId;
        TecnicoNome = tecnicoNome;
        return Result.Ok();
    }

    /// <summary>Promessa ao cliente — editável até <see cref="StatusOrdemServico.Pronta"/>
    /// (depois disso o equipamento já está pronto, prazo deixa de fazer sentido).</summary>
    public Result AlterarPrevisaoEntrega(DateTimeOffset novaPrevisao)
    {
        if (Status is StatusOrdemServico.Pronta or StatusOrdemServico.Entregue
            or StatusOrdemServico.DevolvidaSemReparo or StatusOrdemServico.Cancelada)
        {
            return Result.Falhar(new Error(
                "os.previsao_nao_editavel", $"Previsão de entrega não é mais editável no estado '{Status}'."));
        }

        PrevisaoEntrega = novaPrevisao;
        return Result.Ok();
    }

    public Result RegistrarDiagnostico(string diagnostico, DateTimeOffset agora)
    {
        var transicao = Fsm<StatusOrdemServico>.ValidarTransicao(
            Status, StatusOrdemServico.EmDiagnostico, TransicoesPermitidas);
        if (transicao.Falha) return transicao;

        if (string.IsNullOrWhiteSpace(diagnostico))
            return Result.Falhar(new Error("os.diagnostico_obrigatorio", "Diagnóstico não pode ser vazio."));

        if (TecnicoId is null)
            return Result.Falhar(new Error(
                "os.tecnico_obrigatorio", "Atribua um técnico (AtribuirTecnico) antes de registrar o diagnóstico."));

        RegistrarHistorico(StatusOrdemServico.EmDiagnostico, agora);
        Status = StatusOrdemServico.EmDiagnostico;
        Diagnostico = diagnostico;
        return Result.Ok();
    }

    /// <summary>Envia (ou reenvia, substituindo o anterior — auto-loop permitido no mesmo
    /// estado) o orçamento com peças previstas E mão de obra juntas: é essa combinação que
    /// elimina a briga na entrega do código original, onde só a mão de obra era orçada.</summary>
    public Result EnviarOrcamento(
        IReadOnlyList<PecaOrcada> pecasPrevistas, Money maoDeObra, int validadeDias, DateTimeOffset agora)
    {
        var transicao = Fsm<StatusOrdemServico>.ValidarTransicao(
            Status, StatusOrdemServico.AguardandoAprovacao, TransicoesPermitidas);
        if (transicao.Falha) return transicao;

        if (maoDeObra.EhNegativo)
            return Result.Falhar(new Error("os.mao_de_obra_invalida", "Mão de obra não pode ser negativa."));

        if (validadeDias <= 0)
            return Result.Falhar(new Error("os.validade_invalida", "Validade do orçamento deve ser maior que zero dias."));

        if (pecasPrevistas.Any(peca => peca.Quantidade <= 0))
            return Result.Falhar(new Error("os.quantidade_invalida", "Quantidade de peça prevista deve ser maior que zero."));

        var eraReenvio = Status == StatusOrdemServico.AguardandoAprovacao;
        if (!eraReenvio) RegistrarHistorico(StatusOrdemServico.AguardandoAprovacao, agora);

        Status = StatusOrdemServico.AguardandoAprovacao;
        Orcamento = new Orcamento(pecasPrevistas.ToList(), maoDeObra, validadeDias, agora);
        return Result.Ok();
    }

    /// <summary>Registra a aprovação do cliente — dispara a reserva (evento de domínio, ver
    /// <see cref="PecaReservadaDomainEvent"/>) de toda peça prevista com produto de catálogo.
    /// Peça sob encomenda (<c>ProdutoId</c> nulo) não gera reserva: não há o que reservar.</summary>
    public Result RegistrarAprovacao(
        CanalAprovacao canal, DateTimeOffset agora, string? registradoPorId = null, string? registradoPorNome = null)
    {
        var transicao = Fsm<StatusOrdemServico>.ValidarTransicao(
            Status, StatusOrdemServico.Aprovada, TransicoesPermitidas);
        if (transicao.Falha) return transicao;

        RegistrarHistorico(StatusOrdemServico.Aprovada, agora, registradoPorId, registradoPorNome);
        Status = StatusOrdemServico.Aprovada;
        Aprovacao = new RegistroAprovacao(DecisaoOrcamento.Aprovada, canal, registradoPorId, registradoPorNome, agora);

        foreach (var peca in Orcamento!.Pecas.Where(peca => peca.ProdutoId is not null))
            Raise(new PecaReservadaDomainEvent(Id, TenantId, peca.LinhaId, peca.ProdutoId!, peca.Quantidade));

        return Result.Ok();
    }

    public Result RegistrarReprovacao(
        CanalAprovacao canal, DateTimeOffset agora, string? motivo = null,
        string? registradoPorId = null, string? registradoPorNome = null)
    {
        var transicao = Fsm<StatusOrdemServico>.ValidarTransicao(
            Status, StatusOrdemServico.Reprovada, TransicoesPermitidas);
        if (transicao.Falha) return transicao;

        RegistrarHistorico(StatusOrdemServico.Reprovada, agora, registradoPorId, registradoPorNome);
        Status = StatusOrdemServico.Reprovada;
        Aprovacao = new RegistroAprovacao(DecisaoOrcamento.Reprovada, canal, registradoPorId, registradoPorNome, agora);
        MotivoReprovacao = motivo;
        return Result.Ok();
    }

    /// <summary>Devolve o equipamento sem reparo. <paramref name="taxaDiagnostico"/> zero (o
    /// default do tenant, tipicamente) não emite <c>OsFaturada</c> nenhum — só uma OS
    /// entregue-com-reparo OU devolvida-com-taxa fatura, nunca as duas (§7.4 do plano).</summary>
    public Result DevolverSemReparo(Money taxaDiagnostico, DateTimeOffset agora)
    {
        var transicao = Fsm<StatusOrdemServico>.ValidarTransicao(
            Status, StatusOrdemServico.DevolvidaSemReparo, TransicoesPermitidas);
        if (transicao.Falha) return transicao;

        if (taxaDiagnostico.EhNegativo)
            return Result.Falhar(new Error("os.taxa_diagnostico_invalida", "Taxa de diagnóstico não pode ser negativa."));

        RegistrarHistorico(StatusOrdemServico.DevolvidaSemReparo, agora);
        Status = StatusOrdemServico.DevolvidaSemReparo;
        DataEntrega = agora;

        if (taxaDiagnostico.EhPositivo)
        {
            Raise(new OsFaturadaDomainEvent(
                Id, TenantId, taxaDiagnostico, Money.Zero, Cliente.ClienteId, Cliente.Nome, Numero, null, TecnicoId));
        }

        return Result.Ok();
    }

    /// <summary>Mão de obra final nasce igual à orçada — só muda por
    /// <see cref="AjustarMaoDeObraFinal"/> daqui em diante.</summary>
    public Result IniciarExecucao(DateTimeOffset agora)
    {
        var transicao = Fsm<StatusOrdemServico>.ValidarTransicao(
            Status, StatusOrdemServico.EmExecucao, TransicoesPermitidas);
        if (transicao.Falha) return transicao;

        RegistrarHistorico(StatusOrdemServico.EmExecucao, agora);
        Status = StatusOrdemServico.EmExecucao;
        MaoDeObraFinal = Orcamento!.MaoDeObra;
        return Result.Ok();
    }

    /// <summary>Aplica (baixa) uma peça QUE JÁ ESTAVA no orçamento aprovado — pré-carregada, "1
    /// clique = apliquei" na UI (§8.4 do plano). Peça fora do orçamento entra por
    /// <see cref="AdicionarPecaExtra"/>, nunca por aqui.</summary>
    public Result AplicarPeca(string linhaId, DateTimeOffset agora)
    {
        if (Status != StatusOrdemServico.EmExecucao)
            return Result.Falhar(new Error(
                "os.status_invalido", $"Só é possível aplicar peça com a OS 'EmExecucao' (status atual: '{Status}')."));

        if (_pecasAplicadas.Any(peca => peca.LinhaId == linhaId))
            return Result.Falhar(new Error("os.peca_ja_aplicada", $"A linha '{linhaId}' já foi aplicada."));

        var linhaOrcada = Orcamento?.Pecas.FirstOrDefault(peca => peca.LinhaId == linhaId);
        if (linhaOrcada is null)
            return Result.Falhar(new Error(
                "os.peca_nao_orcada", $"A linha '{linhaId}' não existe no orçamento — use AdicionarPecaExtra."));

        _pecasAplicadas.Add(new PecaAplicada(
            linhaOrcada.LinhaId, linhaOrcada.ProdutoId, linhaOrcada.Descricao,
            linhaOrcada.Quantidade, linhaOrcada.PrecoUnitario, OrigemPeca.Orcada));

        if (linhaOrcada.ProdutoId is not null)
        {
            Raise(new PecaConsumidaDomainEvent(
                Id, TenantId, linhaOrcada.LinhaId, linhaOrcada.ProdutoId, linhaOrcada.Quantidade, linhaOrcada.PrecoUnitario));
        }

        return Result.Ok();
    }

    /// <summary>Peça descoberta na bancada, fora do orçamento — MEXE no valor combinado com o
    /// cliente, por isso exige <paramref name="clienteAvisado"/> explícito (guarda de valor,
    /// §5.3.4 do plano: sem isso, uma OS poderia fechar cobrando mais do que o cliente
    /// aprovou sem rastro de que ele soube).</summary>
    public Result AdicionarPecaExtra(
        string? produtoId, string descricao, int quantidade, Money precoUnitario, bool clienteAvisado, DateTimeOffset agora)
    {
        if (Status != StatusOrdemServico.EmExecucao)
            return Result.Falhar(new Error(
                "os.status_invalido", $"Só é possível adicionar peça extra com a OS 'EmExecucao' (status atual: '{Status}')."));

        if (quantidade <= 0)
            return Result.Falhar(new Error("os.quantidade_invalida", "Quantidade deve ser maior que zero."));

        if (precoUnitario.EhNegativo)
            return Result.Falhar(new Error("os.preco_invalido", "Preço unitário não pode ser negativo."));

        if (!clienteAvisado)
            return Result.Falhar(new Error(
                "os.peca_extra_exige_aviso", "Peça extra muda o valor combinado — confirme que o cliente foi avisado."));

        var linhaId = Ulid.NewUlid().ToString();
        _pecasAplicadas.Add(new PecaAplicada(linhaId, produtoId, descricao, quantidade, precoUnitario, OrigemPeca.Extra));

        if (produtoId is not null)
            Raise(new PecaConsumidaDomainEvent(Id, TenantId, linhaId, produtoId, quantidade, precoUnitario));

        return Result.Ok();
    }

    /// <summary>Reduzir é sempre livre; aumentar acima do orçado exige a mesma confirmação de
    /// "cliente avisado" que peça extra (mesma guarda de valor, §5.3.4 do plano).</summary>
    public Result AjustarMaoDeObraFinal(Money novoValor, bool clienteAvisado)
    {
        if (Status != StatusOrdemServico.EmExecucao)
            return Result.Falhar(new Error(
                "os.status_invalido", $"Só é possível ajustar mão de obra com a OS 'EmExecucao' (status atual: '{Status}')."));

        if (novoValor.EhNegativo)
            return Result.Falhar(new Error("os.mao_de_obra_invalida", "Mão de obra não pode ser negativa."));

        var orcado = Orcamento!.MaoDeObra;
        if (novoValor.Centavos > orcado.Centavos && !clienteAvisado)
        {
            return Result.Falhar(new Error(
                "os.aumento_mao_de_obra_exige_aviso",
                "Aumentar a mão de obra acima do orçado exige confirmar que o cliente foi avisado."));
        }

        MaoDeObraFinal = novoValor;
        return Result.Ok();
    }

    /// <summary>Fecha a execução — toda peça prevista com produto de catálogo que NÃO foi
    /// aplicada libera a reserva feita na aprovação (evento de domínio, ver
    /// <see cref="ReservaLiberadaDomainEvent"/>): peça prevista mas não usada volta ao
    /// disponível.</summary>
    public Result ConcluirExecucao(DateTimeOffset agora)
    {
        var transicao = Fsm<StatusOrdemServico>.ValidarTransicao(
            Status, StatusOrdemServico.Pronta, TransicoesPermitidas);
        if (transicao.Falha) return transicao;

        RegistrarHistorico(StatusOrdemServico.Pronta, agora);
        Status = StatusOrdemServico.Pronta;

        var linhasAplicadas = _pecasAplicadas.Select(peca => peca.LinhaId).ToHashSet();
        foreach (var naoAplicada in Orcamento!.Pecas.Where(
            peca => peca.ProdutoId is not null && !linhasAplicadas.Contains(peca.LinhaId)))
        {
            Raise(new ReservaLiberadaDomainEvent(Id, TenantId, naoAplicada.LinhaId, naoAplicada.ProdutoId!, naoAplicada.Quantidade));
        }

        return Result.Ok();
    }

    /// <summary>
    /// Fatura E entrega no MESMO ato — na assistência o cliente paga quando retira; separar os
    /// dois estados criaria um limbo ("faturada mas o aparelho está aqui") e violaria a regra de
    /// "fato financeiro = uma transação local única" (ARCHITECTURE §2.4). É aqui que nasce o
    /// evento de domínio que a Application traduz para <c>OsFaturada</c>, DEPOIS do commit —
    /// exatamente como <c>Venda.Concluir()</c> faz para <c>VendaConcluida</c>.
    ///
    /// Desconto abate PRIMEIRO a mão de obra (decisão determinística, favorável ao custo de
    /// peças — §7.1 do plano): só sobra pra peças o que exceder a mão de obra.
    ///
    /// Uma OS de garantia com total zero (peças em garantia com preço 0, mão de obra 0) NÃO
    /// emite <c>OsFaturada</c> — não há nada a receber; o consumo de peças já foi registrado em
    /// <see cref="AplicarPeca"/>/<see cref="AdicionarPecaExtra"/> (§5.4 do plano).
    /// </summary>
    public Result Entregar(FormaPagamento formaPagamento, Money desconto, int garantiaDias, DateTimeOffset agora)
    {
        var transicao = Fsm<StatusOrdemServico>.ValidarTransicao(
            Status, StatusOrdemServico.Entregue, TransicoesPermitidas);
        if (transicao.Falha) return transicao;

        if (desconto.EhNegativo)
            return Result.Falhar(new Error("os.desconto_invalido", "Desconto não pode ser negativo."));

        if (garantiaDias < 0)
            return Result.Falhar(new Error("os.garantia_invalida", "Dias de garantia não pode ser negativo."));

        if (desconto.Centavos > TotalGeral.Centavos)
            return Result.Falhar(new Error("os.desconto_maior_que_total", "Desconto não pode ser maior que o total da OS."));

        var valorServicoLiquido = new Money(Math.Max(0, MaoDeObraAtual.Centavos - desconto.Centavos));
        var descontoRestante = new Money(Math.Max(0, desconto.Centavos - MaoDeObraAtual.Centavos));
        var valorPecasLiquido = new Money(TotalPecasAplicadas.Centavos - descontoRestante.Centavos);

        RegistrarHistorico(StatusOrdemServico.Entregue, agora);
        Status = StatusOrdemServico.Entregue;
        FormaPagamento = formaPagamento;
        Desconto = desconto;
        GarantiaDias = garantiaDias;
        DataEntrega = agora;
        GarantiaAte = agora.AddDays(garantiaDias);

        if (!TotalGeral.EhZero)
        {
            Raise(new OsFaturadaDomainEvent(
                Id, TenantId, valorServicoLiquido, valorPecasLiquido,
                Cliente.ClienteId, Cliente.Nome, Numero, formaPagamento, TecnicoId));
        }

        return Result.Ok();
    }

    /// <summary>
    /// Cancela em qualquer ponto pré-entrega e estorna o estoque conforme onde a OS estava
    /// (§6.4 do plano): antes de <see cref="StatusOrdemServico.Aprovada"/> não havia reserva
    /// (nenhum efeito); entre aprovada e execução libera todas as reservas; durante a execução
    /// libera as reservas restantes E estorna as baixas já feitas (equipamento foi desmontado).
    /// </summary>
    public Result Cancelar(string motivo, DateTimeOffset agora)
    {
        var transicao = Fsm<StatusOrdemServico>.ValidarTransicao(
            Status, StatusOrdemServico.Cancelada, TransicoesPermitidas);
        if (transicao.Falha) return transicao;

        if (string.IsNullOrWhiteSpace(motivo))
            return Result.Falhar(new Error("os.motivo_obrigatorio", "Motivo do cancelamento é obrigatório."));

        var statusAnterior = Status;
        RegistrarHistorico(StatusOrdemServico.Cancelada, agora);
        Status = StatusOrdemServico.Cancelada;
        MotivoCancelamento = motivo;

        if (statusAnterior is StatusOrdemServico.Aprovada or StatusOrdemServico.EmExecucao)
        {
            var linhasAplicadas = _pecasAplicadas.Select(peca => peca.LinhaId).ToHashSet();
            foreach (var reservada in Orcamento!.Pecas.Where(
                peca => peca.ProdutoId is not null && !linhasAplicadas.Contains(peca.LinhaId)))
            {
                Raise(new ReservaLiberadaDomainEvent(Id, TenantId, reservada.LinhaId, reservada.ProdutoId!, reservada.Quantidade));
            }
        }

        if (statusAnterior == StatusOrdemServico.EmExecucao)
        {
            foreach (var aplicada in _pecasAplicadas.Where(peca => peca.ProdutoId is not null))
                Raise(new ConsumoEstornadoDomainEvent(Id, TenantId, aplicada.LinhaId, aplicada.ProdutoId!, aplicada.Quantidade));
        }

        return Result.Ok();
    }

    /// <summary>Derivado — NUNCA persistido como verdade (§4.7 do plano). Mesma decisão de
    /// tratar atraso/urgência como cálculo, não status ou campo gravado.</summary>
    public bool EstaAtrasada(DateTimeOffset agora) =>
        PrevisaoEntrega is { } previsao && agora.Date > previsao.Date && !EhTerminal(Status);

    /// <summary>Orçamento vencido é alerta, não trava (§4.3 do plano) — só faz sentido enquanto
    /// a OS ainda espera decisão do cliente.</summary>
    public bool OrcamentoVencido(DateTimeOffset agora) =>
        Status == StatusOrdemServico.AguardandoAprovacao && Orcamento is not null && agora > Orcamento.VenceEm;

    /// <summary>Tempo desde a última transição registrada (ou desde a abertura, se ainda não
    /// transicionou) — alimenta "há 6 dias" nas telas de fila/detalhe.</summary>
    public TimeSpan TempoNaEtapaAtual(DateTimeOffset agora)
    {
        var inicioDaEtapa = _historico.Count > 0 ? _historico[^1].Em : AbertaEm;
        return agora - inicioDaEtapa;
    }

    private void RegistrarHistorico(StatusOrdemServico para, DateTimeOffset agora, string? porId = null, string? porNome = null)
        => _historico.Add(new HistoricoTransicaoOs(Status, para, agora, porId, porNome));

    private static bool EhTerminal(StatusOrdemServico status) =>
        status is StatusOrdemServico.Entregue or StatusOrdemServico.DevolvidaSemReparo or StatusOrdemServico.Cancelada;

    /// <summary>Único ponto de verdade sobre transições legais — nenhum método acima escreve em
    /// <see cref="Status"/> sem passar por <see cref="Fsm{TStatus}.ValidarTransicao"/> contra este
    /// mapa (regra dura do projeto: proibido status livre). <see cref="StatusOrdemServico.AguardandoAprovacao"/>
    /// tem auto-loop deliberado: reenviar orçamento no mesmo estado substitui o anterior.</summary>
    private static readonly IReadOnlyDictionary<StatusOrdemServico, StatusOrdemServico[]> TransicoesPermitidas =
        new Dictionary<StatusOrdemServico, StatusOrdemServico[]>
        {
            [StatusOrdemServico.Aberta] =
                [StatusOrdemServico.EmDiagnostico, StatusOrdemServico.Cancelada],
            [StatusOrdemServico.EmDiagnostico] =
                [StatusOrdemServico.AguardandoAprovacao, StatusOrdemServico.Cancelada],
            [StatusOrdemServico.AguardandoAprovacao] =
                [StatusOrdemServico.AguardandoAprovacao, StatusOrdemServico.Aprovada,
                 StatusOrdemServico.Reprovada, StatusOrdemServico.Cancelada],
            [StatusOrdemServico.Aprovada] =
                [StatusOrdemServico.EmExecucao, StatusOrdemServico.Cancelada],
            [StatusOrdemServico.Reprovada] =
                [StatusOrdemServico.DevolvidaSemReparo],
            [StatusOrdemServico.EmExecucao] =
                [StatusOrdemServico.Pronta, StatusOrdemServico.Cancelada],
            [StatusOrdemServico.Pronta] =
                [StatusOrdemServico.Entregue],
            [StatusOrdemServico.Entregue] = [],
            [StatusOrdemServico.DevolvidaSemReparo] = [],
            [StatusOrdemServico.Cancelada] = []
        };
}
