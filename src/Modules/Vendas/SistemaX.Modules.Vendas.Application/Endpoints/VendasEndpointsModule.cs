using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Abstractions.Autorizacao;
using SistemaX.Modules.Vendas.Application.CasosDeUso;
using SistemaX.Modules.Vendas.Application.Ports;
using SistemaX.Modules.Vendas.Domain;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Vendas.Application.Endpoints;

/// <summary>DTOs de fio (wire) da Venda — nunca serializamos o agregado direto (mesmo motivo do
/// <c>ProdutoDto</c> do Estoque): a UI só precisa da posição atual do carrinho, não de invariantes
/// internas do agregado.</summary>
public sealed record ItemDeVendaDto(string Id, string ProdutoId, string Descricao, int Quantidade, Money PrecoUnitario, Money Desconto, Money Subtotal)
{
    public static ItemDeVendaDto DeDominio(ItemDeVenda item) => new(item.Id, item.ProdutoId, item.Descricao, item.Quantidade, item.PrecoUnitario, item.Desconto, item.Subtotal);
}

public sealed record PagamentoDeVendaDto(string Id, string Metodo, Money Valor, Money? ValorRecebido, Money Troco, DateTimeOffset RegistradoEm)
{
    public static PagamentoDeVendaDto DeDominio(PagamentoDeVenda pagamento) => new(
        pagamento.Id, pagamento.Metodo.ToString(), pagamento.Valor, pagamento.ValorRecebido, pagamento.Troco, pagamento.RegistradoEm);
}

public sealed record VendaDto(
    string Id,
    string Status,
    IReadOnlyList<ItemDeVendaDto> Itens,
    IReadOnlyList<PagamentoDeVendaDto> Pagamentos,
    Money DescontoVenda,
    Money SubtotalItens,
    Money Total,
    Money TotalPago,
    Money Restante,
    string? FormaPagamento,
    string? ClienteId)
{
    public static VendaDto DeDominio(Venda venda) => new(
        venda.Id,
        venda.Status.ToString(),
        venda.Itens.Select(ItemDeVendaDto.DeDominio).ToList(),
        venda.Pagamentos.Select(PagamentoDeVendaDto.DeDominio).ToList(),
        venda.DescontoVenda,
        venda.SubtotalItens,
        venda.Total,
        venda.TotalPago,
        venda.Restante,
        venda.FormaPagamento,
        venda.ClienteId);
}

public sealed record AdicionarItemRequest(string ProdutoId, string Descricao, int Quantidade, long PrecoUnitarioCentavos);

public sealed record RegistrarPagamentoRequest(string Metodo, long ValorCentavos, long? ValorRecebidoCentavos = null);

/// <summary><c>ClienteId: null</c> (ou string vazia) remove o cliente já vinculado ao carrinho.</summary>
public sealed record DefinirClienteRequest(string? ClienteId);

/// <summary>
/// Terceiro <see cref="IModule"/> do Vendas — implementa <see cref="IModuleEndpoints"/> no mesmo
/// espírito de <c>EstoqueEndpointsModule</c>/<c>FinanceiroEndpointsModule</c> (F1a). F1c: fecha o
/// PDV via HTTP — abrir venda, montar carrinho, registrar pagamento e concluir, o fluxo completo
/// que a UI React dirige (ver scratchpad/design/sistemax-production-plano.md F1).
///
/// R1 reforçado aqui: <see cref="IVendaRepository.ObterPorIdAsync"/> não filtra por tenant (o port
/// só busca por id — ver a nota na própria interface). Todo endpoint que recebe um <c>vendaId</c>
/// do cliente busca a venda e confere <c>venda.TenantId == businessId da sessão</c> ANTES de
/// mutar/ler — devolve 404 (nunca 403) para não revelar que o id existe noutro tenant.
/// </summary>
public sealed class VendasEndpointsModule : IModule, IModuleEndpoints
{
    public string Codigo => "vendas.endpoints";
    public string Nome => "Vendas — Endpoints HTTP";
    public IReadOnlyCollection<string> DependeDe => ["vendas"];

    public void Registrar(IServiceCollection services, IModuleContext contexto)
    {
        // Sem registro de serviço — só rotas, ver MapearEndpoints.
    }

