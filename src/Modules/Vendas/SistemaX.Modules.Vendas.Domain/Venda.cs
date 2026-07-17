using SistemaX.Modules.Abstractions;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Vendas.Domain;

/// <summary>
/// Agregado raiz do módulo Vendas — o exemplo EMISSOR de "tudo alimenta o financeiro"
/// (ver docs/arquitetura/ARCHITECTURE.md §5). Vendas não conhece Financeiro; ele só levanta
/// eventos de domínio que, traduzidos para eventos de integração (ver VendaDomainEvents.cs),
/// o Financeiro assina.
///
/// Fronteira de consistência transacional: <see cref="Itens"/> e <see cref="Pagamentos"/> só
/// existem dentro de uma Venda, nunca são referenciados de fora. Id é ULID (ordenável por
/// criação, gerado no terminal sem precisar do servidor — essencial para operar offline no PDV).
///
/// MONTAGEM vs PAGAMENTO — a FSM continua com só 3 estados (ver <see cref="StatusVenda"/>); a
/// distinção entre "carrinho em montagem" e "recebendo pagamento" não vira um 4º status porque
/// ambos acontecem dentro de <see cref="StatusVenda.Aberta"/> (persistida a cada mudança — é isso
/// que dá crash-safety ao PDV: um refresh/queda de energia não perde a venda). A trava é uma
/// invariante, não um estado: assim que o primeiro <see cref="PagamentoDeVenda"/> é registrado,
/// itens/descontos não podem mais mudar (ver <see cref="GarantirEmMontagem"/>) — porque editar o
/// carrinho depois que dinheiro já foi recebido invalidaria pagamentos calculados contra um total
/// que deixou de existir.
/// </summary>
public sealed class Venda : AggregateRoot<string>
{
    private readonly List<ItemDeVenda> _itens = new();
    private readonly List<PagamentoDeVenda> _pagamentos = new();

    public string TenantId { get; private set; } = string.Empty;
    public StatusVenda Status { get; private set; }
    public IReadOnlyList<ItemDeVenda> Itens => _itens.AsReadOnly();
    public IReadOnlyList<PagamentoDeVenda> Pagamentos => _pagamentos.AsReadOnly();
    public Money DescontoVenda { get; private set; } = Money.Zero;
    public string? MotivoDescontoVenda { get; private set; }

    /// <summary>Cliente vinculado ao carrinho (opcional — PDV de balcão permite venda sem
    /// identificar cliente). Companion da F0 do plano de inteligência do Financeiro
    /// (docs/financeiro/inteligencia-arquitetura.md §3.3/ADR-0005): fecha o gap documentado em
    /// <c>VendaConcluida.ClienteId</c> (Modules.Abstractions) — antes deste campo o agregado nunca
    /// capturava cliente, e <see cref="Concluir"/> sempre publicava <c>ClienteId: null</c>. Só pode
    /// mudar em montagem (mesma trava de <see cref="GarantirEmMontagem"/>): depois do primeiro
    /// pagamento o carrinho está congelado.</summary>
    public string? ClienteId { get; private set; }

    /// <summary>Soma dos subtotais líquidos dos itens (já com desconto de item aplicado) — é o
    /// teto de <see cref="DescontoVenda"/> (não dá para descontar mais do que os itens somam).</summary>
    public Money SubtotalItens => _itens.Aggregate(Money.Zero, static (acumulado, item) => acumulado + item.Subtotal);

    /// <summary>Total da venda. Sempre recalculado a partir de <see cref="Itens"/> e
    /// <see cref="DescontoVenda"/>, nunca cacheado num campo — evita drift entre o total
    /// armazenado e a soma real das linhas.</summary>
    public Money Total => SubtotalItens - DescontoVenda;

    public Money TotalPago => _pagamentos.Aggregate(Money.Zero, static (acumulado, pagamento) => acumulado + pagamento.Valor);

    /// <summary>Quanto falta para quitar a venda. Nunca negativo em operação normal: cada
    /// pagamento é aceito no máximo até este valor no MOMENTO do registro
    /// (ver <see cref="RegistrarPagamento"/>) — não existe overpayment de <see cref="Valor"/>
    /// de pagamento, só de <see cref="PagamentoDeVenda.ValorRecebido"/> em dinheiro (o troco).</summary>
    public Money Restante => Total - TotalPago;

    /// <summary>Método de pagamento "principal" — o de maior valor somado entre os registrados.
    /// Existe para alimentar o campo <c>FormaPagamento</c> (string) do evento de integração
    /// <c>VendaConcluida</c> (Modules.Abstractions), que hoje só comporta um método por venda —
    /// ver nota de evolução em VendaDomainEvents.cs. <c>null</c> enquanto não há pagamento algum.</summary>
    public string? FormaPagamento => _pagamentos.Count == 0
        ? null
        : _pagamentos
            .GroupBy(p => p.Metodo)
            .OrderByDescending(grupo => grupo.Sum(p => p.Valor.Centavos))
            .First().Key.ToString();

