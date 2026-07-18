using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Abstractions.Autorizacao;
using SistemaX.Modules.Compras.Application.CasosDeUso;
using SistemaX.Modules.Compras.Application.Ports;
using SistemaX.Modules.Compras.Domain.Comum;
using SistemaX.Modules.Compras.Domain.Fornecedores;
using SistemaX.Modules.Compras.Domain.Notas;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Compras.Application.Endpoints;

public sealed record FornecedorDto(string Id, string? Documento, string RazaoSocial, string? NomeFantasia, string Status)
{
    public static FornecedorDto DeDominio(Fornecedor f) => new(f.Id, f.Documento, f.RazaoSocial, f.NomeFantasia, f.Status.ToString());
}

public sealed record CadastrarFornecedorRequest(string RazaoSocial, string? Documento = null, string? NomeFantasia = null);

public sealed record ItemDeNotaDeCompraDto(
    int NItem, string? CProd, string DescricaoNf, string? Ncm, string UnidadeNf, Quantidade QuantidadeNf,
    Money VProd, Money VDesc, string MatchState, string? ProdutoId, Money? CustoTotalEntrada)
{
    public static ItemDeNotaDeCompraDto DeDominio(ItemDeNotaDeCompra i) =>
        new(i.NItem, i.CProd, i.DescricaoNf, i.Ncm, i.UnidadeNf, i.QuantidadeNf, i.VProd, i.VDesc, i.MatchState.ToString(), i.ProdutoId, i.CustoTotalEntrada);
}

public sealed record NotaDeCompraResumoDto(
    string Id, string? FornecedorId, string Origem, string Numero, string Serie, DateTimeOffset DataEmissao,
    string Status, Money VNf, int QuantidadeItens)
{
    public static NotaDeCompraResumoDto DeDominio(NotaDeCompra n) =>
        new(n.Id, n.FornecedorId, n.Origem.ToString(), n.Numero, n.Serie, n.DataEmissao, n.Status.ToString(), n.Totais.VNf, n.Itens.Count);
}

public sealed record NotaDeCompraDetalheDto(
    string Id, string? FornecedorId, string Origem, string? ChaveDeAcesso, string Numero, string Serie,
    DateTimeOffset DataEmissao, string Status, Money VNf, DateTimeOffset? RecebidaEm, string? RecebidaPorNome,
    string? MotivoDescarte, IReadOnlyList<ItemDeNotaDeCompraDto> Itens)
{
    public static NotaDeCompraDetalheDto DeDominio(NotaDeCompra n) => new(
        n.Id, n.FornecedorId, n.Origem.ToString(), n.ChaveDeAcesso?.Valor, n.Numero, n.Serie, n.DataEmissao,
        n.Status.ToString(), n.Totais.VNf, n.RecebidaEm, n.RecebidaPorNome, n.MotivoDescarte,
        n.Itens.Select(ItemDeNotaDeCompraDto.DeDominio).ToList());
}

public sealed record ItemDeEntradaRequest(
    int NItem, string? CProd, string DescricaoNf, string? Ncm, string UnidadeNf, long QuantidadeNfMilesimos,
    long VProdCentavos, long VDescCentavos = 0, long? VFreteItemCentavos = null, long? VSegItemCentavos = null,
    long? VOutroItemCentavos = null, long VIpiCentavos = 0, long VIcmsStCentavos = 0, string? LoteFornecedor = null,
    DateOnly? Validade = null, string? ProdutoIdConhecido = null, long? FatorConversaoConhecidoMilesimos = null);

public sealed record RegistrarEntradaDeNotaRequest(
    string LojaId, string Origem, string Numero, string Serie, DateTimeOffset DataEmissao,
    string? FornecedorId, string? ChaveDeAcessoBruta, long VProdCentavos, long VNfCentavos,
    IReadOnlyList<ItemDeEntradaRequest> Itens, long VFreteCentavos = 0, long VSeguroCentavos = 0,
    long VOutroCentavos = 0, long VDescontoCentavos = 0, long VStCentavos = 0, long VIpiCentavos = 0);

public sealed record ResolverMatchDeItemRequest(int NItem, string ProdutoId, long FatorConversaoMilesimos);

