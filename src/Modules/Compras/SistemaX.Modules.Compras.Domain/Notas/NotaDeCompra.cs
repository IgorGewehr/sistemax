using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Compras.Domain.Comum;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Compras.Domain.Notas;

/// <summary>
/// Agregado raiz do módulo Compras — o coração do fluxo de entrada de mercadoria (plano §3-§8).
/// Fronteira de consistência: <see cref="Itens"/> só existe dentro de uma nota, nunca referenciado
/// de fora. É o EMISSOR canônico de <c>CompraRecebida</c>/<c>CompraItensRecebidos</c> (Financeiro
/// e Estoque assinam — Compras nunca os conhece, mesmo desenho de <c>Venda</c> em Vendas.Domain).
///
/// <see cref="ConfirmarRecebimento"/> é O momento do módulo: valida as invariantes de negócio
/// (match resolvido, fator de conversão aplicado, quantidade convertida positiva), congela o
/// rateio do custo de entrada (<see cref="CustoDeEntrada"/>) em cada item, e levanta o evento que —
/// fora do agregado, na Application, depois do commit — vira os dois eventos de integração.
/// </summary>
public sealed class NotaDeCompra : AggregateRoot<string>
{
    private readonly List<ItemDeNotaDeCompra> _itens = new();

    public string TenantId { get; private set; } = string.Empty;
    public string LojaId { get; private set; } = string.Empty;
    public string? FornecedorId { get; private set; }
    public OrigemNota Origem { get; private set; }
    public ChaveDeAcesso? ChaveDeAcesso { get; private set; }
    public string Numero { get; private set; } = string.Empty;
    public string Serie { get; private set; } = string.Empty;
    public DateTimeOffset DataEmissao { get; private set; }
    public TotaisDaNota Totais { get; private set; } = null!;
    public StatusNotaDeCompra Status { get; private set; }
    public IReadOnlyList<ItemDeNotaDeCompra> Itens => _itens.AsReadOnly();

    public DateTimeOffset? RecebidaEm { get; private set; }
    public string? RecebidaPorId { get; private set; }
    public string? RecebidaPorNome { get; private set; }
    public string? MotivoDescarte { get; private set; }

    private NotaDeCompra()
    {
    }

    /// <summary>Passo 6 do pipeline de importação (plano §4): a nota nasce <see cref="StatusNotaDeCompra.Importada"/>
    /// já com o match (passo 5) resolvido em cada item pela Application. Nota de origem
    /// <see cref="OrigemNota.Manual"/> é a única que dispensa <paramref name="chaveDeAcesso"/>.</summary>
    public static Result<NotaDeCompra> Importar(
        string tenantId, string lojaId, OrigemNota origem, string numero, string serie, DateTimeOffset dataEmissao,
        TotaisDaNota totais, IReadOnlyList<ItemDeNotaDeCompra> itens, string? fornecedorId = null, ChaveDeAcesso? chaveDeAcesso = null)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            return Result.Falhar<NotaDeCompra>(new Error("compras.nota.tenant_invalido", "TenantId é obrigatório."));

        if (string.IsNullOrWhiteSpace(lojaId))
            return Result.Falhar<NotaDeCompra>(new Error("compras.nota.loja_invalida", "LojaId é obrigatório."));

        if (itens.Count == 0)
            return Result.Falhar<NotaDeCompra>(new Error("compras.nota.sem_itens", "Nota sem itens não pode ser importada."));

        if (origem != OrigemNota.Manual && chaveDeAcesso is null)
            return Result.Falhar<NotaDeCompra>(new Error("compras.nota.chave_obrigatoria", "Nota de XML/DFe exige chave de acesso."));

        var nota = new NotaDeCompra
        {
            Id = IdGenerator.NovoId(),
            TenantId = tenantId,
            LojaId = lojaId,
            FornecedorId = fornecedorId,
            Origem = origem,
            ChaveDeAcesso = chaveDeAcesso,
            Numero = numero,
            Serie = serie,
            DataEmissao = dataEmissao,
            Totais = totais,
            Status = StatusNotaDeCompra.Importada
        };
        nota._itens.AddRange(itens);

