using SistemaX.Modules.Estoque.Domain.Comum;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Estoque.Domain.Catalogo;

/// <summary>
/// Natureza da operação do produto — fecha o gap de CFOP identificado em
/// docs/fiscal/arquitetura.md §2.3/§9: <c>5101</c>/<c>6101</c> (produção própria) vs
/// <c>5102</c>/<c>6102</c> (revenda de terceiros) têm o mesmo tipo de operação/UF/contribuinte;
/// só este atributo do PRODUTO distingue os dois. Decisão de Igor (ADR-0002 do Fiscal): vive
/// aqui, no cadastro do produto — não no módulo Fiscal — porque é fato intrínseco do item
/// (como ele é sourced), propagado ao Fiscal via <c>ProdutoFiscalAtualizado</c> (evento de
/// integração), nunca lido por chamada síncrona cross-módulo.
/// </summary>
public enum NaturezaOperacaoProduto
{
    ProducaoPropria,
    RevendaDeTerceiros,
    ImportacaoPropria
}

/// <summary>Código estável de wire (evento de integração <c>ProdutoFiscalAtualizado</c>/coluna
/// SQLite) — nunca o ordinal do enum, que não é garantia de estabilidade entre módulos
/// versionados independentemente (o Fiscal mantém sua PRÓPRIA cópia deste enum e usa o MESMO
/// código para o lado dele — ver <c>SistemaX.Modules.Fiscal.Domain.Produtos.NaturezaOperacaoProdutoExtensions</c>).</summary>
public static class NaturezaOperacaoProdutoExtensions
{
    public const string CodigoProducaoPropria = "producao_propria";
    public const string CodigoRevendaDeTerceiros = "revenda_terceiros";
    public const string CodigoImportacaoPropria = "importacao_propria";

    public static string ParaCodigo(this NaturezaOperacaoProduto natureza) => natureza switch
    {
        NaturezaOperacaoProduto.ProducaoPropria => CodigoProducaoPropria,
        NaturezaOperacaoProduto.ImportacaoPropria => CodigoImportacaoPropria,
        _ => CodigoRevendaDeTerceiros
    };

    public static NaturezaOperacaoProduto DeCodigo(string? codigo) => codigo switch
    {
        CodigoProducaoPropria => NaturezaOperacaoProduto.ProducaoPropria,
        CodigoImportacaoPropria => NaturezaOperacaoProduto.ImportacaoPropria,
        _ => NaturezaOperacaoProduto.RevendaDeTerceiros
    };
}

/// <summary>Campos fiscais mínimos que a NFC-e/NF-e precisa referenciar a partir do item de
/// estoque. Mantido leve de propósito — o dono do CÁLCULO fiscal em si é o módulo Fiscal;
/// <see cref="CfopOverride"/> é a única exceção — um override PONTUAL de CFOP por produto, que
/// vence sobre o CFOP padrão configurável do Fiscal e perde só para um override explícito na
/// emissão (cadeia decidida por Igor: emissão &gt; produto &gt; padrão-config).</summary>
public sealed record DadosFiscaisProduto(
    string? Ncm = null,
    string? Cest = null,
    NaturezaOperacaoProduto NaturezaOperacao = NaturezaOperacaoProduto.RevendaDeTerceiros,
    string? CfopOverride = null);

/// <summary>
/// Catálogo — SKU, códigos de barras, unidade, fiscal, limiares de reposição, localização e ficha
/// técnica (BOM). NÃO guarda saldo (isso é <c>SaldoDeItem</c>, read-model derivado do razão) —
/// separar as duas coisas é o que evita o bug clássico de "campo <c>currentStock</c> editável no
/// próprio doc do produto" (fonte de drift entre dois caixas concorrentes).
/// </summary>
public sealed class Produto : AggregateRoot<string>
{
    private readonly List<CodigoDeBarras> _codigosDeBarras = [];
    private readonly List<ComponenteDeFicha> _fichaTecnica = [];