public sealed record IgnorarItemRequest(int NItem, string Motivo);

public sealed record ConfirmarRecebimentoRequest(string UsuarioId, string UsuarioNome);

public sealed record EstornarRecebimentoRequest(string UsuarioId, string UsuarioNome);

public sealed record DescartarNotaRequest(string Motivo);

/// <summary>
/// Endpoints HTTP do módulo Compras — achado de auditoria (guard-rail em <c>SistemaXHost.cs</c>):
/// Compras já existia por inteiro (fornecedores + pipeline de importação/conferência/recebimento
/// de nota) sem NENHUMA rota HTTP. Fecha a listagem de fornecedores/notas (read-model que
/// faltava) e as mutações do pipeline já implementadas em Application, reusando os casos de uso
/// existentes — nenhum domínio novo.
/// </summary>
public sealed class ComprasEndpointsModule : IModule, IModuleEndpoints
{
    public string Codigo => "compras.endpoints";
    public string Nome => "Compras — Endpoints HTTP";
    public IReadOnlyCollection<string> DependeDe => ["compras"];

    public void Registrar(IServiceCollection services, IModuleContext contexto)
    {
        // Sem registro de serviço — só rotas, ver MapearEndpoints.
    }

    public void MapearEndpoints(IEndpointRouteBuilder api)
    {
        // ---------------------------------------------------------------- Fornecedores

        api.MapGet("/compras/fornecedores", async (
            HttpContext http, IFornecedorRepository fornecedores, CancellationToken ct) =>
        {
            var businessId = http.ObterBusinessId();
            var lista = await fornecedores.ListarAsync(businessId, ct).ConfigureAwait(false);
            return Results.Ok(lista.Select(FornecedorDto.DeDominio));
        }).RequerPermissao(Modulo.Compras, Acao.Ver);

        // POST /api/compras/fornecedores — idempotente por documento não-vazio (R3: dedupe já
        // vive em CadastrarFornecedorUseCase, reenviar o mesmo documento devolve o existente).
        api.MapPost("/compras/fornecedores", async (
            HttpContext http, CadastrarFornecedorRequest corpo, CadastrarFornecedorUseCase useCase, CancellationToken ct) =>
        {
            var businessId = http.ObterBusinessId();
            var resultado = await useCase.ExecutarAsync(businessId, corpo.RazaoSocial, corpo.Documento, corpo.NomeFantasia, ct).ConfigureAwait(false);
            return resultado.Sucesso ? Results.Ok(FornecedorDto.DeDominio(resultado.Valor)) : resultado.Erro.ParaRespostaHttp();
        }).RequerPermissao(Modulo.Compras, Acao.Editar);

        api.MapPost("/compras/fornecedores/{id}/bloquear", async (
            HttpContext http, string id, IFornecedorRepository fornecedores, GerenciarFornecedorUseCase useCase, CancellationToken ct) =>
            await MutarFornecedorAsync(http, id, fornecedores, useCase.BloquearAsync, ct)
        ).RequerPermissao(Modulo.Compras, Acao.Editar);

        api.MapPost("/compras/fornecedores/{id}/reativar", async (
            HttpContext http, string id, IFornecedorRepository fornecedores, GerenciarFornecedorUseCase useCase, CancellationToken ct) =>
            await MutarFornecedorAsync(http, id, fornecedores, useCase.ReativarAsync, ct)
        ).RequerPermissao(Modulo.Compras, Acao.Editar);

        api.MapPost("/compras/fornecedores/{id}/inativar", async (
            HttpContext http, string id, IFornecedorRepository fornecedores, GerenciarFornecedorUseCase useCase, CancellationToken ct) =>
            await MutarFornecedorAsync(http, id, fornecedores, useCase.InativarAsync, ct)
        ).RequerPermissao(Modulo.Compras, Acao.Editar);

        // ---------------------------------------------------------------- Notas de Compra

        api.MapGet("/compras/notas", async (
            HttpContext http, INotaDeCompraRepository notas, CancellationToken ct) =>
        {
            var businessId = http.ObterBusinessId();
            var lista = await notas.ListarAsync(businessId, ct).ConfigureAwait(false);
            return Results.Ok(lista.Select(NotaDeCompraResumoDto.DeDominio));
        }).RequerPermissao(Modulo.Compras, Acao.Ver);

        api.MapGet("/compras/notas/{id}", async (
            HttpContext http, string id, INotaDeCompraRepository notas, CancellationToken ct) =>
        {
            var businessId = http.ObterBusinessId();
            var nota = await notas.ObterPorIdAsync(id, ct).ConfigureAwait(false);
            if (nota is null || nota.TenantId != businessId) return Results.NotFound();
            return Results.Ok(NotaDeCompraDetalheDto.DeDominio(nota));
        }).RequerPermissao(Modulo.Compras, Acao.Ver);

        // POST /api/compras/notas — passos 1-7 do pipeline de importação. Idempotente por chave
        // de acesso (R3: reimportar o mesmo XML devolve a nota já existente, nunca duplica).
        api.MapPost("/compras/notas", async (
            HttpContext http, RegistrarEntradaDeNotaRequest corpo, RegistrarEntradaDeNotaUseCase useCase, CancellationToken ct) =>
        {
            var businessId = http.ObterBusinessId();

            if (!Enum.TryParse<OrigemNota>(corpo.Origem, ignoreCase: true, out var origem))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["origem"] = [$"Origem '{corpo.Origem}' desconhecida."] });

            var input = new EntradaDeNotaInput(
                businessId, corpo.LojaId, origem, corpo.Numero, corpo.Serie, corpo.DataEmissao,
                corpo.FornecedorId, corpo.ChaveDeAcessoBruta, corpo.VProdCentavos, corpo.VNfCentavos,
                corpo.Itens.Select(i => new ItemDeEntradaInput(
                    i.NItem, i.CProd, i.DescricaoNf, i.Ncm, i.UnidadeNf, i.QuantidadeNfMilesimos, i.VProdCentavos,
                    i.VDescCentavos, i.VFreteItemCentavos, i.VSegItemCentavos, i.VOutroItemCentavos, i.VIpiCentavos,
                    i.VIcmsStCentavos, i.LoteFornecedor, i.Validade, i.ProdutoIdConhecido, i.FatorConversaoConhecidoMilesimos)).ToList(),
                corpo.VFreteCentavos, corpo.VSeguroCentavos, corpo.VOutroCentavos, corpo.VDescontoCentavos, corpo.VStCentavos, corpo.VIpiCentavos);

            var resultado = await useCase.ExecutarAsync(input, ct).ConfigureAwait(false);
            return resultado.Sucesso ? Results.Ok(NotaDeCompraDetalheDto.DeDominio(resultado.Valor)) : resultado.Erro.ParaRespostaHttp();
        }).RequerPermissao(Modulo.Compras, Acao.Editar);

        api.MapPost("/compras/notas/{id}/resolver-match", async (
            HttpContext http, string id, ResolverMatchDeItemRequest corpo, INotaDeCompraRepository notas,
            ResolverMatchDeItemUseCase useCase, CancellationToken ct) =>
        {
            var businessId = http.ObterBusinessId();
            var existente = await notas.ObterPorIdAsync(id, ct).ConfigureAwait(false);
            if (existente is null || existente.TenantId != businessId) return Results.NotFound();

            var resultado = await useCase.ExecutarAsync(id, corpo.NItem, corpo.ProdutoId, corpo.FatorConversaoMilesimos, ct).ConfigureAwait(false);
            if (resultado.Falha) return resultado.Erro.ParaRespostaHttp();

            var atualizada = await notas.ObterPorIdAsync(id, ct).ConfigureAwait(false);
            return Results.Ok(NotaDeCompraDetalheDto.DeDominio(atualizada!));
        }).RequerPermissao(Modulo.Compras, Acao.Editar);

        api.MapPost("/compras/notas/{id}/ignorar-item", async (
            HttpContext http, string id, IgnorarItemRequest corpo, INotaDeCompraRepository notas,
            IgnorarItemDaNotaUseCase useCase, CancellationToken ct) =>
        {
            var businessId = http.ObterBusinessId();
            var existente = await notas.ObterPorIdAsync(id, ct).ConfigureAwait(false);
            if (existente is null || existente.TenantId != businessId) return Results.NotFound();

            var resultado = await useCase.ExecutarAsync(id, corpo.NItem, corpo.Motivo, ct).ConfigureAwait(false);
            if (resultado.Falha) return resultado.Erro.ParaRespostaHttp();

            var atualizada = await notas.ObterPorIdAsync(id, ct).ConfigureAwait(false);
            return Results.Ok(NotaDeCompraDetalheDto.DeDominio(atualizada!));
        }).RequerPermissao(Modulo.Compras, Acao.Editar);

        api.MapPost("/compras/notas/{id}/confirmar-recebimento", async (
            HttpContext http, string id, ConfirmarRecebimentoRequest corpo, INotaDeCompraRepository notas,
            ConfirmarRecebimentoUseCase useCase, CancellationToken ct) =>
        {
            var businessId = http.ObterBusinessId();
            var existente = await notas.ObterPorIdAsync(id, ct).ConfigureAwait(false);
            if (existente is null || existente.TenantId != businessId) return Results.NotFound();

            var resultado = await useCase.ExecutarAsync(id, corpo.UsuarioId, corpo.UsuarioNome, DateTimeOffset.UtcNow, ct).ConfigureAwait(false);
            return resultado.Sucesso ? Results.Ok(NotaDeCompraDetalheDto.DeDominio(resultado.Valor)) : resultado.Erro.ParaRespostaHttp();
        }).RequerPermissao(Modulo.Compras, Acao.Editar);

        api.MapPost("/compras/notas/{id}/estornar", async (
            HttpContext http, string id, EstornarRecebimentoRequest corpo, INotaDeCompraRepository notas,
            EstornarRecebimentoUseCase useCase, CancellationToken ct) =>
        {
            var businessId = http.ObterBusinessId();
            var existente = await notas.ObterPorIdAsync(id, ct).ConfigureAwait(false);
            if (existente is null || existente.TenantId != businessId) return Results.NotFound();

            var resultado = await useCase.ExecutarAsync(id, corpo.UsuarioId, corpo.UsuarioNome, DateTimeOffset.UtcNow, ct).ConfigureAwait(false);
            return resultado.Sucesso ? Results.Ok(NotaDeCompraDetalheDto.DeDominio(resultado.Valor)) : resultado.Erro.ParaRespostaHttp();
        }).RequerPermissao(Modulo.Compras, Acao.Editar);

        api.MapPost("/compras/notas/{id}/descartar", async (
            HttpContext http, string id, DescartarNotaRequest corpo, INotaDeCompraRepository notas,
            DescartarNotaUseCase useCase, CancellationToken ct) =>
        {
            var businessId = http.ObterBusinessId();
            var existente = await notas.ObterPorIdAsync(id, ct).ConfigureAwait(false);
            if (existente is null || existente.TenantId != businessId) return Results.NotFound();

            var resultado = await useCase.ExecutarAsync(id, corpo.Motivo, ct).ConfigureAwait(false);
            if (resultado.Falha) return resultado.Erro.ParaRespostaHttp();

            var atualizada = await notas.ObterPorIdAsync(id, ct).ConfigureAwait(false);
            return Results.Ok(NotaDeCompraDetalheDto.DeDominio(atualizada!));
        }).RequerPermissao(Modulo.Compras, Acao.Editar);
    }

    private static async Task<IResult> MutarFornecedorAsync(
        HttpContext http, string id, IFornecedorRepository fornecedores,
        Func<string, CancellationToken, Task<Result>> acao, CancellationToken ct)
    {
        var businessId = http.ObterBusinessId();
        var existente = await fornecedores.ObterPorIdAsync(id, ct).ConfigureAwait(false);
        if (existente is null || existente.TenantId != businessId) return Results.NotFound();

        var resultado = await acao(id, ct).ConfigureAwait(false);
        if (resultado.Falha) return resultado.Erro.ParaRespostaHttp();

        var atualizado = await fornecedores.ObterPorIdAsync(id, ct).ConfigureAwait(false);
        return Results.Ok(FornecedorDto.DeDominio(atualizado!));
    }
}