    public void MapearEndpoints(IEndpointRouteBuilder api)
    {
        // POST /api/vendas — abre o carrinho. BusinessId sempre da sessão (R1).
        api.MapPost("/vendas", async (
            HttpContext http,
            IniciarVendaUseCase useCase,
            CancellationToken ct) =>
        {
            var businessId = http.ObterBusinessId();
            var resultado = await useCase.ExecutarAsync(businessId, ct).ConfigureAwait(false);
            return resultado.Sucesso
                ? Results.Ok(VendaDto.DeDominio(resultado.Valor))
                : resultado.Erro.ParaRespostaHttp();
        }).RequerPermissao(Modulo.Vendas, Acao.Editar);

        // GET /api/vendas/{id} — estado atual do carrinho (refresh de UI/recuperação de crash).
        api.MapGet("/vendas/{id}", async (
            HttpContext http,
            string id,
            IVendaRepository vendas,
            CancellationToken ct) =>
        {
            var businessId = http.ObterBusinessId();
            var venda = await ObterDoTenantAsync(vendas, id, businessId, ct).ConfigureAwait(false);
            return venda is null ? Results.NotFound() : Results.Ok(VendaDto.DeDominio(venda));
        }).RequerPermissao(Modulo.Vendas, Acao.Ver);

        // POST /api/vendas/{id}/itens — adiciona uma linha ao carrinho em montagem.
        api.MapPost("/vendas/{id}/itens", async (
            HttpContext http,
            string id,
            AdicionarItemRequest corpo,
            IVendaRepository vendas,
            MontarVendaUseCase useCase,
            CancellationToken ct) =>
        {
            var businessId = http.ObterBusinessId();
            if (await ObterDoTenantAsync(vendas, id, businessId, ct).ConfigureAwait(false) is null)
                return Results.NotFound();

            var resultado = await useCase.AdicionarItemAsync(
                id, corpo.ProdutoId, corpo.Descricao, corpo.Quantidade, new Money(corpo.PrecoUnitarioCentavos), ct).ConfigureAwait(false);

            if (resultado.Falha) return resultado.Erro.ParaRespostaHttp();

            var atualizada = await vendas.ObterPorIdAsync(id, ct).ConfigureAwait(false);
            return Results.Ok(VendaDto.DeDominio(atualizada!));
        }).RequerPermissao(Modulo.Vendas, Acao.Editar);

        // POST /api/vendas/{id}/cliente — vincula (ou remove, com ClienteId nulo/vazio) o cliente
        // do carrinho. Companion dimensional da F0 do plano de inteligência do Financeiro (ver
        // Venda.ClienteId/VendaConcluida.ClienteId).
        api.MapPost("/vendas/{id}/cliente", async (
            HttpContext http,
            string id,
            DefinirClienteRequest corpo,
            IVendaRepository vendas,
            MontarVendaUseCase useCase,
            CancellationToken ct) =>
        {
            var businessId = http.ObterBusinessId();
            if (await ObterDoTenantAsync(vendas, id, businessId, ct).ConfigureAwait(false) is null)
                return Results.NotFound();

            var resultado = await useCase.DefinirClienteAsync(id, corpo.ClienteId, ct).ConfigureAwait(false);
            if (resultado.Falha) return resultado.Erro.ParaRespostaHttp();

            var atualizada = await vendas.ObterPorIdAsync(id, ct).ConfigureAwait(false);
            return Results.Ok(VendaDto.DeDominio(atualizada!));
        }).RequerPermissao(Modulo.Vendas, Acao.Editar);

        // POST /api/vendas/{id}/pagamentos — registra uma linha de pagamento (split natural).
        api.MapPost("/vendas/{id}/pagamentos", async (
            HttpContext http,
            string id,
            RegistrarPagamentoRequest corpo,
            IVendaRepository vendas,
            MontarVendaUseCase useCase,
            CancellationToken ct) =>
        {
            var businessId = http.ObterBusinessId();
            if (await ObterDoTenantAsync(vendas, id, businessId, ct).ConfigureAwait(false) is null)
                return Results.NotFound();

            if (!Enum.TryParse<MetodoPagamento>(corpo.Metodo, ignoreCase: true, out var metodo))
                return new Error("venda.pagamento.metodo_invalido", $"Método de pagamento '{corpo.Metodo}' desconhecido.").ParaRespostaHttp();

            var valorRecebido = corpo.ValorRecebidoCentavos is { } centavos ? new Money(centavos) : (Money?)null;
            var resultado = await useCase.RegistrarPagamentoAsync(
                id, metodo, new Money(corpo.ValorCentavos), valorRecebido, DateTimeOffset.UtcNow, ct).ConfigureAwait(false);

            if (resultado.Falha) return resultado.Erro.ParaRespostaHttp();

            var atualizada = await vendas.ObterPorIdAsync(id, ct).ConfigureAwait(false);
            return Results.Ok(VendaDto.DeDominio(atualizada!));
        }).RequerPermissao(Modulo.Vendas, Acao.Editar);

        // POST /api/vendas/{id}/concluir — fecha a venda; publica VendaConcluida (síncrono, ver
        // InProcessIntegrationEventBus) — quando este handler retorna, o Financeiro já criou
        // ContaAReceber/MovimentoFinanceiro (F1c: é isso que faz "vende → aparece no financeiro"
        // funcionar na mesma volta de HTTP, sem polling).
        api.MapPost("/vendas/{id}/concluir", async (
            HttpContext http,
            string id,
            IVendaRepository vendas,
            ConcluirVendaUseCase useCase,
            CancellationToken ct) =>
        {
            var businessId = http.ObterBusinessId();
            if (await ObterDoTenantAsync(vendas, id, businessId, ct).ConfigureAwait(false) is null)
                return Results.NotFound();

            var resultado = await useCase.ExecutarAsync(id, ct).ConfigureAwait(false);
            return resultado.Sucesso
                ? Results.Ok(VendaDto.DeDominio(resultado.Valor))
                : resultado.Erro.ParaRespostaHttp();
        }).RequerPermissao(Modulo.Vendas, Acao.Editar);
    }

    private static async Task<Venda?> ObterDoTenantAsync(IVendaRepository vendas, string vendaId, string businessId, CancellationToken ct)
    {
        var venda = await vendas.ObterPorIdAsync(vendaId, ct).ConfigureAwait(false);
        return venda is not null && venda.TenantId == businessId ? venda : null;
    }
}