    public string TenantId { get; private set; } = string.Empty;
    public string Sku { get; private set; } = string.Empty;
    public IReadOnlyList<CodigoDeBarras> CodigosDeBarras => _codigosDeBarras.AsReadOnly();
    public string Nome { get; private set; } = string.Empty;
    public string? Descricao { get; private set; }
    public string? Categoria { get; private set; }
    public UnidadeDeMedida Unidade { get; private set; }
    public Money PrecoVenda { get; private set; } = Money.Zero;
    public DadosFiscaisProduto Fiscal { get; private set; } = new();

    /// <summary>Limiar de alerta (<c>EstoqueAbaixoDoMinimo</c> dispara na transição pra baixo dele).</summary>
    public Quantidade EstoqueMinimo { get; private set; } = Quantidade.Zero;

    /// <summary>Quando preenchido, é o gatilho da Sugestão de Compra — dispara ANTES do mínimo
    /// (considera o lead time do fornecedor, então repõe antes de ficar crítico).</summary>
    public Quantidade? PontoDeReposicao { get; private set; }

    public Quantidade? LoteEconomico { get; private set; }
    public int? LeadTimeDias { get; private set; }
    public string? Localizacao { get; private set; }

    /// <summary><c>false</c> = serviço/taxa: nunca gera movimento de estoque.</summary>
    public bool ControlaEstoque { get; private set; } = true;

    public bool ControlePorLote { get; private set; }
    public PoliticaDeValorizacao Valorizacao { get; private set; } = PoliticaDeValorizacao.CustoMedio;

    /// <summary>Ficha técnica (BOM). Quando não-vazia, a baixa NUNCA atinge este produto
    /// diretamente — os handlers expandem nos insumos-folha (<c>ExpansorDeFichaTecnica</c>).</summary>
    public IReadOnlyList<ComponenteDeFicha> FichaTecnica => _fichaTecnica.AsReadOnly();

    public bool Ativo { get; private set; } = true;

    private Produto()
    {
    }

    public static Result<Produto> Criar(
        string tenantId,
        string nome,
        UnidadeDeMedida unidade,
        string? sku = null,
        Money? precoVenda = null,
        string? categoria = null,
        string? descricao = null,
        DadosFiscaisProduto? fiscal = null,
        Quantidade? estoqueMinimo = null,
        Quantidade? pontoDeReposicao = null,
        Quantidade? loteEconomico = null,
        int? leadTimeDias = null,
        string? localizacao = null,
        bool controlaEstoque = true,
        bool controlePorLote = false,
        PoliticaDeValorizacao valorizacao = PoliticaDeValorizacao.CustoMedio,
        IReadOnlyList<ComponenteDeFicha>? fichaTecnica = null,
        IReadOnlyList<CodigoDeBarras>? codigosDeBarras = null)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            return Result.Falhar<Produto>(new Error("estoque.produto.tenant_obrigatorio", "TenantId é obrigatório para criar um produto."));

        if (string.IsNullOrWhiteSpace(nome))
            return Result.Falhar<Produto>(new Error("estoque.produto.nome_obrigatorio", "Nome do produto é obrigatório."));

        var ficha = fichaTecnica ?? [];
        if (ficha.Select(c => c.ProdutoInsumoId).Distinct().Count() != ficha.Count)
            return Result.Falhar<Produto>(new Error("estoque.produto.ficha_duplicada", "Ficha técnica não pode repetir o mesmo insumo em duas linhas."));

        if (ficha.Any(c => !c.Quantidade.EhPositiva))
            return Result.Falhar<Produto>(new Error("estoque.produto.ficha_quantidade_invalida", "Quantidade de insumo na ficha técnica deve ser positiva."));

        if (estoqueMinimo is { EhNegativa: true })
            return Result.Falhar<Produto>(new Error("estoque.produto.minimo_negativo", "Estoque mínimo não pode ser negativo."));

        var produto = new Produto
        {
            Id = IdGenerator.NovoId(),
            TenantId = tenantId,
            Sku = string.IsNullOrWhiteSpace(sku) ? GerarSkuPadrao() : sku,
            Nome = nome,
            Descricao = descricao,
            Categoria = categoria,
            Unidade = unidade,
            PrecoVenda = precoVenda ?? Money.Zero,
            Fiscal = fiscal ?? new DadosFiscaisProduto(),
            EstoqueMinimo = estoqueMinimo ?? Quantidade.Zero,
            PontoDeReposicao = pontoDeReposicao,
            LoteEconomico = loteEconomico,
            LeadTimeDias = leadTimeDias,
            Localizacao = localizacao,
            ControlaEstoque = controlaEstoque,
            ControlePorLote = controlePorLote,
            Valorizacao = valorizacao,
            Ativo = true
        };