        return Result.Ok(nota);
    }

    /// <summary>Abre a conferência — passo 7 do pipeline (plano §4), implícito ao abrir a tela;
    /// neste backend sem UI, a Application chama logo após <see cref="Importar"/>.</summary>
    public Result AbrirConferencia() => Transicionar(StatusNotaDeCompra.EmConferencia);

    public Result VincularFornecedor(string fornecedorId)
    {
        if (Status is StatusNotaDeCompra.Recebida or StatusNotaDeCompra.Descartada)
            return Result.Falhar(new Error("compras.nota.status_invalido", $"Não é possível vincular fornecedor: nota está '{Status}'."));

        if (string.IsNullOrWhiteSpace(fornecedorId))
            return Result.Falhar(new Error("compras.nota.fornecedor_invalido", "FornecedorId é obrigatório."));

        FornecedorId = fornecedorId;
        return Result.Ok();
    }

    /// <summary>Resolução humana (ou aprendizado automático via vínculo) de um item <c>Sugerido</c>/
    /// <c>SemMatch</c> — sempre resulta em <see cref="MatchState.Manual"/> (a Application decide
    /// separadamente se isso também atualiza o <c>VinculoProdutoFornecedor</c> do fornecedor).</summary>
    public Result ResolverMatch(int nItem, string produtoId, long fatorConversaoAplicadoMilesimos)
    {
        if (Status is not (StatusNotaDeCompra.Importada or StatusNotaDeCompra.EmConferencia))
            return Result.Falhar(new Error("compras.nota.status_invalido", $"Não é possível resolver match: nota está '{Status}'."));

        var indice = _itens.FindIndex(i => i.NItem == nItem);
        if (indice < 0)
            return Result.Falhar(new Error("compras.item.nao_encontrado", $"Item {nItem} não está na nota."));

        if (string.IsNullOrWhiteSpace(produtoId))
            return Result.Falhar(new Error("compras.item.produto_invalido", "ProdutoId é obrigatório para resolver o match."));

        if (fatorConversaoAplicadoMilesimos <= 0)
            return Result.Falhar(new Error("compras.item.fator_invalido", "Fator de conversão deve ser maior que zero."));

        _itens[indice] = _itens[indice].ComMatch(MatchState.Manual, produtoId, fatorConversaoAplicadoMilesimos);
        return Result.Ok();
    }

    /// <summary>Exclui deliberadamente um item do recebimento (amostra/brinde) — plano §3.3
    /// invariante 3: o recebimento parcial é HONESTO (item ignorado fica fora do evento), nunca um
    /// buraco silencioso.</summary>
    public Result IgnorarItem(int nItem, string motivo)
    {
        if (Status is not (StatusNotaDeCompra.Importada or StatusNotaDeCompra.EmConferencia))
            return Result.Falhar(new Error("compras.nota.status_invalido", $"Não é possível ignorar item: nota está '{Status}'."));

        var indice = _itens.FindIndex(i => i.NItem == nItem);
        if (indice < 0)
            return Result.Falhar(new Error("compras.item.nao_encontrado", $"Item {nItem} não está na nota."));

        _itens[indice] = _itens[indice].ComoIgnorado();
        return Result.Ok();
    }

    /// <summary>
    /// O MOMENTO do módulo (plano §3.2/§3.3). Valida que todo item não-ignorado tem match
    /// resolvido e fator de conversão aplicado, roda o rateio de custo de entrada
    /// (<see cref="CustoDeEntrada.Ratear"/>) sobre TODOS os itens — inclusive os ignorados, porque
    /// o valor deles ainda compõe <c>vNF</c> — e congela o resultado em cada linha. Só depois
    /// disso transiciona o FSM e levanta o evento de domínio.
    /// </summary>
    public Result ConfirmarRecebimento(string usuarioId, string usuarioNome, DateTimeOffset agora)
    {
        var transicao = Fsm<StatusNotaDeCompra>.ValidarTransicao(Status, StatusNotaDeCompra.Recebida, TransicoesPermitidas);
        if (transicao.Falha) return transicao;

        if (string.IsNullOrWhiteSpace(FornecedorId))
            return Result.Falhar(new Error("compras.nota.sem_fornecedor", "Não é possível confirmar recebimento sem fornecedor vinculado."));

        var itensNaoIgnorados = _itens.Where(i => i.MatchState != MatchState.Ignorado).ToList();
        if (itensNaoIgnorados.Count == 0)
            return Result.Falhar(new Error("compras.nota.sem_itens_validos", "Todos os itens estão ignorados — nada a receber."));

        foreach (var item in itensNaoIgnorados)
        {
            if (item.MatchState is MatchState.SemMatch or MatchState.Sugerido)
                return Result.Falhar(new Error("compras.item.match_pendente", $"Item {item.NItem} ainda não tem match resolvido."));

            if (item.FatorConversaoAplicadoMilesimos is not { } fator || fator <= 0)
                return Result.Falhar(new Error("compras.item.fator_ausente", $"Item {item.NItem} não tem fator de conversão aplicado."));

            if (item.QuantidadeConvertida is not { } qtd || !qtd.EhPositiva)
                return Result.Falhar(new Error("compras.item.quantidade_convertida_invalida", $"Item {item.NItem} resultou em quantidade convertida inválida."));
        }

        var rateio = CustoDeEntrada.Ratear(Totais, _itens);
        if (rateio.Falha) return Result.Falhar(rateio.Erro);

        for (var i = 0; i < _itens.Count; i++)
            _itens[i] = _itens[i].ComCustoDeEntrada(rateio.Valor[i]);

        var itensParaEstoque = MontarItensParaEstoque();

        Status = StatusNotaDeCompra.Recebida;
        RecebidaEm = agora;
        RecebidaPorId = usuarioId;
        RecebidaPorNome = usuarioNome;

        Raise(new NotaDeCompraRecebidaDomainEvent(Id, TenantId, FornecedorId!, Totais.VNf, itensParaEstoque));
        return Result.Ok();
    }

    /// <summary>Estorna um recebimento confirmado — nunca edita a nota original, só volta o FSM
    /// para conferência e levanta o evento que Financeiro/Estoque revertem cada um a seu modo
    /// (plano §8.4). Os custos congelados em <see cref="ConfirmarRecebimento"/> permanecem nos
    /// itens (auditoria) — reconfirmar depois recalcula o rateio do zero.</summary>
    public Result Estornar(string usuarioId, string usuarioNome, DateTimeOffset agora)
    {
        var transicao = Fsm<StatusNotaDeCompra>.ValidarTransicao(Status, StatusNotaDeCompra.EmConferencia, TransicoesPermitidas);
        if (transicao.Falha) return transicao;

        var itensParaEstoque = MontarItensParaEstoque();

        Status = StatusNotaDeCompra.EmConferencia;

        Raise(new NotaDeCompraEstornadaDomainEvent(Id, TenantId, FornecedorId!, Totais.VNf, itensParaEstoque));
        return Result.Ok();
    }

    public Result Descartar(string motivo)
    {
        var transicao = Fsm<StatusNotaDeCompra>.ValidarTransicao(Status, StatusNotaDeCompra.Descartada, TransicoesPermitidas);
        if (transicao.Falha) return transicao;

        Status = StatusNotaDeCompra.Descartada;
        MotivoDescarte = motivo;
        return Result.Ok();
    }

    /// <summary>Converte os itens não-ignorados (já com custo de entrada congelado) para o formato
    /// pobre que os eventos de integração exigem — custo UNITÁRIO derivado do total ÷ quantidade
    /// no momento da conversão (nunca persistido, plano §6.1).</summary>
    private IReadOnlyList<ItemRecebidoParaEstoque> MontarItensParaEstoque()
        => _itens
            .Where(i => i.MatchState != MatchState.Ignorado)
            .Select(item => new ItemRecebidoParaEstoque(
                item.ProdutoId!, item.DescricaoNf, item.QuantidadeConvertida!.Value.Milesimos,
                CustoUnitarioCentavos(item), item.NItem.ToString(), item.LoteFornecedor, item.Validade))
            .ToList();

    private static long CustoUnitarioCentavos(ItemDeNotaDeCompra item)
        => (long)Math.Round(item.CustoTotalEntrada!.Value.Centavos * 1000m / item.QuantidadeConvertida!.Value.Milesimos, MidpointRounding.ToEven);

    private Result Transicionar(StatusNotaDeCompra destino)
    {
        var transicao = Fsm<StatusNotaDeCompra>.ValidarTransicao(Status, destino, TransicoesPermitidas);
        if (transicao.Falha) return transicao;

        Status = destino;
        return Result.Ok();
    }

    private static readonly IReadOnlyDictionary<StatusNotaDeCompra, StatusNotaDeCompra[]> TransicoesPermitidas =
        new Dictionary<StatusNotaDeCompra, StatusNotaDeCompra[]>
        {
            [StatusNotaDeCompra.Importada] = [StatusNotaDeCompra.EmConferencia, StatusNotaDeCompra.Descartada],
            [StatusNotaDeCompra.EmConferencia] = [StatusNotaDeCompra.Recebida, StatusNotaDeCompra.Descartada],
            [StatusNotaDeCompra.Recebida] = [StatusNotaDeCompra.EmConferencia],
            [StatusNotaDeCompra.Descartada] = []
        };
}
