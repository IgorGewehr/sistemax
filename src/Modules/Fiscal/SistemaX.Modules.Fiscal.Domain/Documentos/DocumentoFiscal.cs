using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Fiscal.Domain.Comum;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Fiscal.Domain.Documentos;

/// <summary>
/// Agregado central do módulo — o "registro" de um fato fiscal já resolvido item a item. Nunca
/// calcula tributo sozinho (isso seria Domain fazendo orquestração cross-NCM); a Application
/// resolve via <c>MotorDeCalculoTributario</c> e entrega o item já pronto. Toda transição de
/// <see cref="Status"/> passa por <see cref="Fsm{TStatus}.ValidarTransicao"/> contra
/// <see cref="TransicoesPermitidas"/> — regra dura R4 do CLAUDE.md (docs/fiscal/arquitetura.md §2.6).
/// </summary>
public sealed class DocumentoFiscal : AggregateRoot<string>
{
    private readonly List<ItemDocumentoFiscal> _itens = new();

    private static readonly IReadOnlyDictionary<StatusDocumentoFiscal, StatusDocumentoFiscal[]> TransicoesPermitidas =
        new Dictionary<StatusDocumentoFiscal, StatusDocumentoFiscal[]>
        {
            [StatusDocumentoFiscal.Rascunho] = [StatusDocumentoFiscal.Rascunho, StatusDocumentoFiscal.BloqueadoPorConfiguracaoFiscal, StatusDocumentoFiscal.NumeroAlocado],
            [StatusDocumentoFiscal.BloqueadoPorConfiguracaoFiscal] = [StatusDocumentoFiscal.Rascunho],
            [StatusDocumentoFiscal.NumeroAlocado] = [StatusDocumentoFiscal.Autorizado, StatusDocumentoFiscal.Denegado, StatusDocumentoFiscal.Rejeitado, StatusDocumentoFiscal.Inutilizado, StatusDocumentoFiscal.EmContingencia],
            [StatusDocumentoFiscal.Rejeitado] = [StatusDocumentoFiscal.Autorizado, StatusDocumentoFiscal.Denegado, StatusDocumentoFiscal.Rejeitado, StatusDocumentoFiscal.Inutilizado],
            [StatusDocumentoFiscal.Autorizado] = [StatusDocumentoFiscal.Cancelado],
            // EmContingencia deliberadamente NÃO lista Inutilizado como destino — gap #8 de
            // emissao-mapping.md §6.2: um DANFCE já impresso é fato legal irreversível, Desistir()
            // nunca pode "voltar" um documento em contingência (a guarda é esta ausência na
            // tabela, verificada por Fsm<TStatus>.ValidarTransicao, nenhum código extra necessário).
            [StatusDocumentoFiscal.EmContingencia] = [StatusDocumentoFiscal.Autorizado, StatusDocumentoFiscal.Denegado, StatusDocumentoFiscal.Rejeitado, StatusDocumentoFiscal.EmContingencia],
        };

    public string TenantId { get; private set; } = string.Empty;
    public TipoDocumentoFiscal Tipo { get; private set; }
    public SourceRef Origem { get; private set; } = null!;
    public StatusDocumentoFiscal Status { get; private set; }
    public string? Serie { get; private set; }
    public long? Numero { get; private set; }
    public string? ChaveDeAcesso { get; private set; }
    public string? Protocolo { get; private set; }
    public IReadOnlyList<ItemDocumentoFiscal> Itens => _itens.AsReadOnly();
    public Money Total => _itens.Aggregate(Money.Zero, static (acc, i) => acc + i.Subtotal);
    public string? MotivoBloqueioOuRejeicaoOuDenegacao { get; private set; }
    public DateTimeOffset CriadoEm { get; private set; }

    private DocumentoFiscal() { }

    public static DocumentoFiscal Abrir(string tenantId, TipoDocumentoFiscal tipo, SourceRef origem)
        => new()
        {
            Id = IdGenerator.NovoId(),
            TenantId = tenantId,
            Tipo = tipo,
            Origem = origem,
            Status = StatusDocumentoFiscal.Rascunho,
            CriadoEm = DateTimeOffset.UtcNow
        };

