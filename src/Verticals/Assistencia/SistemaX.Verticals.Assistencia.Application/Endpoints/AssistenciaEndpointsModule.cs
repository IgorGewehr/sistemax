using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Abstractions.Autorizacao;
using SistemaX.SharedKernel;
using SistemaX.Verticals.Assistencia.Application.CasosDeUso;
using SistemaX.Verticals.Assistencia.Application.Ports;

namespace SistemaX.Verticals.Assistencia.Application.Endpoints;

public sealed record ClienteRefDto(string ClienteId, string Nome, string? Telefone)
{
    public static ClienteRefDto DeDominio(ClienteRef c) => new(c.ClienteId, c.Nome, c.Telefone);
}

public sealed record EquipamentoDto(string Tipo, string Marca, string Modelo, string? NumeroSerie, string? Acessorios, string? EstadoEntrada)
{
    // SenhaAcesso NUNCA sai no wire — mesma disciplina de Equipamento.ToString() (o domínio já
    // torna isso estruturalmente impossível de vazar por descuido; o DTO só reforça na borda).
    public static EquipamentoDto DeDominio(Equipamento e) => new(e.Tipo, e.Marca, e.Modelo, e.NumeroSerie, e.Acessorios, e.EstadoEntrada);
}

public sealed record PecaOrcadaDto(string LinhaId, string? ProdutoId, string Descricao, int Quantidade, Money PrecoUnitario, Money Subtotal)
{
    public static PecaOrcadaDto DeDominio(PecaOrcada p) => new(p.LinhaId, p.ProdutoId, p.Descricao, p.Quantidade, p.PrecoUnitario, p.Subtotal);
}

public sealed record OrcamentoDto(IReadOnlyList<PecaOrcadaDto> Pecas, Money MaoDeObra, Money TotalPecas, Money Total, int ValidadeDias, DateTimeOffset EnviadoEm, DateTimeOffset VenceEm)
{
    public static OrcamentoDto DeDominio(Orcamento o) =>
        new(o.Pecas.Select(PecaOrcadaDto.DeDominio).ToList(), o.MaoDeObra, o.TotalPecas, o.Total, o.ValidadeDias, o.EnviadoEm, o.VenceEm);
}

public sealed record PecaAplicadaDto(string LinhaId, string? ProdutoId, string Descricao, int Quantidade, Money PrecoUnitario, Money Subtotal, string Origem)
{
    public static PecaAplicadaDto DeDominio(PecaAplicada p) => new(p.LinhaId, p.ProdutoId, p.Descricao, p.Quantidade, p.PrecoUnitario, p.Subtotal, p.Origem.ToString());
}

/// <summary>Resumo para a fila/listagem — sem histórico/orçamento completo (achado de auditoria: o
/// vertical inteiro não tinha NENHUMA rota HTTP, então nem esta listagem existia).</summary>
public sealed record OrdemDeServicoResumoDto(
    string Id, string Numero, string Status, ClienteRefDto Cliente, EquipamentoDto Equipamento,
    string DefeitoRelatado, string? TecnicoNome, DateTimeOffset AbertaEm, DateTimeOffset? PrevisaoEntrega,
    Money TotalGeral, bool EstaAtrasada)
{
    public static OrdemDeServicoResumoDto DeDominio(OrdemDeServico os, DateTimeOffset agora) => new(
        os.Id, os.Numero, os.Status.ToString(), ClienteRefDto.DeDominio(os.Cliente), EquipamentoDto.DeDominio(os.Equipamento),
        os.DefeitoRelatado, os.TecnicoNome, os.AbertaEm, os.PrevisaoEntrega, os.TotalGeral, os.EstaAtrasada(agora));
}

