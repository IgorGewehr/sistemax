using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Abstractions.Autorizacao;
using SistemaX.Modules.Estoque.Application.CasosDeUso;
using SistemaX.Modules.Estoque.Application.Ports;
using SistemaX.Modules.Estoque.Application.ReadModels;
using SistemaX.Modules.Estoque.Domain.Catalogo;
using SistemaX.Modules.Estoque.Domain.Comum;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Estoque.Application.Endpoints;

/// <summary>DTO de fio (wire) do catálogo — nunca serializamos o agregado <see cref="Produto"/>
/// direto (vazaria invariantes internas de ficha técnica/código de barras que a F1a não precisa
/// expor ainda). Dinheiro sempre <see cref="Money"/> (centavos-inteiros — R1 estendido ao
/// contrato, ver plano de produção).</summary>
public sealed record ProdutoDto(
    string Id,
    string Sku,
    string Nome,
    string? Categoria,
    string Unidade,
    Money PrecoVenda,
    bool ControlaEstoque,
    bool Ativo)
{
    public static ProdutoDto DeDominio(Produto produto) => new(
        produto.Id,
        produto.Sku,
        produto.Nome,
        produto.Categoria,
        produto.Unidade.ToString(),
        produto.PrecoVenda,
        produto.ControlaEstoque,
        produto.Ativo);
}

/// <summary>Corpo de <c>POST /api/estoque/produtos</c> — F1c (cadastro real pela UI, ver
/// docs/arquitetura/bridge-http-local.md). <see cref="PrecoVendaCentavos"/> e
/// <see cref="EstoqueMinimoMilesimos"/> chegam já na unidade inteira do domínio (centavos/milésimos)
/// para não introduzir ponto flutuante no boundary — a UI converte reais→centavos antes de
/// chamar, nunca o servidor.</summary>
public sealed record CriarProdutoRequest(
    string Nome,
    string Unidade,
    string? Sku = null,
    long PrecoVendaCentavos = 0,
    string? Categoria = null,
    bool ControlaEstoque = true,
    long? EstoqueMinimoMilesimos = null);

/// <summary>
/// Terceiro <see cref="IModule"/> do Estoque — existe só para implementar
/// <see cref="IModuleEndpoints"/>, no mesmo espírito de <see cref="Financeiro.Application.Endpoints.FinanceiroEndpointsModule"/>.
/// Prova que a enumeração <c>IModuleEndpoints</c> do Host não é fiação de um caso só — dois
/// módulos plugam endpoints pelo MESMO mecanismo, zero <c>if</c> no Host sobre qual módulo é qual.
///
/// F1c adiciona o cadastro de produto (<c>POST /produtos</c>) e a posição de saldo
/// (<c>GET /saldos</c>) — as duas peças que faltavam para o fluxo "cadastra produto → vende →
/// aparece no financeiro" fechar pela UI, não só por curl (ver
/// scratchpad/design/sistemax-production-plano.md F1).
/// </summary>
public sealed class EstoqueEndpointsModule : IModule, IModuleEndpoints
{
    public string Codigo => "estoque.endpoints";
    public string Nome => "Estoque — Endpoints HTTP";
    public IReadOnlyCollection<string> DependeDe => ["estoque"];

    public void Registrar(IServiceCollection services, IModuleContext contexto)
    {
        // Sem registro de serviço — só rotas, ver MapearEndpoints.
    }