    /// <summary>REIDRATAÇÃO a partir do banco — não valida, não levanta evento (R6).</summary>
    public static DocumentoFiscal Reconstituir(
        string id, string tenantId, TipoDocumentoFiscal tipo, SourceRef origem, StatusDocumentoFiscal status,
        string? serie, long? numero, string? chaveDeAcesso, string? protocolo, string? motivoBloqueioOuRejeicaoOuDenegacao,
        DateTimeOffset criadoEm, IReadOnlyList<ItemDocumentoFiscal> itens)
    {
        var documento = new DocumentoFiscal
        {
            Id = id,
            TenantId = tenantId,
            Tipo = tipo,
            Origem = origem,
            Status = status,
            Serie = serie,
            Numero = numero,
            ChaveDeAcesso = chaveDeAcesso,
            Protocolo = protocolo,
            MotivoBloqueioOuRejeicaoOuDenegacao = motivoBloqueioOuRejeicaoOuDenegacao,
            CriadoEm = criadoEm
        };
        documento._itens.AddRange(itens);
        return documento;
    }

    private Result Transicionar(StatusDocumentoFiscal para) =>
        Fsm<StatusDocumentoFiscal>.ValidarTransicao(Status, para, TransicoesPermitidas);

    /// <summary>Chamado pela Application com o resultado já calculado pelo
    /// <c>MotorDeCalculoTributario</c> — só valida a invariante "documento com item sem ICMS
    /// resolvido não avança" e acumula.</summary>
    public Result AdicionarItemResolvido(ItemDocumentoFiscal item)
    {
        var transicao = Transicionar(StatusDocumentoFiscal.Rascunho);
        if (transicao.Falha) return transicao;

        if (item.Tributos.All(t => t.Tipo != TipoTributo.Icms))
            return Result.Falhar(new Error("fiscal.item.icms_nao_resolvido",
                $"Item '{item.ProdutoId}' (NCM {item.Ncm}) não tem ICMS resolvido — configure PerfilFiscalNCM/TributacaoProduto antes de emitir."));

        _itens.Add(item);
        Status = StatusDocumentoFiscal.Rascunho;
        return Result.Ok();
    }

    /// <summary>Chamado pela Application quando a resolução de tributação de 1+ itens FALHOU —
    /// nunca emite com um default silencioso; bloqueia e nomeia o motivo.</summary>
    public Result Bloquear(string motivo)
    {
        var transicao = Transicionar(StatusDocumentoFiscal.BloqueadoPorConfiguracaoFiscal);
        if (transicao.Falha) return transicao;

        Status = StatusDocumentoFiscal.BloqueadoPorConfiguracaoFiscal;
        MotivoBloqueioOuRejeicaoOuDenegacao = motivo;
        return Result.Ok();
    }

    /// <summary>Volta de Bloqueado para Rascunho depois que o cadastro fiscal foi corrigido — o
    /// chamador deve tentar <see cref="AdicionarItemResolvido"/> de novo para cada item.</summary>
    public Result Desbloquear()
    {
        var transicao = Transicionar(StatusDocumentoFiscal.Rascunho);
        if (transicao.Falha) return transicao;

        Status = StatusDocumentoFiscal.Rascunho;
        MotivoBloqueioOuRejeicaoOuDenegacao = null;
        return Result.Ok();
    }

    /// <summary>Consome o próximo número da SequenciaFiscal (já alocado atomicamente pela
    /// Infrastructure) — a partir daqui o número está COMPROMETIDO, mesmo que a transmissão
    /// falhe (por isso existe <see cref="Desistir"/>, nunca "voltar" para Rascunho).</summary>
    public Result AlocarNumero(string serie, long numero)
    {
        var transicao = Transicionar(StatusDocumentoFiscal.NumeroAlocado);
        if (transicao.Falha) return transicao;
        if (_itens.Count == 0)
            return Result.Falhar(new Error("fiscal.documento.sem_itens", "Documento sem itens não pode alocar número."));

        Serie = serie;
        Numero = numero;
        Status = StatusDocumentoFiscal.NumeroAlocado;
        Raise(new NumeroFiscalAlocadoDomainEvent(Id, TenantId, Tipo, serie, numero));
        return Result.Ok();
    }