    /// <summary>Construtor privado — reidratação (repositório da Infrastructure) usa este mais
    /// os setters privados; código de aplicação sempre entra por <see cref="Abrir"/>.</summary>
    private Venda()
    {
    }

    public static Venda Abrir(string tenantId)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            throw new ArgumentException("TenantId é obrigatório para abrir uma venda.", nameof(tenantId));

        return new Venda
        {
            Id = Ulid.NewUlid().ToString(),
            TenantId = tenantId,
            Status = StatusVenda.Aberta
        };
    }

    /// <summary>REIDRATAÇÃO a partir do banco — usada só pela camada de persistência. Não valida, não levanta evento (R6).</summary>
    public static Venda Reconstituir(
        string id, string tenantId, StatusVenda status, IReadOnlyList<ItemDeVenda> itens,
        IReadOnlyList<PagamentoDeVenda> pagamentos, Money descontoVenda, string? motivoDescontoVenda,
        string? clienteId = null)
    {
        var venda = new Venda
        {
            Id = id,
            TenantId = tenantId,
            Status = status,
            DescontoVenda = descontoVenda,
            MotivoDescontoVenda = motivoDescontoVenda,
            ClienteId = clienteId
        };
        venda._itens.AddRange(itens);
        venda._pagamentos.AddRange(pagamentos);
        return venda;
    }

    public Result AdicionarItem(string produtoId, string descricao, int quantidade, Money precoUnitario)
    {
        var montagem = GarantirEmMontagem();
        if (montagem.Falha) return montagem;

        var item = ItemDeVenda.Criar(produtoId, descricao, quantidade, precoUnitario);
        if (item.Falha) return item;

        _itens.Add(item.Valor);
        return Result.Ok();
    }

    public Result RemoverItem(string itemId)
    {
        var montagem = GarantirEmMontagem();
        if (montagem.Falha) return montagem;

        var item = _itens.FirstOrDefault(i => i.Id == itemId);
        if (item is null)
            return Result.Falhar(new Error("venda.item.nao_encontrado", $"Item '{itemId}' não está na venda."));

        _itens.Remove(item);
        return Result.Ok();
    }

    public Result AlterarQuantidadeItem(string itemId, int novaQuantidade)
    {
        var montagem = GarantirEmMontagem();
        if (montagem.Falha) return montagem;

        var indice = _itens.FindIndex(i => i.Id == itemId);
        if (indice < 0)
            return Result.Falhar(new Error("venda.item.nao_encontrado", $"Item '{itemId}' não está na venda."));

        var atualizado = _itens[indice].ComQuantidade(novaQuantidade);
        if (atualizado.Falha) return atualizado;

        _itens[indice] = atualizado.Valor;
        return Result.Ok();
    }

    /// <summary>Desconto no valor ABSOLUTO da linha (não percentual — a UI calcula o percentual e
    /// converte antes de chamar, mantendo o domínio livre de ponto flutuante).</summary>
    public Result AplicarDescontoItem(string itemId, Money desconto)
    {
        var montagem = GarantirEmMontagem();
        if (montagem.Falha) return montagem;

        var indice = _itens.FindIndex(i => i.Id == itemId);
        if (indice < 0)
            return Result.Falhar(new Error("venda.item.nao_encontrado", $"Item '{itemId}' não está na venda."));

        var atualizado = _itens[indice].ComDesconto(desconto);
        if (atualizado.Falha) return atualizado;

        _itens[indice] = atualizado.Valor;
        return Result.Ok();
    }

    /// <summary>Vincula (ou desvincula, passando <c>null</c>) o cliente ao carrinho. Mesma trava de
    /// <see cref="GarantirEmMontagem"/> das demais mutações de carrinho — depois do primeiro
    /// pagamento a venda está congelada.</summary>
    public Result DefinirCliente(string? clienteId)
    {
        var montagem = GarantirEmMontagem();
        if (montagem.Falha) return montagem;

        ClienteId = string.IsNullOrWhiteSpace(clienteId) ? null : clienteId;
        return Result.Ok();
    }

    public Result AplicarDescontoVenda(Money desconto, string? motivo = null)
    {
        var montagem = GarantirEmMontagem();
        if (montagem.Falha) return montagem;

        if (desconto.EhNegativo)
            return Result.Falhar(new Error("venda.desconto_negativo", "Desconto não pode ser negativo."));

        if (desconto.Centavos > SubtotalItens.Centavos)
            return Result.Falhar(new Error(
                "venda.desconto_maior_que_subtotal", "Desconto da venda não pode ser maior que o subtotal dos itens."));

        DescontoVenda = desconto;
        MotivoDescontoVenda = motivo;
        return Result.Ok();
    }

    /// <summary>Adiciona uma linha de pagamento (split natural: chamar de novo cria outra linha).
    /// <paramref name="valor"/> nunca pode exceder <see cref="Restante"/> no instante do registro —
    /// é essa trava, e não uma checagem só no <see cref="Concluir"/>, que impede overpayment em
    /// métodos não-dinheiro (não existe "troco de PIX/cartão").</summary>
    public Result RegistrarPagamento(MetodoPagamento metodo, Money valor, Money? valorRecebido, DateTimeOffset registradoEm)
    {
        if (Status != StatusVenda.Aberta)
            return Result.Falhar(new Error(
                "venda.status_invalido", $"Não é possível registrar pagamento: a venda está '{Status}', não 'Aberta'."));

        if (_itens.Count == 0)
            return Result.Falhar(new Error("venda.sem_itens", "Não é possível registrar pagamento numa venda sem itens."));

        if (valor.Centavos > Restante.Centavos)
            return Result.Falhar(new Error(
                "venda.pagamento_excede_restante",
                $"Pagamento de {valor.Formatado()} excede o restante de {Restante.Formatado()}."));

        var pagamento = PagamentoDeVenda.Registrar(metodo, valor, valorRecebido, registradoEm);
        if (pagamento.Falha) return pagamento;

        _pagamentos.Add(pagamento.Valor);
        return Result.Ok();
    }

    public Result RemoverPagamento(string pagamentoId)
    {
        if (Status != StatusVenda.Aberta)
            return Result.Falhar(new Error(
                "venda.status_invalido", $"Não é possível remover pagamento: a venda está '{Status}', não 'Aberta'."));

        var pagamento = _pagamentos.FirstOrDefault(p => p.Id == pagamentoId);
        if (pagamento is null)
            return Result.Falhar(new Error("venda.pagamento.nao_encontrado", $"Pagamento '{pagamentoId}' não está na venda."));

        _pagamentos.Remove(pagamento);
        return Result.Ok();
    }

    /// <summary>
    /// Fecha a venda quando os pagamentos já cobrem o total. É AQUI que nasce o evento de domínio
    /// que, fora do agregado (na Application, após o commit), vira o evento de integração
    /// <c>VendaConcluida</c> que o Financeiro consome para criar ContaAReceber/MovimentoFinanceiro.
    /// </summary>
    public Result Concluir()
    {
        var transicao = Fsm<StatusVenda>.ValidarTransicao(Status, StatusVenda.Concluida, TransicoesPermitidas);
        if (transicao.Falha)
            return transicao;

        if (_itens.Count == 0)
            return Result.Falhar(new Error("venda.sem_itens", "Não é possível concluir uma venda sem itens."));

        if (_pagamentos.Count == 0)
            return Result.Falhar(new Error("venda.sem_pagamento", "Não é possível concluir uma venda sem nenhum pagamento registrado."));

        if (!Restante.EhZero)
            return Result.Falhar(new Error(
                "venda.pagamento_incompleto", $"Restam {Restante.Formatado()} a pagar para concluir a venda."));

        var formaPagamentoPrincipal = FormaPagamento!; // seguro: _pagamentos.Count > 0 acima garante não-nulo

        Status = StatusVenda.Concluida;

        var itensParaEstoque = _itens
            .Select(item => new ItemVendaParaEstoque(
                item.Id, item.ProdutoId, item.Descricao, item.Quantidade * 1000L,
                item.PrecoUnitario.Centavos, item.Desconto.Centavos))
            .ToArray();

        Raise(new VendaConcluidaDomainEvent(Id, TenantId, Total, formaPagamentoPrincipal, itensParaEstoque, ClienteId));

        return Result.Ok();
    }

    /// <summary>Estorna uma venda já concluída. Nunca edita/remove a venda original — só
    /// transiciona o status e levanta o evento que vira <c>VendaEstornada</c> (que no Financeiro
    /// lança um fato de REVERSÃO, jamais apaga o fato original — ver docs/financeiro).</summary>
    public Result Estornar()
    {
        var transicao = Fsm<StatusVenda>.ValidarTransicao(Status, StatusVenda.Estornada, TransicoesPermitidas);
        if (transicao.Falha)
            return transicao;

        Status = StatusVenda.Estornada;

        Raise(new VendaEstornadaDomainEvent(Id, TenantId, Total));

        return Result.Ok();
    }

    /// <summary>Guarda comum de AdicionarItem/RemoverItem/AlterarQuantidadeItem/AplicarDesconto* —
    /// ver nota de MONTAGEM vs PAGAMENTO na doc do tipo.</summary>
    private Result GarantirEmMontagem()
    {
        if (Status != StatusVenda.Aberta)
            return Result.Falhar(new Error(
                "venda.status_invalido", $"Não é possível alterar itens/descontos: a venda está '{Status}', não 'Aberta'."));

        if (_pagamentos.Count > 0)
            return Result.Falhar(new Error(
                "venda.pagamento_ja_iniciado",
                "Não é possível alterar itens/descontos depois que o primeiro pagamento foi registrado — remova os pagamentos primeiro."));

        return Result.Ok();
    }

    private static readonly IReadOnlyDictionary<StatusVenda, StatusVenda[]> TransicoesPermitidas =
        new Dictionary<StatusVenda, StatusVenda[]>
        {
            [StatusVenda.Aberta] = [StatusVenda.Concluida],
            [StatusVenda.Concluida] = [StatusVenda.Estornada],
            [StatusVenda.Estornada] = []
        };
}