    public void MapearEndpoints(IEndpointRouteBuilder api)
    {
        // GET /api/estoque/produtos — catálogo do tenant da sessão (R1).
        api.MapGet("/estoque/produtos", async (
            HttpContext http,
            IProdutoRepository repositorio,
            CancellationToken ct) =>
        {
            var businessId = http.ObterBusinessId();
            var produtos = await repositorio.ListarAsync(businessId, ct).ConfigureAwait(false);
            return Results.Ok(produtos.Select(ProdutoDto.DeDominio));
        }).RequerPermissao(Modulo.Estoque, Acao.Ver);

        // POST /api/estoque/produtos — cadastro real (F1c). BusinessId SEMPRE da sessão (R1) —
        // o corpo nunca carrega tenant.
        api.MapPost("/estoque/produtos", async (
            HttpContext http,
            CriarProdutoRequest corpo,
            CriarProdutoUseCase useCase,
            CancellationToken ct) =>
        {
            var businessId = http.ObterBusinessId();

            if (!Enum.TryParse<UnidadeDeMedida>(corpo.Unidade, ignoreCase: true, out var unidade))
            {
                return new Error("estoque.produto.unidade_invalida", $"Unidade '{corpo.Unidade}' desconhecida.")
                    .ParaRespostaHttp();
            }

            var resultado = await useCase.ExecutarAsync(
                businessId,
                corpo.Nome,
                unidade,
                sku: corpo.Sku,
                precoVenda: new Money(corpo.PrecoVendaCentavos),
                categoria: corpo.Categoria,
                controlaEstoque: corpo.ControlaEstoque,
                estoqueMinimo: corpo.EstoqueMinimoMilesimos is { } milesimos ? new Quantidade(milesimos) : null,
                ct: ct).ConfigureAwait(false);

            return resultado.Sucesso
                ? Results.Ok(ProdutoDto.DeDominio(resultado.Valor))
                : resultado.Erro.ParaRespostaHttp();
        }).RequerPermissao(Modulo.Estoque, Acao.Editar);

        // GET /api/estoque/saldos — posição atual (SaldoAtualService), o que alimenta a lista de
        // Produtos + o KPI "valor em estoque" da tela.
        api.MapGet("/estoque/saldos", async (
            HttpContext http,
            SaldoAtualService servico,
            CancellationToken ct) =>
        {
            var businessId = http.ObterBusinessId();
            var posicoes = await servico.ObterPosicaoAsync(businessId, ct).ConfigureAwait(false);
            return Results.Ok(posicoes);
        }).RequerPermissao(Modulo.Estoque, Acao.Ver);

        // LENTE VERTICAL VAREJO (opt-in — só faz sentido pra quem tem catálogo/giro de produto;
        // CurvaAbcService/GiroDeEstoqueService/RupturaService já existiam registrados em
        // EstoqueModule, prontos e testados, só sem superfície HTTP até aqui). Nenhum dado novo:
        // as 3 lentes reusam só Produto+MovimentoDeEstoque+SaldoDeItem já existentes — read-model
        // puro, gate Estoque.Ver (mesma permissão do catálogo, não uma nova).

        // GET /api/estoque/analises/curva-abc — classe A/B/C por VALOR DE CUSTO baixado (corte
        // 80/95 clássico) no período — "o que concentra o giro" / "o que é capital parado (C)".
        api.MapGet("/estoque/analises/curva-abc", async (
            HttpContext http,
            CurvaAbcService servico,
            CancellationToken ct,
            DateTimeOffset? de = null,
            DateTimeOffset? ate = null) =>
        {
            var businessId = http.ObterBusinessId();
            var (desde, ateQuando) = ResolverPeriodo(de, ate);
            var curva = await servico.ClassificarAsync(businessId, desde, ateQuando, ct).ConfigureAwait(false);
            return Results.Ok(curva);
        }).RequerPermissao(Modulo.Estoque, Acao.Ver);

        // GET /api/estoque/analises/giro — giro anualizado (CMV do período ÷ valor imobilizado
        // atual) + cobertura em dias (disponível ÷ consumo médio diário), ordenado do mais parado
        // pro mais girado — é o "isso aqui é dinheiro parado na prateleira?" do dono de loja.
        api.MapGet("/estoque/analises/giro", async (
            HttpContext http,
            GiroDeEstoqueService servico,
            CancellationToken ct,
            DateTimeOffset? de = null,
            DateTimeOffset? ate = null) =>
        {
            var businessId = http.ObterBusinessId();
            var (desde, ateQuando) = ResolverPeriodo(de, ate);
            var linhas = await servico.CalcularAsync(businessId, desde, ateQuando, ct).ConfigureAwait(false);
            return Results.Ok(linhas);
        }).RequerPermissao(Modulo.Estoque, Acao.Ver);

        // GET /api/estoque/analises/ruptura — dias com saldo disponível ≤ 0 dentro da janela +
        // venda perdida estimada (consumo médio × dias em ruptura × preço de catálogo).
        api.MapGet("/estoque/analises/ruptura", async (
            HttpContext http,
            RupturaService servico,
            CancellationToken ct,
            DateTimeOffset? de = null,
            DateTimeOffset? ate = null) =>
        {
            var businessId = http.ObterBusinessId();
            var (desde, ateQuando) = ResolverPeriodo(de, ate);
            var linhas = await servico.AnalisarAsync(businessId, desde, ateQuando, ct).ConfigureAwait(false);
            return Results.Ok(linhas);
        }).RequerPermissao(Modulo.Estoque, Acao.Ver);
    }

    /// <summary>Default de 30 dias terminando agora quando <paramref name="de"/>/<paramref name="ate"/>
    /// não vêm na query string — mesmo horizonte-padrão das rotas <c>fato-*</c> do Financeiro
    /// (<c>FinanceiroEndpointsModule.ResolverPeriodo</c>), só que em <see cref="DateTimeOffset"/>
    /// porque é isso que os 3 read-models de análise (Razão de estoque) recebem.</summary>
    private static (DateTimeOffset De, DateTimeOffset Ate) ResolverPeriodo(DateTimeOffset? de, DateTimeOffset? ate)
    {
        var agora = DateTimeOffset.UtcNow;
        var ateResolvido = ate ?? agora;
        var deResolvido = de ?? ateResolvido.AddDays(-30);
        return (deResolvido, ateResolvido);
    }
}