public sealed record OrdemDeServicoDetalheDto(
    string Id, string Numero, string Status, ClienteRefDto Cliente, EquipamentoDto Equipamento,
    string DefeitoRelatado, string? Diagnostico, string? TecnicoId, string? TecnicoNome,
    DateTimeOffset AbertaEm, DateTimeOffset? PrevisaoEntrega, bool EhRetornoDeGarantia,
    OrcamentoDto? Orcamento, string? MotivoReprovacao, string? MotivoCancelamento,
    IReadOnlyList<PecaAplicadaDto> PecasAplicadas, Money TotalPecasAplicadas, Money MaoDeObraAtual, Money TotalGeral,
    string? FormaPagamento, Money Desconto, int GarantiaDias, DateTimeOffset? DataEntrega, DateTimeOffset? GarantiaAte, bool EstaAtrasada)
{
    public static OrdemDeServicoDetalheDto DeDominio(OrdemDeServico os, DateTimeOffset agora) => new(
        os.Id, os.Numero, os.Status.ToString(), ClienteRefDto.DeDominio(os.Cliente), EquipamentoDto.DeDominio(os.Equipamento),
        os.DefeitoRelatado, os.Diagnostico, os.TecnicoId, os.TecnicoNome, os.AbertaEm, os.PrevisaoEntrega, os.EhRetornoDeGarantia,
        os.Orcamento is null ? null : OrcamentoDto.DeDominio(os.Orcamento), os.MotivoReprovacao, os.MotivoCancelamento,
        os.PecasAplicadas.Select(PecaAplicadaDto.DeDominio).ToList(), os.TotalPecasAplicadas, os.MaoDeObraAtual, os.TotalGeral,
        os.FormaPagamento?.ToString(), os.Desconto, os.GarantiaDias, os.DataEntrega, os.GarantiaAte, os.EstaAtrasada(agora));
}

public sealed record AbrirOsRequest(
    string ClienteId, string ClienteNome, string? ClienteTelefone,
    string EquipamentoTipo, string EquipamentoMarca, string EquipamentoModelo, string? NumeroSerie,
    string? SenhaAcesso, string? Acessorios, string? EstadoEntrada, string DefeitoRelatado,
    DateTimeOffset? PrevisaoEntrega = null, string? OsOrigemId = null);

public sealed record AtribuirTecnicoRequest(string TecnicoId, string TecnicoNome);

public sealed record AlterarPrevisaoRequest(DateTimeOffset NovaPrevisao);

public sealed record RegistrarDiagnosticoRequest(string Diagnostico);

public sealed record PecaOrcadaRequest(string? ProdutoId, string Descricao, int Quantidade, long PrecoUnitarioCentavos);

public sealed record EnviarOrcamentoRequest(IReadOnlyList<PecaOrcadaRequest> PecasPrevistas, long MaoDeObraCentavos, int ValidadeDias);

public sealed record RegistrarAprovacaoRequest(string Canal, string? RegistradoPorId = null, string? RegistradoPorNome = null);

public sealed record RegistrarReprovacaoRequest(string Canal, string? Motivo = null, string? RegistradoPorId = null, string? RegistradoPorNome = null);

public sealed record DevolverSemReparoRequest(long TaxaDiagnosticoCentavos = 0);

public sealed record AplicarPecaRequest(string LinhaId);

public sealed record AdicionarPecaExtraRequest(string? ProdutoId, string Descricao, int Quantidade, long PrecoUnitarioCentavos, bool ClienteAvisado);

public sealed record AjustarMaoDeObraFinalRequest(long NovoValorCentavos, bool ClienteAvisado);

public sealed record EntregarRequest(string FormaPagamento, long DescontoCentavos, int GarantiaDias);

public sealed record CancelarOsRequest(string Motivo);

/// <summary>
/// Endpoints HTTP do vertical Assistência Técnica (Ordem de Serviço) — achado de auditoria (guard-
/// rail em <c>SistemaXHost.cs</c>): o domínio inteiro (agregado + FSM completa + os 2 grupos de
/// casos de uso) existia sem NENHUMA rota HTTP, sob o módulo RBAC <c>Autorizacao.Modulo.Ordens</c>.
/// Fecha a listagem/detalhe (read-model que faltava — <c>IOrdemDeServicoRepository</c> nem tinha
/// <c>ListarAsync</c> até esta rodada) e as transições de FSM já implementadas em Application.
///
/// <c>Numero</c> (ex.: "OS-0001") é gerado AQUI (camada HTTP, não domínio — <c>OrdemDeServico.Abrir</c>
/// já recebe o número pronto) por contagem simples da fila do tenant — MVP suficiente para
/// instalação single-writer local; se um gerador de sequência dedicado (mesmo molde de
/// <c>ISequenciaFiscalRepository</c>) vier a ser necessário, é extensão aditiva sem tocar Domain.
/// </summary>
public sealed class AssistenciaEndpointsModule : IModule, IModuleEndpoints
{
    public string Codigo => "assistencia.endpoints";
    public string Nome => "Assistência Técnica — Endpoints HTTP";
    public IReadOnlyCollection<string> DependeDe => ["assistencia"];