        produto._fichaTecnica.AddRange(ficha);
        if (codigosDeBarras is not null)
            produto._codigosDeBarras.AddRange(codigosDeBarras);

        return Result.Ok(produto);
    }

    /// <summary>
    /// Atualiza NCM/CEST/natureza-da-operação/CFOP-override do produto — o gesto que faltava
    /// (gap documentado em docs/fiscal/arquitetura.md §4: "Produto.cs só define Fiscal no
    /// construtor"). Mutação simples, sem FSM (mesmo gesto de <see cref="Ativar"/>/<see cref="Inativar"/>);
    /// quem chama (Application) publica <c>ProdutoFiscalAtualizado</c> depois de salvar — o Fiscal
    /// nunca lê este campo por chamada síncrona, só via evento de integração + cache local.
    /// </summary>
    public Result AtualizarDadosFiscais(DadosFiscaisProduto fiscal)
    {
        Fiscal = fiscal;
        return Result.Ok();
    }

    public Result Inativar()
    {
        Ativo = false;
        return Result.Ok();
    }

    public Result Ativar()
    {
        Ativo = true;
        return Result.Ok();
    }

    public Result AdicionarCodigoDeBarras(CodigoDeBarras codigo)
    {
        if (_codigosDeBarras.Any(c => c.Valor == codigo.Valor))
            return Result.Falhar(new Error("estoque.produto.codigo_duplicado", $"Código de barras '{codigo.Valor}' já está cadastrado neste produto."));

        _codigosDeBarras.Add(codigo);
        return Result.Ok();
    }

    /// <summary>REIDRATAÇÃO a partir do banco — não valida, não levanta evento (R6).</summary>
    public static Produto Reconstituir(
        string id, string tenantId, string sku, string nome, string? descricao, string? categoria,
        UnidadeDeMedida unidade, Money precoVenda, DadosFiscaisProduto fiscal, Quantidade estoqueMinimo,
        Quantidade? pontoDeReposicao, Quantidade? loteEconomico, int? leadTimeDias, string? localizacao,
        bool controlaEstoque, bool controlePorLote, PoliticaDeValorizacao valorizacao, bool ativo,
        IReadOnlyList<CodigoDeBarras> codigosDeBarras, IReadOnlyList<ComponenteDeFicha> fichaTecnica)
    {
        var produto = new Produto
        {
            Id = id,
            TenantId = tenantId,
            Sku = sku,
            Nome = nome,
            Descricao = descricao,
            Categoria = categoria,
            Unidade = unidade,
            PrecoVenda = precoVenda,
            Fiscal = fiscal,
            EstoqueMinimo = estoqueMinimo,
            PontoDeReposicao = pontoDeReposicao,
            LoteEconomico = loteEconomico,
            LeadTimeDias = leadTimeDias,
            Localizacao = localizacao,
            ControlaEstoque = controlaEstoque,
            ControlePorLote = controlePorLote,
            Valorizacao = valorizacao,
            Ativo = ativo
        };
        produto._codigosDeBarras.AddRange(codigosDeBarras);
        produto._fichaTecnica.AddRange(fichaTecnica);
        return produto;
    }

    /// <summary>Últimos 8 chars do ULID (não os primeiros): num ULID, os primeiros ~10 chars são
    /// timestamp — dois produtos criados no mesmo milissegundo sem SKU explícito colidiriam
    /// (visto na prática: <c>DemoSeeder</c>/F1c criando vários produtos em sequência rápida
    /// disparava <c>UNIQUE constraint failed: produtos.tenant_id, produtos.sku</c> no SQLite). Os
    /// últimos 8 chars vivem inteiramente na porção de 80 bits de aleatoriedade do ULID.</summary>
    private static string GerarSkuPadrao() => $"SKU-{IdGenerator.NovoId()[^8..].ToUpperInvariant()}";
}
