using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Abstractions.Autorizacao;
using SistemaX.Modules.Fiscal.Application.CasosDeUso;
using SistemaX.Modules.Fiscal.Application.Ports;
using SistemaX.Modules.Fiscal.Domain.Comum;
using SistemaX.Modules.Fiscal.Domain.Documentos;
using SistemaX.Modules.Fiscal.Domain.Operacoes;
using SistemaX.Modules.Fiscal.Domain.Regimes;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Fiscal.Application.Endpoints;

/// <summary>DTO de fio do documento fiscal — versão RESUMO para a listagem (sem itens/tributos,
/// achado de auditoria: o front não tinha NENHUMA rota para listar documentos). <see cref="Total"/>
/// é recalculado do agregado (nunca cacheado, mesma disciplina de <c>DocumentoFiscal.Total</c>).</summary>
public sealed record DocumentoFiscalResumoDto(
    string Id,
    string Tipo,
    string Status,
    string? Serie,
    long? Numero,
    string? ChaveDeAcesso,
    string? Protocolo,
    string? Motivo,
    Money Total,
    string OrigemModulo,
    string OrigemId,
    DateTimeOffset CriadoEm)
{
    public static DocumentoFiscalResumoDto DeDominio(DocumentoFiscal d) => new(
        d.Id, d.Tipo.ToString(), d.Status.ToString(), d.Serie, d.Numero, d.ChaveDeAcesso, d.Protocolo,
        d.MotivoBloqueioOuRejeicaoOuDenegacao, d.Total, d.Origem.Modulo, d.Origem.Id, d.CriadoEm);
}

/// <summary>Versão DETALHE (com itens/tributos) — usada por <c>GET /fiscal/documentos/{id}</c>.</summary>
public sealed record ItemDocumentoFiscalDto(
    string ProdutoId, string Descricao, string Ncm, string? Cest, string Cfop,
    Quantidade Quantidade, Money PrecoUnitario, Money Desconto, Money Subtotal)
{
    public static ItemDocumentoFiscalDto DeDominio(ItemDocumentoFiscal i) =>
        new(i.ProdutoId, i.Descricao, i.Ncm, i.Cest, i.Cfop, i.Quantidade, i.PrecoUnitario, i.Desconto, i.Subtotal);
}

public sealed record DocumentoFiscalDetalheDto(
    string Id, string Tipo, string Status, string? Serie, long? Numero, string? ChaveDeAcesso,
    string? Protocolo, string? Motivo, Money Total, string OrigemModulo, string OrigemId,
    DateTimeOffset CriadoEm, IReadOnlyList<ItemDocumentoFiscalDto> Itens)
{
    public static DocumentoFiscalDetalheDto DeDominio(DocumentoFiscal d) => new(
        d.Id, d.Tipo.ToString(), d.Status.ToString(), d.Serie, d.Numero, d.ChaveDeAcesso, d.Protocolo,
        d.MotivoBloqueioOuRejeicaoOuDenegacao, d.Total, d.Origem.Modulo, d.Origem.Id, d.CriadoEm,
        d.Itens.Select(ItemDocumentoFiscalDto.DeDominio).ToList());
}

public sealed record ItemParaEmitirRequest(
    string ProdutoId, string Descricao, string Ncm, long QuantidadeMilesimos,
    long PrecoUnitarioCentavos, long DescontoCentavos = 0, string? CfopDaEmissao = null);

public sealed record OperacaoFiscalRequest(
    string TipoOperacao, string UfDestino, bool DestinatarioConsumidorFinal,
    bool DestinatarioContribuinteIcms, bool OperacaoPresencial);

/// <summary>Corpo de <c>POST /api/fiscal/documentos</c> — emissão manual (avulsa) de um documento
/// fiscal. Regime/UfOrigem vêm SEMPRE de <see cref="ConfiguracaoFiscalTenant"/> (nunca do corpo —
/// o cliente não escolhe o regime tributário do tenant numa emissão pontual).</summary>
public sealed record CriarDocumentoFiscalRequest(
    string Tipo, string OrigemModulo, string OrigemId, string Modelo, string Serie,
    OperacaoFiscalRequest Operacao, IReadOnlyList<ItemParaEmitirRequest> Itens);

public sealed record CartaCorrecaoDto(string Id, string DocumentoFiscalId, int Sequencia, string Texto, DateTimeOffset RegistradoEm)
{
    public static CartaCorrecaoDto DeDominio(CartaCorrecaoFiscal c) => new(c.Id, c.DocumentoFiscalId, c.Sequencia, c.Texto, c.RegistradoEm);
}