    public void Registrar(IServiceCollection services, IModuleContext contexto)
    {
        // Sem registro de serviço — só rotas, ver MapearEndpoints.
    }

    public void MapearEndpoints(IEndpointRouteBuilder api)
    {
        api.MapGet("/assistencia/ordens", async (
            HttpContext http, IOrdemDeServicoRepository ordens, CancellationToken ct, string? status = null) =>
        {
            var businessId = http.ObterBusinessId();

            StatusOrdemServico? statusFiltro = null;
            if (!string.IsNullOrWhiteSpace(status))
            {
                if (!Enum.TryParse<StatusOrdemServico>(status, ignoreCase: true, out var parsed))
                    return Results.ValidationProblem(new Dictionary<string, string[]> { ["status"] = [$"Status '{status}' desconhecido."] });
                statusFiltro = parsed;
            }

            var agora = DateTimeOffset.UtcNow;
            var lista = await ordens.ListarAsync(businessId, statusFiltro, ct).ConfigureAwait(false);
            return Results.Ok(lista.Select(os => OrdemDeServicoResumoDto.DeDominio(os, agora)));
        }).RequerPermissao(Modulo.Ordens, Acao.Ver);

        api.MapGet("/assistencia/ordens/{id}", async (HttpContext http, string id, IOrdemDeServicoRepository ordens, CancellationToken ct) =>
        {
            var businessId = http.ObterBusinessId();
            var os = await ordens.ObterPorIdAsync(id, ct).ConfigureAwait(false);
            if (os is null || os.TenantId != businessId) return Results.NotFound();
            return Results.Ok(OrdemDeServicoDetalheDto.DeDominio(os, DateTimeOffset.UtcNow));
        }).RequerPermissao(Modulo.Ordens, Acao.Ver);

        // POST /api/assistencia/ordens — abre a OS. Número gerado por contagem da fila do tenant
        // (ver doc da classe) — não é o dedupe de idempotência de R3 (abrir OS é sempre um NOVO
        // atendimento, nunca um replay de evento); segurança de duplicata acidental fica a cargo
        // do cliente HTTP (mesmo tratamento de qualquer POST de criação sem chave natural, como
        // AbrirCaixaRequest no Financeiro).
        api.MapPost("/assistencia/ordens", async (
            HttpContext http, AbrirOsRequest corpo, IOrdemDeServicoRepository ordens, AbrirOsUseCase useCase, CancellationToken ct) =>
        {
            var businessId = http.ObterBusinessId();
            var existentes = await ordens.ListarAsync(businessId, ct: ct).ConfigureAwait(false);
            var numero = $"OS-{existentes.Count + 1:D4}";

            var cliente = new ClienteRef(corpo.ClienteId, corpo.ClienteNome, corpo.ClienteTelefone);
            var equipamento = new Equipamento(
                corpo.EquipamentoTipo, corpo.EquipamentoMarca, corpo.EquipamentoModelo, corpo.NumeroSerie,
                corpo.SenhaAcesso, corpo.Acessorios, corpo.EstadoEntrada);

            var resultado = await useCase.ExecutarAsync(
                businessId, numero, cliente, equipamento, corpo.DefeitoRelatado, DateTimeOffset.UtcNow,
                corpo.PrevisaoEntrega, corpo.OsOrigemId, ct).ConfigureAwait(false);

            return resultado.Sucesso
                ? Results.Ok(OrdemDeServicoDetalheDto.DeDominio(resultado.Valor, DateTimeOffset.UtcNow))
                : resultado.Erro.ParaRespostaHttp();
        }).RequerPermissao(Modulo.Ordens, Acao.Editar);

        api.MapPost("/assistencia/ordens/{id}/atribuir-tecnico", async (
            HttpContext http, string id, AtribuirTecnicoRequest corpo, IOrdemDeServicoRepository ordens,
            GerenciarOrdemDeServicoUseCase useCase, CancellationToken ct) =>
            await MutarAsync(http, id, ordens, ct, () => useCase.AtribuirTecnicoAsync(id, corpo.TecnicoId, corpo.TecnicoNome, ct))
        ).RequerPermissao(Modulo.Ordens, Acao.Editar);

        api.MapPost("/assistencia/ordens/{id}/previsao-entrega", async (
            HttpContext http, string id, AlterarPrevisaoRequest corpo, IOrdemDeServicoRepository ordens,
            GerenciarOrdemDeServicoUseCase useCase, CancellationToken ct) =>
            await MutarAsync(http, id, ordens, ct, () => useCase.AlterarPrevisaoEntregaAsync(id, corpo.NovaPrevisao, ct))
        ).RequerPermissao(Modulo.Ordens, Acao.Editar);

        api.MapPost("/assistencia/ordens/{id}/diagnostico", async (
            HttpContext http, string id, RegistrarDiagnosticoRequest corpo, IOrdemDeServicoRepository ordens,
            GerenciarOrdemDeServicoUseCase useCase, CancellationToken ct) =>
            await MutarAsync(http, id, ordens, ct, () => useCase.RegistrarDiagnosticoAsync(id, corpo.Diagnostico, DateTimeOffset.UtcNow, ct))
        ).RequerPermissao(Modulo.Ordens, Acao.Editar);

        api.MapPost("/assistencia/ordens/{id}/orcamento", async (
            HttpContext http, string id, EnviarOrcamentoRequest corpo, IOrdemDeServicoRepository ordens,
            GerenciarOrdemDeServicoUseCase useCase, CancellationToken ct) =>
        {
            var pecas = corpo.PecasPrevistas
                .Select(p => PecaOrcada.Nova(p.ProdutoId, p.Descricao, p.Quantidade, new Money(p.PrecoUnitarioCentavos)))
                .ToList();
            return await MutarAsync(http, id, ordens, ct, () =>
                useCase.EnviarOrcamentoAsync(id, pecas, new Money(corpo.MaoDeObraCentavos), corpo.ValidadeDias, DateTimeOffset.UtcNow, ct));
        }).RequerPermissao(Modulo.Ordens, Acao.Editar);

        api.MapPost("/assistencia/ordens/{id}/aprovacao", async (
            HttpContext http, string id, RegistrarAprovacaoRequest corpo, IOrdemDeServicoRepository ordens,
            OrdemDeServicoFaturamentoUseCases useCase, CancellationToken ct) =>
        {
            if (!Enum.TryParse<CanalAprovacao>(corpo.Canal, ignoreCase: true, out var canal))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["canal"] = [$"Canal '{corpo.Canal}' desconhecido."] });
            return await MutarAsync(http, id, ordens, ct, () =>
                useCase.RegistrarAprovacaoAsync(id, canal, DateTimeOffset.UtcNow, corpo.RegistradoPorId, corpo.RegistradoPorNome, ct));
        }).RequerPermissao(Modulo.Ordens, Acao.Editar);

        api.MapPost("/assistencia/ordens/{id}/reprovacao", async (
            HttpContext http, string id, RegistrarReprovacaoRequest corpo, IOrdemDeServicoRepository ordens,
            GerenciarOrdemDeServicoUseCase useCase, CancellationToken ct) =>
        {
            if (!Enum.TryParse<CanalAprovacao>(corpo.Canal, ignoreCase: true, out var canal))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["canal"] = [$"Canal '{corpo.Canal}' desconhecido."] });
            return await MutarAsync(http, id, ordens, ct, () =>
                useCase.RegistrarReprovacaoAsync(id, canal, DateTimeOffset.UtcNow, corpo.Motivo, corpo.RegistradoPorId, corpo.RegistradoPorNome, ct));
        }).RequerPermissao(Modulo.Ordens, Acao.Editar);

        api.MapPost("/assistencia/ordens/{id}/devolver-sem-reparo", async (
            HttpContext http, string id, DevolverSemReparoRequest corpo, IOrdemDeServicoRepository ordens,
            OrdemDeServicoFaturamentoUseCases useCase, CancellationToken ct) =>
            await MutarAsync(http, id, ordens, ct, () => useCase.DevolverSemReparoAsync(id, new Money(corpo.TaxaDiagnosticoCentavos), DateTimeOffset.UtcNow, ct))
        ).RequerPermissao(Modulo.Ordens, Acao.Editar);

        api.MapPost("/assistencia/ordens/{id}/iniciar-execucao", async (
            HttpContext http, string id, IOrdemDeServicoRepository ordens, GerenciarOrdemDeServicoUseCase useCase, CancellationToken ct) =>
            await MutarAsync(http, id, ordens, ct, () => useCase.IniciarExecucaoAsync(id, DateTimeOffset.UtcNow, ct))
        ).RequerPermissao(Modulo.Ordens, Acao.Editar);

        api.MapPost("/assistencia/ordens/{id}/aplicar-peca", async (
            HttpContext http, string id, AplicarPecaRequest corpo, IOrdemDeServicoRepository ordens,
            OrdemDeServicoFaturamentoUseCases useCase, CancellationToken ct) =>
            await MutarAsync(http, id, ordens, ct, () => useCase.AplicarPecaAsync(id, corpo.LinhaId, DateTimeOffset.UtcNow, ct))
        ).RequerPermissao(Modulo.Ordens, Acao.Editar);

        api.MapPost("/assistencia/ordens/{id}/peca-extra", async (
            HttpContext http, string id, AdicionarPecaExtraRequest corpo, IOrdemDeServicoRepository ordens,
            OrdemDeServicoFaturamentoUseCases useCase, CancellationToken ct) =>
            await MutarAsync(http, id, ordens, ct, () => useCase.AdicionarPecaExtraAsync(
                id, corpo.ProdutoId, corpo.Descricao, corpo.Quantidade, new Money(corpo.PrecoUnitarioCentavos), corpo.ClienteAvisado, DateTimeOffset.UtcNow, ct))
        ).RequerPermissao(Modulo.Ordens, Acao.Editar);

        api.MapPost("/assistencia/ordens/{id}/mao-de-obra-final", async (
            HttpContext http, string id, AjustarMaoDeObraFinalRequest corpo, IOrdemDeServicoRepository ordens,
            GerenciarOrdemDeServicoUseCase useCase, CancellationToken ct) =>
            await MutarAsync(http, id, ordens, ct, () => useCase.AjustarMaoDeObraFinalAsync(id, new Money(corpo.NovoValorCentavos), corpo.ClienteAvisado, ct))
        ).RequerPermissao(Modulo.Ordens, Acao.Editar);

        api.MapPost("/assistencia/ordens/{id}/concluir-execucao", async (
            HttpContext http, string id, IOrdemDeServicoRepository ordens, OrdemDeServicoFaturamentoUseCases useCase, CancellationToken ct) =>
            await MutarAsync(http, id, ordens, ct, () => useCase.ConcluirExecucaoAsync(id, DateTimeOffset.UtcNow, ct))
        ).RequerPermissao(Modulo.Ordens, Acao.Editar);

        api.MapPost("/assistencia/ordens/{id}/entregar", async (
            HttpContext http, string id, EntregarRequest corpo, IOrdemDeServicoRepository ordens,
            OrdemDeServicoFaturamentoUseCases useCase, CancellationToken ct) =>
        {
            if (!Enum.TryParse<FormaPagamento>(corpo.FormaPagamento, ignoreCase: true, out var formaPagamento))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["formaPagamento"] = [$"Forma de pagamento '{corpo.FormaPagamento}' desconhecida."] });
            return await MutarAsync(http, id, ordens, ct, () =>
                useCase.EntregarAsync(id, formaPagamento, new Money(corpo.DescontoCentavos), corpo.GarantiaDias, DateTimeOffset.UtcNow, ct));
        }).RequerPermissao(Modulo.Ordens, Acao.Editar);

        api.MapPost("/assistencia/ordens/{id}/cancelar", async (
            HttpContext http, string id, CancelarOsRequest corpo, IOrdemDeServicoRepository ordens,
            OrdemDeServicoFaturamentoUseCases useCase, CancellationToken ct) =>
            await MutarAsync(http, id, ordens, ct, () => useCase.CancelarAsync(id, corpo.Motivo, DateTimeOffset.UtcNow, ct))
        ).RequerPermissao(Modulo.Ordens, Acao.Editar);
    }

    /// <summary>Padrão comum a toda mutação de FSM deste módulo: confirma que a OS existe e
    /// pertence ao tenant (R1), executa a ação, e devolve o detalhe já atualizado — evita repetir
    /// "busca de novo depois de mutar" em cada handler acima.</summary>
    private static async Task<IResult> MutarAsync(
        HttpContext http, string id, IOrdemDeServicoRepository ordens, CancellationToken ct, Func<Task<Result>> acao)
    {
        var businessId = http.ObterBusinessId();
        var existente = await ordens.ObterPorIdAsync(id, ct).ConfigureAwait(false);
        if (existente is null || existente.TenantId != businessId) return Results.NotFound();

        var resultado = await acao().ConfigureAwait(false);
        if (resultado.Falha) return resultado.Erro.ParaRespostaHttp();

        var atualizada = await ordens.ObterPorIdAsync(id, ct).ConfigureAwait(false);
        return Results.Ok(OrdemDeServicoDetalheDto.DeDominio(atualizada!, DateTimeOffset.UtcNow));
    }
}