    /// <summary>Fecha o gap #8 de docs/fiscal/emissao-mapping.md §6.2 — o terminal PDV assinou o
    /// XML localmente (<c>tpEmis=9</c>) porque a rede caiu no meio do fechamento da venda e já
    /// imprimiu o DANFCE; a partir daqui o documento é um fato legal irreversível
    /// (<see cref="Desistir"/> nunca aceita <see cref="StatusDocumentoFiscal.EmContingencia"/>
    /// como origem — ausente de propósito da tabela de transições). O número já alocado antes
    /// desta chamada continua sendo o número da nota (§6.4 do mapeamento) — nenhuma renumeração
    /// aqui.</summary>
    public Result PrepararContingencia(DateTimeOffset dhCont, string justificativa)
    {
        var transicao = Transicionar(StatusDocumentoFiscal.EmContingencia);
        if (transicao.Falha) return transicao;
        if (justificativa.Length < 15)
            return Result.Falhar(new Error("fiscal.documento.justificativa_curta", "Justificativa de contingência exige ao menos 15 caracteres (layout SEFAZ)."));

        Status = StatusDocumentoFiscal.EmContingencia;
        Raise(new DocumentoFiscalEmContingenciaDomainEvent(Id, TenantId, Tipo, Serie!, Numero!.Value, dhCont, justificativa));
        return Result.Ok();
    }

    /// <summary><paramref name="protocolo"/> é o protocolo de autorização devolvido pela SEFAZ
    /// junto com a chave de acesso — persistido para que um cancelamento posterior
    /// (<see cref="Application.Ports.IGatewayCancelamentoSefaz"/>) tenha o dado exigido pelo
    /// gateway em vez de enviar protocolo vazio. Nullable porque nem todo gateway/UF devolve o
    /// protocolo (alguns só confirmam via chaveAcesso).</summary>
    public Result RegistrarAutorizacao(string chaveDeAcesso, string? protocolo, DateTimeOffset autorizadoEm)
    {
        var transicao = Transicionar(StatusDocumentoFiscal.Autorizado);
        if (transicao.Falha) return transicao;

        ChaveDeAcesso = chaveDeAcesso;
        Protocolo = protocolo;
        Status = StatusDocumentoFiscal.Autorizado;
        Raise(new DocumentoFiscalAutorizadoDomainEvent(Id, TenantId, Tipo, chaveDeAcesso, Serie!, Numero!.Value, Total, autorizadoEm));
        return Result.Ok();
    }

    public Result RegistrarDenegacao(string motivo)
    {
        var transicao = Transicionar(StatusDocumentoFiscal.Denegado);
        if (transicao.Falha) return transicao;

        Status = StatusDocumentoFiscal.Denegado;
        MotivoBloqueioOuRejeicaoOuDenegacao = motivo;
        return Result.Ok();
    }

    /// <summary>Erro de schema/preenchimento — o MESMO número pode ser reenviado depois de
    /// corrigido (número já está comprometido desde <see cref="AlocarNumero"/>, rejeição não o
    /// libera).</summary>
    public Result RegistrarRejeicao(string motivo)
    {
        var transicao = Transicionar(StatusDocumentoFiscal.Rejeitado);
        if (transicao.Falha) return transicao;

        Status = StatusDocumentoFiscal.Rejeitado;
        MotivoBloqueioOuRejeicaoOuDenegacao = motivo;
        return Result.Ok();
    }

    public Result Cancelar(string justificativa)
    {
        var transicao = Transicionar(StatusDocumentoFiscal.Cancelado);
        if (transicao.Falha) return transicao;
        if (justificativa.Length < 15)
            return Result.Falhar(new Error("fiscal.documento.justificativa_curta", "Justificativa de cancelamento exige ao menos 15 caracteres (layout SEFAZ)."));

        Status = StatusDocumentoFiscal.Cancelado;
        Raise(new DocumentoFiscalCanceladoDomainEvent(Id, TenantId, Total));
        return Result.Ok();
    }

    /// <summary>Fecha formalmente um número que foi alocado mas nunca chegou a autorizar — vira
    /// insumo do evento de Inutilização de Numeração que a Application deve protocolar na SEFAZ
    /// dentro do prazo legal (docs/fiscal/arquitetura.md §5).</summary>
    public Result Desistir(string motivo)
    {
        var transicao = Transicionar(StatusDocumentoFiscal.Inutilizado);
        if (transicao.Falha) return transicao;

        Status = StatusDocumentoFiscal.Inutilizado;
        MotivoBloqueioOuRejeicaoOuDenegacao = motivo;
        Raise(new NumeroFiscalInutilizadoDomainEvent(Id, TenantId, Tipo, Serie!, Numero!.Value, motivo));
        return Result.Ok();
    }
}