public sealed record EmitirCartaCorrecaoRequest(string Correcao);

public sealed record ConfiguracaoFiscalTenantDto(
    string TenantId, string Regime, string UfOrigem, string SerieNfce, string SerieNfe, string? CscId, bool CscConfigurado)
{
    /// <summary><see cref="CscToken"/> NUNCA sai no wire (é segredo — mesmo cuidado de
    /// <c>CertificadoDigital.Senha</c>, que também não tem DTO de leitura) — só
    /// <see cref="CscConfigurado"/> (booleano) informa a UI se o token já foi cadastrado.</summary>
    public static ConfiguracaoFiscalTenantDto DeDominio(ConfiguracaoFiscalTenant c) =>
        new(c.TenantId, c.Regime.ToString(), c.UfOrigem, c.SerieNfce, c.SerieNfe, c.CscId, !string.IsNullOrWhiteSpace(c.CscToken));
}

public sealed record AtualizarConfiguracaoFiscalTenantRequest(
    string Regime, string UfOrigem, string SerieNfce = "1", string SerieNfe = "1", string? CscId = null, string? CscToken = null);

/// <summary>
/// Endpoints HTTP do módulo Fiscal — achado de auditoria (guard-rail em <c>SistemaXHost.cs</c>):
/// o módulo de domínio existia por inteiro (emissão/transmissão/cancelamento/desistência) sem
/// NENHUMA rota HTTP. Fecha a listagem/detalhe (read-model que faltava), a emissão manual, as
/// transições de FSM já implementadas em Application, e os dois itens do Passo 3 (CC-e sobre
/// documento Autorizado; CSC na configuração fiscal do tenant).
/// </summary>
public sealed class FiscalEndpointsModule : IModule, IModuleEndpoints
{
    public string Codigo => "fiscal.endpoints";
    public string Nome => "Fiscal — Endpoints HTTP";
    public IReadOnlyCollection<string> DependeDe => ["fiscal"];

    public void Registrar(IServiceCollection services, IModuleContext contexto)
    {
        // Sem registro de serviço — só rotas, ver MapearEndpoints.
    }

    public void MapearEndpoints(IEndpointRouteBuilder api)
    {
        // GET /api/fiscal/documentos — listagem do tenant da sessão (R1), filtro opcional por status.
        api.MapGet("/fiscal/documentos", async (
            HttpContext http, IDocumentoFiscalRepository documentos, CancellationToken ct, string? status = null) =>
        {
            var businessId = http.ObterBusinessId();

            StatusDocumentoFiscal? statusFiltro = null;
            if (!string.IsNullOrWhiteSpace(status))
            {
                if (!Enum.TryParse<StatusDocumentoFiscal>(status, ignoreCase: true, out var parsed))
                    return Results.ValidationProblem(new Dictionary<string, string[]> { ["status"] = [$"Status '{status}' desconhecido."] });
                statusFiltro = parsed;
            }

            var lista = await documentos.ListarAsync(businessId, statusFiltro, ct).ConfigureAwait(false);
            return Results.Ok(lista.Select(DocumentoFiscalResumoDto.DeDominio));
        }).RequerPermissao(Modulo.Fiscal, Acao.Ver);

        // GET /api/fiscal/documentos/{id} — detalhe com itens/tributos.
        api.MapGet("/fiscal/documentos/{id}", async (
            HttpContext http, string id, IDocumentoFiscalRepository documentos, CancellationToken ct) =>
        {
            var businessId = http.ObterBusinessId();
            var documento = await documentos.ObterPorIdAsync(id, ct).ConfigureAwait(false);
            if (documento is null || documento.TenantId != businessId) return Results.NotFound();
            return Results.Ok(DocumentoFiscalDetalheDto.DeDominio(documento));
        }).RequerPermissao(Modulo.Fiscal, Acao.Ver);

        // POST /api/fiscal/documentos — emissão manual. Idempotente por Origem (R3) — reenviar o
        // mesmo par (origemModulo, origemId) devolve o documento já existente, nunca duplica
        // (mesma garantia de EmitirDocumentoFiscalUseCase, aqui só exposta via HTTP).
        api.MapPost("/fiscal/documentos", async (
            HttpContext http, CriarDocumentoFiscalRequest corpo, EmitirDocumentoFiscalUseCase useCase,
            IConfiguracaoFiscalTenantRepository configuracoes, CancellationToken ct) =>
        {
            var businessId = http.ObterBusinessId();

            if (!Enum.TryParse<TipoDocumentoFiscal>(corpo.Tipo, ignoreCase: true, out var tipo))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["tipo"] = [$"Tipo '{corpo.Tipo}' desconhecido."] });

            if (!Enum.TryParse<TipoOperacaoFiscal>(corpo.Operacao.TipoOperacao, ignoreCase: true, out var tipoOperacao))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["operacao.tipoOperacao"] = [$"Tipo de operação '{corpo.Operacao.TipoOperacao}' desconhecido."] });

            var configuracao = await configuracoes.ObterAsync(businessId, ct).ConfigureAwait(false);
            if (configuracao is null)
                return new Error("fiscal.documento.configuracao_tenant_ausente", "Configure regime/UF de origem em /fiscal/configuracao antes de emitir.").ParaRespostaHttp();

            var operacao = new OperacaoFiscal(
                tipoOperacao, configuracao.UfOrigem, corpo.Operacao.UfDestino.ToUpperInvariant(),
                corpo.Operacao.DestinatarioConsumidorFinal, corpo.Operacao.DestinatarioContribuinteIcms, corpo.Operacao.OperacaoPresencial);

            var itens = corpo.Itens.Select(i => new ItemParaEmitir(
                i.ProdutoId, i.Descricao, i.Ncm, new Quantidade(i.QuantidadeMilesimos),
                new Money(i.PrecoUnitarioCentavos), new Money(i.DescontoCentavos), i.CfopDaEmissao)).ToList();

            var resultado = await useCase.ExecutarAsync(
                businessId, tipo, new SourceRef(corpo.OrigemModulo, corpo.OrigemId), configuracao.Regime,
                operacao, corpo.Modelo, corpo.Serie, itens, ct).ConfigureAwait(false);

            return resultado.Sucesso ? Results.Ok(DocumentoFiscalDetalheDto.DeDominio(resultado.Valor)) : resultado.Erro.ParaRespostaHttp();
        }).RequerPermissao(Modulo.Fiscal, Acao.EmitirFiscal);

        // POST /api/fiscal/documentos/{id}/retransmitir — reenvia o XML de um documento preso em
        // NumeroAlocado/Rejeitado/EmContingencia (mesmo caso de uso do job RetransmitirDocumentosPendentesUseCase).
        api.MapPost("/fiscal/documentos/{id}/retransmitir", async (
            HttpContext http, string id, IDocumentoFiscalRepository documentos, TransmitirDocumentoFiscalUseCase useCase, CancellationToken ct) =>
        {
            var businessId = http.ObterBusinessId();
            var documento = await documentos.ObterPorIdAsync(id, ct).ConfigureAwait(false);
            if (documento is null || documento.TenantId != businessId) return Results.NotFound();

            var resultado = await useCase.ExecutarAsync(documento, ct).ConfigureAwait(false);
            return resultado.Sucesso ? Results.Ok(DocumentoFiscalDetalheDto.DeDominio(resultado.Valor)) : resultado.Erro.ParaRespostaHttp();
        }).RequerPermissao(Modulo.Fiscal, Acao.EmitirFiscal);

        // POST /api/fiscal/documentos/{id}/cancelar — janela pós-autorização (FSM: só Autorizado→Cancelado).
        api.MapPost("/fiscal/documentos/{id}/cancelar", async (
            HttpContext http, string id, CancelarDocumentoFiscalRequest corpo, IDocumentoFiscalRepository documentos,
            CancelarDocumentoFiscalUseCase useCase, CancellationToken ct) =>
        {
            var businessId = http.ObterBusinessId();
            var existente = await documentos.ObterPorIdAsync(id, ct).ConfigureAwait(false);
            if (existente is null || existente.TenantId != businessId) return Results.NotFound();

            var resultado = await useCase.ExecutarAsync(id, corpo.Justificativa, ct).ConfigureAwait(false);
            return resultado.Sucesso ? Results.Ok(DocumentoFiscalDetalheDto.DeDominio(resultado.Valor)) : resultado.Erro.ParaRespostaHttp();
        }).RequerPermissao(Modulo.Fiscal, Acao.EmitirFiscal);

        // POST /api/fiscal/documentos/{id}/desistir — fecha número alocado que nunca autorizou.
        api.MapPost("/fiscal/documentos/{id}/desistir", async (
            HttpContext http, string id, DesistirDeNumeroRequest corpo, IDocumentoFiscalRepository documentos,
            DesistirDeNumeroUseCase useCase, CancellationToken ct) =>
        {
            var businessId = http.ObterBusinessId();
            var existente = await documentos.ObterPorIdAsync(id, ct).ConfigureAwait(false);
            if (existente is null || existente.TenantId != businessId) return Results.NotFound();

            var resultado = await useCase.ExecutarAsync(id, corpo.Motivo, ct).ConfigureAwait(false);
            return resultado.Sucesso ? Results.Ok(DocumentoFiscalDetalheDto.DeDominio(resultado.Valor)) : resultado.Erro.ParaRespostaHttp();
        }).RequerPermissao(Modulo.Fiscal, Acao.EmitirFiscal);

        // GET /api/fiscal/documentos/{id}/cartas-correcao — histórico de CC-e do documento.
        api.MapGet("/fiscal/documentos/{id}/cartas-correcao", async (
            HttpContext http, string id, IDocumentoFiscalRepository documentos, ICartaCorrecaoFiscalRepository cartas, CancellationToken ct) =>
        {
            var businessId = http.ObterBusinessId();
            var documento = await documentos.ObterPorIdAsync(id, ct).ConfigureAwait(false);
            if (documento is null || documento.TenantId != businessId) return Results.NotFound();

            var lista = await cartas.ListarPorDocumentoAsync(id, ct).ConfigureAwait(false);
            return Results.Ok(lista.Select(CartaCorrecaoDto.DeDominio));
        }).RequerPermissao(Modulo.Fiscal, Acao.Ver);

        // POST /api/fiscal/documentos/{id}/carta-correcao — Passo 3(a): FSM guard (só Autorizado)
        // vive dentro de EmitirCartaCorrecaoUseCase; o endpoint só traduz o Result em HTTP.
        api.MapPost("/fiscal/documentos/{id}/carta-correcao", async (
            HttpContext http, string id, EmitirCartaCorrecaoRequest corpo, IDocumentoFiscalRepository documentos,
            EmitirCartaCorrecaoUseCase useCase, CancellationToken ct) =>
        {
            var businessId = http.ObterBusinessId();
            var existente = await documentos.ObterPorIdAsync(id, ct).ConfigureAwait(false);
            if (existente is null || existente.TenantId != businessId) return Results.NotFound();

            var resultado = await useCase.ExecutarAsync(id, corpo.Correcao, DateTimeOffset.UtcNow, ct).ConfigureAwait(false);
            return resultado.Sucesso ? Results.Ok(CartaCorrecaoDto.DeDominio(resultado.Valor)) : resultado.Erro.ParaRespostaHttp();
        }).RequerPermissao(Modulo.Fiscal, Acao.EmitirFiscal);

        // GET /api/fiscal/configuracao — regime/UF/séries/CSC do tenant da sessão.
        api.MapGet("/fiscal/configuracao", async (
            HttpContext http, IConfiguracaoFiscalTenantRepository configuracoes, CancellationToken ct) =>
        {
            var businessId = http.ObterBusinessId();
            var configuracao = await configuracoes.ObterAsync(businessId, ct).ConfigureAwait(false);
            return configuracao is null ? Results.NotFound() : Results.Ok(ConfiguracaoFiscalTenantDto.DeDominio(configuracao));
        }).RequerPermissao(Modulo.Fiscal, Acao.Ver);

        // PUT /api/fiscal/configuracao — cria ou atualiza (upsert, mesma semântica de
        // ConfiguracaoFiscalTenantRepository.SalvarAsync). Passo 3(b): CSC entra aqui.
        api.MapPut("/fiscal/configuracao", async (
            HttpContext http, AtualizarConfiguracaoFiscalTenantRequest corpo, IConfiguracaoFiscalTenantRepository configuracoes, CancellationToken ct) =>
        {
            var businessId = http.ObterBusinessId();

            if (!Enum.TryParse<RegimeTributario>(corpo.Regime, ignoreCase: true, out var regime))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["regime"] = [$"Regime '{corpo.Regime}' desconhecido."] });

            var resultado = ConfiguracaoFiscalTenant.Criar(businessId, regime, corpo.UfOrigem, corpo.SerieNfce, corpo.SerieNfe, corpo.CscId, corpo.CscToken);
            if (resultado.Falha) return resultado.Erro.ParaRespostaHttp();

            await configuracoes.SalvarAsync(resultado.Valor, ct).ConfigureAwait(false);
            return Results.Ok(ConfiguracaoFiscalTenantDto.DeDominio(resultado.Valor));
        }).RequerPermissao(Modulo.Fiscal, Acao.Editar);
    }
}

public sealed record CancelarDocumentoFiscalRequest(string Justificativa);

public sealed record DesistirDeNumeroRequest(string Motivo);
