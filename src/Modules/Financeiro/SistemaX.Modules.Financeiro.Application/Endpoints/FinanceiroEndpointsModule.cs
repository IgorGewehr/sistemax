using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Abstractions.Autorizacao;
using SistemaX.Modules.Abstractions.Consultor;
using SistemaX.Modules.Financeiro.Application.Ativos;
using SistemaX.Modules.Financeiro.Application.Caixa;
using SistemaX.Modules.Financeiro.Application.CasosDeUso;
using SistemaX.Modules.Financeiro.Application.Configuracao;
using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Application.Projetos;
using SistemaX.Modules.Financeiro.Application.Quant;
using SistemaX.Modules.Financeiro.Application.ReadModels;
using SistemaX.Modules.Financeiro.Application.Tempo;
using SistemaX.Modules.Financeiro.Domain.Ativos;
using SistemaX.Modules.Financeiro.Domain.Caixa;
using SistemaX.Modules.Financeiro.Domain.Configuracao;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Application.Endpoints;

/// <summary>DTO de fio de <see cref="Conciliacao"/> — nunca o agregado direto (mesma convenção do
/// <c>VendaDto</c>/<c>ProdutoDto</c>): a UI só precisa saber o resultado da ação de
/// confirmar/ignorar, não os invariantes internos do agregado.</summary>
public sealed record ConciliacaoDto(string Id, string MovimentoFinanceiroId, string ExtratoBancarioItemId, string Status, DateTimeOffset? ConciliadoEm)
{
    public static ConciliacaoDto DeDominio(Conciliacao conciliacao) => new(
        conciliacao.Id, conciliacao.MovimentoFinanceiroId, conciliacao.ExtratoBancarioItemId, conciliacao.Status.ToString(), conciliacao.ConciliadoEm);
}

public sealed record ConciliarMovimentoRequest(string MovimentoFinanceiroId, string ExtratoBancarioItemId, bool Automatico = false);

public sealed record IgnorarConciliacaoRequest(string MovimentoFinanceiroId, string ExtratoBancarioItemId);

/// <summary>DTO de fio de uma linha (<c>MovimentoDeSessaoCaixa</c>) dentro de <see cref="SessaoCaixaDto"/>
/// — tipo serializado em minúsculo (<c>suprimento|sangria|vendaEmEspecie</c>), nunca o enum cru.</summary>
public sealed record MovimentoSessaoCaixaDto(string Id, string Tipo, long ValorCentavos, string? Motivo, DateTimeOffset RegistradoEm, string OperadorId, string OperadorNome)
{
    public static MovimentoSessaoCaixaDto DeDominio(MovimentoDeSessaoCaixa m) => new(
        m.Id, ParaRotulo(m.Tipo), m.Valor.Centavos, m.Motivo, m.RegistradoEm, m.OperadorId, m.OperadorNome);

    private static string ParaRotulo(TipoMovimentoCaixa tipo) => tipo switch
    {
        TipoMovimentoCaixa.Suprimento => "suprimento",
        TipoMovimentoCaixa.Sangria => "sangria",
        TipoMovimentoCaixa.VendaEmEspecie => "vendaEmEspecie",
        _ => tipo.ToString()
    };
}

/// <summary>DTO de fio de <see cref="SessaoCaixa"/> — nunca o agregado direto (mesma convenção do
/// <c>ConciliacaoDto</c>). Carrega os totais já derivados (<c>saldoEsperadoCentavos</c>,
/// <c>diferencaCentavos</c>) para a UI não recalcular nada — só exibir.</summary>
public sealed record SessaoCaixaDto(
    string Id, string ContaCaixaId, string OperadorId, string OperadorNome, string Status,
    DateTimeOffset AbertaEm, long SaldoAberturaCentavos, long TotalEntradasCentavos, long TotalSaidasCentavos,
    long SaldoEsperadoCentavos, DateTimeOffset? FechadaEm, long? SaldoInformadoCentavos, long? DiferencaCentavos,
    IReadOnlyList<MovimentoSessaoCaixaDto> Movimentos)
{
    public static SessaoCaixaDto DeDominio(SessaoCaixa s) => new(
        s.Id, s.ContaCaixaId, s.OperadorId, s.OperadorNome, s.Status.ToString(), s.AbertaEm,
        s.SaldoAbertura.Centavos, s.TotalEntradas.Centavos, s.TotalSaidas.Centavos, s.SaldoEsperado.Centavos,
        s.FechadaEm, s.SaldoInformado?.Centavos, s.Diferenca?.Centavos,
        s.Movimentos.Select(MovimentoSessaoCaixaDto.DeDominio).ToArray());
}

public sealed record AbrirCaixaRequest(long SaldoAberturaCentavos, string OperadorId, string OperadorNome, string? ContaCaixaId = null);

public sealed record SuprimentoRequest(string SessaoId, long ValorCentavos, string Motivo, string OperadorId, string OperadorNome);

public sealed record SangriaRequest(string SessaoId, long ValorCentavos, string Motivo, string OperadorId, string OperadorNome);

public sealed record FecharCaixaRequest(string SessaoId, long ContadoCentavos);

/// <summary>DTO de fio de <see cref="ConfiguracaoFinanceiraTenant"/> — o shape de
/// docs/financeiro/design-analise-por-projeto.md §8.1, estendido pelos 3 campos do SEGUNDO toggle
/// (docs/financeiro/design-imobilizado-roi.md §8.3): <c>ImobilizadoRoiAtivo</c>,
/// <c>TaxaDescontoAnualBps</c> (null = payback descontado omitido — nunca um default silencioso) e
/// <c>InicioOperacao</c> (override do marco <c>m0</c> do ROI).</summary>
public sealed record ConfiguracaoFinanceiraDto(
    bool AnalisePorProjetoAtiva, long? CustoHoraPadraoCentavos, bool TempoEntraNoDre,
    bool ImobilizadoRoiAtivo = false, int? TaxaDescontoAnualBps = null, DateOnly? InicioOperacao = null)
{
    public static ConfiguracaoFinanceiraDto DeDominio(ConfiguracaoFinanceiraTenant c)
        => new(c.AnalisePorProjetoAtiva, c.CustoHoraPadraoCentavos, c.TempoEntraNoDre, c.ImobilizadoRoiAtivo, c.TaxaDescontoAnualBps, c.InicioOperacao);
}

public sealed record CriarProjetoRequest(string Nome, string? Descricao = null);

public sealed record EditarProjetoRequest(string? Nome = null, bool AtualizarDescricao = false, string? Descricao = null);

public sealed record VincularProjetoAssinaturaRequest(string? ProjetoId);

/// <summary>Requests de fio de <c>AtivoDeCapital</c> (design-pai §8.3) — <c>Natureza</c>/<c>Categoria</c>
/// chegam como STRING (nome do enum, ex. "Intangivel"/"LicencaSoftware") e são resolvidos aqui —
/// nunca o int cru na API pública.</summary>
public sealed record ParcelaInvestimentoRequest(DateTimeOffset Vencimento, long ValorCentavos);

public sealed record CriarAtivoDeCapitalRequest(
    string Nome, string Natureza, string Categoria, long CustoAquisicaoCentavos, DateOnly DataAquisicao, int VidaUtilMeses,
    long ValorResidualCentavos = 0, DateOnly? InicioDepreciacao = null, int QuantidadeUnidades = 1,
    string? ProjetoId = null, IReadOnlyCollection<ParcelaInvestimentoRequest>? Parcelas = null, string? ContaAPagarId = null);

/// <summary><paramref name="ValorVendaCentavos"/> não-nulo ⇒ alienação/venda (fatia I4,
/// docs/financeiro/design-imobilizado-roi.md §8.1: "!= null ⇒ Vendido"); nulo (default) ⇒ baixa
/// antecipada/write-off comum, comportamento intocado.</summary>
public sealed record BaixarAtivoDeCapitalRequest(string Motivo, DateOnly Competencia, long? ValorVendaCentavos = null);

/// <summary>Request de fio de <c>AporteDeCapital</c> (docs/financeiro/design-imobilizado-roi.md
/// §8.2) — valor+data+descrição, o gesto de um campo.</summary>
public sealed record RegistrarAporteDeCapitalRequest(long ValorCentavos, DateOnly Data, string Descricao);

/// <summary>Request de fio de <c>ApontamentoDeTempo</c> (design §8.4) — <c>custoCentavos</c> nunca
/// vem do cliente (sempre derivado/resolvido no servidor — nesta fatia, sempre <c>null</c>).</summary>
public sealed record RegistrarApontamentoRequest(
    int Minutos, DateTimeOffset Data, string OperadorId, string OperadorNome,
    string? ProjetoId = null, string? ClienteId = null, string? ClienteNome = null, string? AssinaturaId = null,
    string? OrdemServicoId = null, string? Descricao = null);

/// <summary>
/// Terceiro <see cref="IModule"/> do Financeiro — existe só para implementar
/// <see cref="IModuleEndpoints"/> (o Host o descobre via
/// <c>registry.ModulosAdicionados.OfType&lt;IModuleEndpoints&gt;()</c> e chama
/// <see cref="MapearEndpoints"/>, ver docs do contrato). Vive separado de <see cref="FinanceiroModule"/>
/// pelo mesmo motivo de <c>FinanceiroInfrastructureModule</c> existir separado: um módulo, uma
/// responsabilidade. <see cref="Registrar"/> é vazio de propósito — os serviços que os handlers
/// abaixo usam (<see cref="ReceitaRecorrenteService"/>) já são registrados por
/// <see cref="FinanceiroModule"/>.
/// </summary>
public sealed class FinanceiroEndpointsModule : IModule, IModuleEndpoints
{
    public string Codigo => "financeiro.endpoints";
    public string Nome => "Financeiro — Endpoints HTTP";
    public IReadOnlyCollection<string> DependeDe => ["financeiro"];

    public void Registrar(IServiceCollection services, IModuleContext contexto)
    {
        // Sem registro de serviço — só rotas, ver MapearEndpoints.
    }

    /// <summary>
    /// F1a: <c>GET /api/financeiro/receita-recorrente</c> — o painel de MRR/ARR/churn
    /// (<see cref="ReceitaRecorrenteService"/>), a prova end-to-end da F1a: read-model real sobre
    /// dado persistido, servido via HTTP, tenant vindo SÓ da sessão (R1 — nunca de query string).
    ///
    /// F1c adiciona os outros dois read-models da Visão Geral do Financeiro:
    /// <c>disponivel-retirada</c> (<see cref="QuantoSobrouDeVerdadeService"/>) e
    /// <c>fluxo</c> (<see cref="FluxoDeCaixaService"/>) — juntos, os 3 números que a tela React
    /// "Visão Geral" precisa para deixar de depender de <c>src/mocks/financeiro.ts</c>.
    /// </summary>
    public void MapearEndpoints(IEndpointRouteBuilder api)
    {
        api.MapGet("/financeiro/receita-recorrente", async (
            HttpContext http,
            ReceitaRecorrenteService servico,
            CancellationToken ct) =>
        {
            var businessId = http.ObterBusinessId();
            var resultado = await servico.CalcularAsync(businessId, DateTimeOffset.UtcNow, ct).ConfigureAwait(false);
            return Results.Ok(resultado);
        }).RequerPermissao(Modulo.Financeiro, Acao.Ver);

        // "Quanto sobra de verdade" — saldo em caixa, o que já tem dono (contas a pagar nos
        // próximos 30 dias) e o que pode ser retirado hoje. É o hero da Visão Geral.
        api.MapGet("/financeiro/disponivel-retirada", async (
            HttpContext http,
            QuantoSobrouDeVerdadeService servico,
            CancellationToken ct) =>
        {
            var businessId = http.ObterBusinessId();
            var resultado = await servico.CalcularAsync(businessId, ct).ConfigureAwait(false);
            return Results.Ok(resultado);
        }).RequerPermissao(Modulo.Financeiro, Acao.Ver);

        // Linha do tempo do caixa: realizado (histórico) + projetado (parcelas em aberto por
        // vencimento). Parâmetros com default de 14/30 dias — os mesmos da tela do mockup
        // "O caixa nos próximos 30 dias".
        api.MapGet("/financeiro/fluxo", async (
            HttpContext http,
            FluxoDeCaixaService servico,
            CancellationToken ct,
            int diasHistorico = 14,
            int diasProjecao = 30) =>
        {
            var businessId = http.ObterBusinessId();
            var resultado = await servico.ProjetarAsync(
                businessId,
                diasHistorico <= 0 ? 14 : diasHistorico,
                diasProjecao <= 0 ? 30 : diasProjecao,
                ct).ConfigureAwait(false);
            return Results.Ok(resultado);
        }).RequerPermissao(Modulo.Financeiro, Acao.Ver);

        // F0 do plano de inteligência do Financeiro (docs/financeiro/inteligencia-arquitetura.md/
        // ADR-0005): as fact tables já eram consultáveis via port/repositório (com contract tests),
        // mas não tinham rota HTTP — este era o gap. Ainda NÃO é a Porta 2 da extensibilidade
        // (endpoint genérico `/financeiro/series?metrica=&bucket=&de=&ate=` sobre N métricas
        // registradas, planejado para a Fase 3): são 3 rotas específicas, uma por fact table, o
        // caminho mínimo pra UI/API consultar o que o fold já produz. `de`/`ate` default para os
        // últimos 30 dias — mesmo horizonte do `diasHistorico` de `/financeiro/fluxo` acima.
        api.MapGet("/financeiro/fato-receita-diaria", async (
            HttpContext http,
            IFatoReceitaDiariaRepository repositorio,
            CancellationToken ct,
            DateOnly? de = null,
            DateOnly? ate = null) =>
        {
            var businessId = http.ObterBusinessId();
            var (desde, ateQuando) = ResolverPeriodo(de, ate);
            var resultado = await repositorio.ListarAsync(businessId, desde, ateQuando, ct).ConfigureAwait(false);
            return Results.Ok(resultado);
        }).RequerPermissao(Modulo.Financeiro, Acao.Ver);

        api.MapGet("/financeiro/fato-caixa-diario", async (
            HttpContext http,
            IFatoCaixaDiarioRepository repositorio,
            CancellationToken ct,
            DateOnly? de = null,
            DateOnly? ate = null) =>
        {
            var businessId = http.ObterBusinessId();
            var (desde, ateQuando) = ResolverPeriodo(de, ate);
            var resultado = await repositorio.ListarAsync(businessId, desde, ateQuando, ct).ConfigureAwait(false);
            return Results.Ok(resultado);
        }).RequerPermissao(Modulo.Financeiro, Acao.Ver);

        api.MapGet("/financeiro/fato-custo-diario", async (
            HttpContext http,
            IFatoCustoDiarioRepository repositorio,
            CancellationToken ct,
            DateOnly? de = null,
            DateOnly? ate = null) =>
        {
            var businessId = http.ObterBusinessId();
            var (desde, ateQuando) = ResolverPeriodo(de, ate);
            var resultado = await repositorio.ListarAsync(businessId, desde, ateQuando, ct).ConfigureAwait(false);
            return Results.Ok(resultado);
        }).RequerPermissao(Modulo.Financeiro, Acao.Ver);

        // fato_margem_produto (F1) — receita/CMV/MC por produto. `produtoId` opcional filtra um
        // único produto (ex.: drill-down "esse item dá lucro de verdade?"); sem ele, lista todos os
        // produtos com movimento no período.
        api.MapGet("/financeiro/fato-margem-produto", async (
            HttpContext http,
            IFatoMargemProdutoRepository repositorio,
            CancellationToken ct,
            string? produtoId = null,
            DateOnly? de = null,
            DateOnly? ate = null) =>
        {
            var businessId = http.ObterBusinessId();
            var (desde, ateQuando) = ResolverPeriodo(de, ate);
            var resultado = produtoId is null
                ? await repositorio.ListarAsync(businessId, desde, ateQuando, ct).ConfigureAwait(false)
                : await repositorio.ListarPorProdutoAsync(businessId, produtoId, desde, ateQuando, ct).ConfigureAwait(false);
            return Results.Ok(resultado);
        }).RequerPermissao(Modulo.Financeiro, Acao.Ver);

        // Frente 3 da autonomia do motor financeiro — fato_recebiveis: a "verdade de recebíveis"
        // já com MDR descontado e a data em que o dinheiro EFETIVAMENTE cai (vencimento + lag D+N
        // da forma de pagamento), não o bruto/emissão. `de`/`ate` filtram por VENCIMENTO, mesmo
        // default de 30 dias das outras rotas fato-*.
        api.MapGet("/financeiro/recebiveis", async (
            HttpContext http,
            IFatoRecebiveisRepository repositorio,
            CancellationToken ct,
            DateOnly? de = null,
            DateOnly? ate = null) =>
        {
            var businessId = http.ObterBusinessId();
            var (desde, ateQuando) = ResolverPeriodo(de, ate);
            var resultado = await repositorio.ListarPorVencimentoAsync(businessId, desde, ateQuando, ct).ConfigureAwait(false);
            return Results.Ok(resultado);
        }).RequerPermissao(Modulo.Financeiro, Acao.Ver);

        // MOTOR QUANT DA F1 (docs/financeiro/inteligencia-arquitetura.md §4/ADR-0005) — as 5
        // análises de "sobrevivência" priorizadas. Toda matemática é determinística em C#
        // (Application.Quant); estes endpoints só orquestram os ports reais.

        // #1 (bandas P5/P50/P95) + #2 (runway) — o mesmo motor de simulação alimenta as duas
        // perguntas ("quando fico sem caixa? com que certeza?" e "quantos dias eu aguento?").
        api.MapGet("/financeiro/previsao-caixa", async (
            HttpContext http,
            PrevisaoDeCaixaService servico,
            CancellationToken ct,
            int dias = 30) =>
        {
            var businessId = http.ObterBusinessId();
            var resultado = await servico.CalcularAsync(businessId, dias <= 0 ? 30 : dias, ct).ConfigureAwait(false);
            return Results.Ok(resultado);
        }).RequerPermissao(Modulo.Financeiro, Acao.Ver);

        // #7 — ponto de equilíbrio vivo do mês + dia do breakeven (+ margem de segurança/GAO/PE
        // econômico — ideia 1 do matemonstro). `custoOportunidadeMensal` opcional: sem config de
        // taxa de desconto cadastrada ainda (painel de ROI/imobilizado), o PE econômico degrada
        // para o PE contábil quando omitido (default 0).
        api.MapGet("/financeiro/ponto-equilibrio", async (
            HttpContext http,
            PontoDeEquilibrioService servico,
            CancellationToken ct,
            long custoOportunidadeMensalCentavos = 0) =>
        {
            var businessId = http.ObterBusinessId();
            var resultado = await servico.CalcularAsync(businessId, custoOportunidadeMensalCentavos, ct).ConfigureAwait(false);
            return Results.Ok(resultado);
        }).RequerPermissao(Modulo.Financeiro, Acao.Ver);

        // #3 — score de inadimplência por roll-rate/aging sobre os recebíveis em aberto.
        api.MapGet("/financeiro/inadimplencia", async (
            HttpContext http,
            InadimplenciaService servico,
            CancellationToken ct) =>
        {
            var businessId = http.ObterBusinessId();
            var resultado = await servico.CalcularAsync(businessId, ct).ConfigureAwait(false);
            return Results.Ok(resultado);
        }).RequerPermissao(Modulo.Financeiro, Acao.Ver);

        // #4 — Radar do Simples Nacional. `anexo` default "I" (único suportado na F1 — ver
        // RadarDoSimplesService); qualquer outro valor devolve 422 documentado, nunca 500.
        api.MapGet("/financeiro/radar-simples", async (
            HttpContext http,
            RadarDoSimplesService servico,
            CancellationToken ct,
            string anexo = "I") =>
        {
            var businessId = http.ObterBusinessId();
            if (!Enum.TryParse<AnexoSimplesNacional>(anexo, ignoreCase: true, out var anexoParseado))
            {
                return Results.BadRequest(new { erro = $"Anexo '{anexo}' inválido — use I, II, III, IV ou V." });
            }

            var resultado = await servico.CalcularAsync(businessId, anexoParseado, ct).ConfigureAwait(false);
            return resultado.Sucesso ? Results.Ok(resultado.Valor) : resultado.Erro.ParaRespostaHttp();
        }).RequerPermissao(Modulo.Financeiro, Acao.Ver);

        // FASE 2 do plano de inteligência do Financeiro (docs/financeiro/inteligencia-arquitetura.md
        // §3.5/ADR-0005) — o Super Consultor DE VERDADE, substituindo os 7 cards mockados
        // (`FLUXO_CAIXA_MOCK`). `ConsultorService` (module-agnostic, Abstractions) coleta os fatos
        // já calculados pelos read-models acima via `IConsultorFactProvider` (aqui,
        // `FinanceiroConsultorFactProvider`), rankeia por severidade/valor e narra. O narrador
        // registrado nesta rodada é o `NarradorTemplate` — determinístico, custo zero, de
        // propósito (nenhuma chamada a LLM ainda; ver a tarefa do Super Consultor). Devolve os
        // insights narrados JUNTO dos fatos crus (`Facts`/`Drill`) — o painel "Ver como
        // calculamos" nunca depende do LLM, só dos fatos.
        // Bancário (docs/wiring/financeiro-telas-restantes.md §3) — a tela rodava sobre UMA conta
        // hardcoded, sem repositório de conta/forma nenhum. Os dois endpoints abaixo são o que
        // desbloqueia a tela: contas com saldo REAL (SaldoInicial + ledger) e formas de pagamento
        // com o MDR/lag que fato_recebiveis também consome (mesmo IFormaDePagamentoRepository —
        // um só LAR pros dois consumidores).
        api.MapGet("/financeiro/contas-bancarias", async (
            HttpContext http,
            ContasBancariasService servico,
            CancellationToken ct) =>
        {
            var businessId = http.ObterBusinessId();
            var resultado = await servico.ListarAsync(businessId, ct).ConfigureAwait(false);
            return Results.Ok(resultado);
        }).RequerPermissao(Modulo.Financeiro, Acao.Ver);

        api.MapGet("/financeiro/formas-pagamento", async (
            HttpContext http,
            FormasDePagamentoService servico,
            CancellationToken ct) =>
        {
            var businessId = http.ObterBusinessId();
            var resultado = await servico.ListarAsync(businessId, ct).ConfigureAwait(false);
            return Results.Ok(resultado);
        }).RequerPermissao(Modulo.Financeiro, Acao.Ver);

        // Extrato do Bancário — junta MovimentoFinanceiro com nome da forma + status de
        // conciliação. `contaId` opcional filtra uma única conta (o AccountFilterBar da tela).
        // `de`/`ate` no mesmo default de 30 dias das demais rotas fato-*.
        api.MapGet("/financeiro/movimentos", async (
            HttpContext http,
            MovimentosBancariosService servico,
            CancellationToken ct,
            string? contaId = null,
            DateOnly? de = null,
            DateOnly? ate = null) =>
        {
            var businessId = http.ObterBusinessId();
            var (desde, ateQuando) = ResolverPeriodo(de, ate);
            var resultado = await servico
                .ListarAsync(businessId, desde.InicioDoDia(), ateQuando.FimDoDia(), contaId, ct)
                .ConfigureAwait(false);
            return Results.Ok(resultado);
        }).RequerPermissao(Modulo.Financeiro, Acao.Ver);

        // "Entrou × saiu por semana" (WeeksAnalysisCard) — agregação em baldes de 7 dias corridos
        // a partir de `de`.
        api.MapGet("/financeiro/movimentos-semana", async (
            HttpContext http,
            MovimentosSemanaisService servico,
            CancellationToken ct,
            DateOnly? de = null,
            DateOnly? ate = null) =>
        {
            var businessId = http.ObterBusinessId();
            var (desde, ateQuando) = ResolverPeriodo(de, ate);
            var resultado = await servico
                .ListarAsync(businessId, desde.InicioDoDia(), ateQuando.FimDoDia(), ct)
                .ConfigureAwait(false);
            return Results.Ok(resultado);
        }).RequerPermissao(Modulo.Financeiro, Acao.Ver);

        // Os 3 baldes de conciliação (bateu certinho / sobrou no banco / sobrou no sistema),
        // cada item de sobra já com a sugestão heurística de match (ver ConciliacaoBancariaService).
        api.MapGet("/financeiro/conciliacao", async (
            HttpContext http,
            ConciliacaoBancariaService servico,
            CancellationToken ct,
            DateOnly? de = null,
            DateOnly? ate = null) =>
        {
            var businessId = http.ObterBusinessId();
            var (desde, ateQuando) = ResolverPeriodo(de, ate);
            var resultado = await servico
                .CalcularAsync(businessId, desde.InicioDoDia(), ateQuando.FimDoDia(), ct)
                .ConfigureAwait(false);
            return Results.Ok(resultado);
        }).RequerPermissao(Modulo.Financeiro, Acao.Ver);

        // Confirma um par movimento/extrato — casca HTTP do ConciliarMovimentoUseCase, já pronto
        // e idempotente (reconciliar o mesmo par duas vezes não é erro). R1 reforçado aqui no
        // mesmo espírito de VendasEndpointsModule: nem IMovimentoFinanceiroRepository.ObterPorIdAsync
        // nem IConciliacaoRepository.BuscarPorParAsync filtram por tenant no port — o endpoint
        // confere movimento.BusinessId == businessId da sessão ANTES de chamar o caso de uso,
        // devolvendo 404 (nunca 403) para não revelar que o id existe noutro tenant.
        api.MapPost("/financeiro/conciliacao", async (
            HttpContext http,
            ConciliarMovimentoRequest corpo,
            ConciliarMovimentoUseCase useCase,
            IMovimentoFinanceiroRepository movimentosRepo,
            CancellationToken ct) =>
        {
            var businessId = http.ObterBusinessId();
            var movimento = await movimentosRepo.ObterPorIdAsync(corpo.MovimentoFinanceiroId, ct).ConfigureAwait(false);
            if (movimento is null || movimento.BusinessId != businessId) return Results.NotFound();

            var resultado = await useCase
                .ExecutarAsync(businessId, corpo.MovimentoFinanceiroId, corpo.ExtratoBancarioItemId, corpo.Automatico, ct)
                .ConfigureAwait(false);
            return resultado.Sucesso ? Results.Ok(ConciliacaoDto.DeDominio(resultado.Valor)) : resultado.Erro.ParaRespostaHttp();
        }).RequerPermissao(Modulo.Financeiro, Acao.Editar);

        // Descarta um par movimento/extrato — o item some do balde de sobra sem contar como batido.
        // Mesmo reforço de R1 acima.
        api.MapPost("/financeiro/conciliacao/ignorar", async (
            HttpContext http,
            IgnorarConciliacaoRequest corpo,
            ConciliarMovimentoUseCase useCase,
            IMovimentoFinanceiroRepository movimentosRepo,
            CancellationToken ct) =>
        {
            var businessId = http.ObterBusinessId();
            var movimento = await movimentosRepo.ObterPorIdAsync(corpo.MovimentoFinanceiroId, ct).ConfigureAwait(false);
            if (movimento is null || movimento.BusinessId != businessId) return Results.NotFound();

            var resultado = await useCase
                .IgnorarAsync(corpo.MovimentoFinanceiroId, corpo.ExtratoBancarioItemId, ct)
                .ConfigureAwait(false);
            return resultado.Sucesso ? Results.Ok(ConciliacaoDto.DeDominio(resultado.Valor)) : resultado.Erro.ParaRespostaHttp();
        }).RequerPermissao(Modulo.Financeiro, Acao.Editar);

        // Painel "Ver por forma" do Super Consultor Bancário — volume × MDR por forma de pagamento.
        api.MapGet("/financeiro/taxas-por-forma", async (
            HttpContext http,
            TaxasPorFormaService servico,
            CancellationToken ct,
            DateOnly? de = null,
            DateOnly? ate = null) =>
        {
            var businessId = http.ObterBusinessId();
            var (desde, ateQuando) = ResolverPeriodo(de, ate);
            var resultado = await servico
                .CalcularAsync(businessId, desde.InicioDoDia(), ateQuando.FimDoDia(), ct)
                .ConfigureAwait(false);
            return Results.Ok(resultado);
        }).RequerPermissao(Modulo.Financeiro, Acao.Ver);

        // Entradas & Saídas (docs/wiring/financeiro-telas-restantes.md §1/§A) — extrato unificado
        // (realizado + previsto/atrasado) com KPIs de total entradas/saídas/saldo do período.
        // O "Como fecha o mês" (projeção de saldo) do mockup NÃO é recalculado aqui — o front reusa
        // GET /financeiro/fluxo (saldoAcumulado do último ponto), que já existe.
        api.MapGet("/financeiro/extrato", async (
            HttpContext http,
            ExtratoUnificadoService servico,
            CancellationToken ct,
            DateOnly? de = null,
            DateOnly? ate = null,
            string? tipo = null,
            string? categoria = null) =>
        {
            var businessId = http.ObterBusinessId();
            var (desde, ateQuando) = ResolverPeriodo(de, ate);
            var resultado = await servico
                .ListarAsync(businessId, desde.InicioDoDia(), ateQuando.FimDoDia(), tipo, categoria, ct)
                .ConfigureAwait(false);
            return Results.Ok(resultado);
        }).RequerPermissao(Modulo.Financeiro, Acao.Ver);

        // Relatórios (docs/wiring/financeiro-telas-restantes.md §5/§B) — DRE gerencial por
        // competência, já pronto em DI (DreGerencialService) sem endpoint até aqui. Mesmo endpoint
        // serve o KPI "Resultado do mês" de Entradas & Saídas (mesmo serviço, dois consumidores).
        api.MapGet("/financeiro/relatorios/dre", async (
            HttpContext http,
            DreGerencialService servico,
            CancellationToken ct,
            DateOnly? de = null,
            DateOnly? ate = null) =>
        {
            var businessId = http.ObterBusinessId();
            var (desde, ateQuando) = ResolverPeriodo(de, ate);
            var resultado = await servico
                .CalcularAsync(businessId, desde.InicioDoDia(), ateQuando.FimDoDia(), ct)
                .ConfigureAwait(false);
            return Results.Ok(resultado);
        }).RequerPermissao(Modulo.Financeiro, Acao.Ver);

        // Accruals — "Lucro ≠ Caixa" (ideia 3 do matemonstro, docs/financeiro/ideias-matemonstro.md):
        // subtrai o resultado operacional por COMPETÊNCIA (mesmo DreGerencialService acima) do
        // fluxo de caixa OPERACIONAL do mesmo período (fato_caixa_diario, já bilateral — P1-3/Fatia
        // 6). Accruals alto e positivo = lucro no papel sem caixa; muito negativo = pré-pagamento.
        api.MapGet("/financeiro/relatorios/accruals", async (
            HttpContext http,
            AccrualsService servico,
            CancellationToken ct,
            DateOnly? de = null,
            DateOnly? ate = null) =>
        {
            var businessId = http.ObterBusinessId();
            var (desde, ateQuando) = ResolverPeriodo(de, ate);
            var resultado = await servico
                .CalcularAsync(businessId, desde.InicioDoDia(), ateQuando.FimDoDia(), ct)
                .ConfigureAwait(false);
            return Results.Ok(resultado);
        }).RequerPermissao(Modulo.Financeiro, Acao.Ver);

        // Concentração de receita por cliente (P2-4, docs/financeiro/revisao-domain-fit-cnpj.md) —
        // risco de dependência de conta grande: % da receita do período que vem do maior cliente.
        api.MapGet("/financeiro/relatorios/concentracao-clientes", async (
            HttpContext http,
            ConcentracaoDeReceitaService servico,
            CancellationToken ct,
            DateOnly? de = null,
            DateOnly? ate = null) =>
        {
            var businessId = http.ObterBusinessId();
            var (desde, ateQuando) = ResolverPeriodo(de, ate);
            var resultado = await servico
                .CalcularAsync(businessId, desde.InicioDoDia(), ateQuando.FimDoDia(), ct)
                .ConfigureAwait(false);
            return Results.Ok(resultado);
        }).RequerPermissao(Modulo.Financeiro, Acao.Ver);

        // Card "Contas em aberto" de Relatórios — soma de recebíveis/pagáveis ainda pendentes +
        // aging (0-15/15-30/+30d) do que está atrasado a receber.
        api.MapGet("/financeiro/relatorios/contas-em-aberto", async (
            HttpContext http,
            ContasEmAbertoService servico,
            CancellationToken ct) =>
        {
            var businessId = http.ObterBusinessId();
            var resultado = await servico.CalcularAsync(businessId, ct).ConfigureAwait(false);
            return Results.Ok(resultado);
        }).RequerPermissao(Modulo.Financeiro, Acao.Ver);

        // Recorrentes (docs/wiring/financeiro-telas-restantes.md §2/§C) — detalhe por assinatura
        // (a tabela "Todas as assinaturas"), complementando o resumo agregado de
        // GET /financeiro/receita-recorrente (que de propósito não devolve a lista nominal).
        // P0-3 (docs/financeiro/revisao-domain-fit-cnpj.md) — disparo manual do faturamento de
        // assinaturas, complementar ao cron (FaturarAssinaturasBackgroundService): útil pra rodar
        // sob demanda (ex.: acabou de configurar o cron, quer ver o efeito na hora, sem esperar o
        // próximo intervalo) sem depender de reiniciar o host. Mesmo caso de uso do cron — mesma
        // idempotência (competência+ciclo, dedupe por SourceRef).
        api.MapPost("/financeiro/assinaturas/faturar", async (
            HttpContext http,
            GerarCobrancasAssinaturasUseCase useCase,
            IRelogio relogio,
            CancellationToken ct) =>
        {
            var businessId = http.ObterBusinessId();
            var geradas = await useCase.ExecutarAsync(businessId, relogio.Agora(), ct).ConfigureAwait(false);
            return Results.Ok(new { geradas });
        }).RequerPermissao(Modulo.Financeiro, Acao.Editar);

        api.MapGet("/financeiro/recorrentes/detalhe", async (
            HttpContext http,
            AssinaturaDetalheService servico,
            CancellationToken ct) =>
        {
            var businessId = http.ObterBusinessId();
            var resultado = await servico.ListarAtivasAsync(businessId, DateTimeOffset.UtcNow, ct).ConfigureAwait(false);
            return Results.Ok(resultado);
        }).RequerPermissao(Modulo.Financeiro, Acao.Ver);

        // Lente "Contas fixas" de Recorrentes — expõe os templates de Recorrencia já cadastrados
        // (IRecorrenciaRepository), com a próxima ocorrência projetada.
        api.MapGet("/financeiro/recorrentes/fixas", async (
            HttpContext http,
            ContasFixasService servico,
            CancellationToken ct) =>
        {
            var businessId = http.ObterBusinessId();
            var resultado = await servico.ListarAsync(businessId, ct).ConfigureAwait(false);
            return Results.Ok(resultado);
        }).RequerPermissao(Modulo.Financeiro, Acao.Ver);

        api.MapGet("/financeiro/consultor", async (
            HttpContext http,
            ConsultorService servico,
            CancellationToken ct,
            int topN = ConsultorService.TopNPadrao) =>
        {
            var businessId = http.ObterBusinessId();
            var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
            var insights = await servico
                .GerarInsightsAsync(new PeriodoRef(businessId, hoje), topN <= 0 ? ConsultorService.TopNPadrao : topN, ct)
                .ConfigureAwait(false);
            return Results.Ok(insights);
        }).RequerPermissao(Modulo.Financeiro, Acao.Ver);

        // Fluxo de Caixa (docs/wiring/financeiro-telas-restantes.md §4) — o RITUAL de caixa físico
        // em espécie: abrir gaveta/sangria/suprimento/fechar contando. NÃO confundir com
        // GET /financeiro/fluxo (projeção de saldo da Visão Geral, colisão de nome só). Sem
        // `contaCaixaId` na query, todas as rotas usam ClassificadorFormaPagamento.ContaCaixaPadraoId
        // (a mesma conta-caixa física que MovimentoFinanceiro já usa como default para dinheiro) —
        // suficiente para o MVP de um caixa físico por tenant; multi-caixa é so passar o parâmetro.

        // A sessão aberta agora (se houver) + os totais já derivados (esperado corrente). null
        // quando não há sessão aberta — a UI mostra o card "Abrir caixa" nesse caso.
        api.MapGet("/financeiro/caixa/atual", async (
            HttpContext http,
            ISessaoCaixaRepository repositorio,
            CancellationToken ct,
            string? contaCaixaId = null) =>
        {
            var businessId = http.ObterBusinessId();
            var conta = contaCaixaId ?? ClassificadorFormaPagamento.ContaCaixaPadraoId;
            var aberta = await repositorio.ObterAbertaPorContaAsync(businessId, conta, ct).ConfigureAwait(false);
            return Results.Ok(aberta is null ? null : SessaoCaixaDto.DeDominio(aberta));
        }).RequerPermissao(Modulo.Financeiro, Acao.Ver);

        // Histórico de sessões (abertas e fechadas) da conta-caixa — a SessoesTable do mockup.
        api.MapGet("/financeiro/caixa/historico", async (
            HttpContext http,
            ISessaoCaixaRepository repositorio,
            CancellationToken ct,
            string? contaCaixaId = null,
            DateOnly? de = null,
            DateOnly? ate = null) =>
        {
            var businessId = http.ObterBusinessId();
            var conta = contaCaixaId ?? ClassificadorFormaPagamento.ContaCaixaPadraoId;
            var desde = de?.InicioDoDia();
            var ateQuando = ate?.FimDoDia();
            var sessoes = await repositorio.ListarAsync(businessId, conta, desde, ateQuando, ct).ConfigureAwait(false);
            return Results.Ok(sessoes.Select(SessaoCaixaDto.DeDominio).ToArray());
        }).RequerPermissao(Modulo.Financeiro, Acao.Ver);

        // Abre a gaveta com o fundo de troco — 404→422 documentado se já houver sessão aberta
        // nesta conta-caixa (AbrirSessaoCaixaUseCase.financeiro.sessao_caixa.ja_aberta).
        api.MapPost("/financeiro/caixa/abrir", async (
            HttpContext http,
            AbrirCaixaRequest corpo,
            AbrirSessaoCaixaUseCase useCase,
            CancellationToken ct) =>
        {
            var businessId = http.ObterBusinessId();
            var conta = corpo.ContaCaixaId ?? ClassificadorFormaPagamento.ContaCaixaPadraoId;
            var resultado = await useCase
                .ExecutarAsync(businessId, conta, corpo.OperadorId, corpo.OperadorNome, new Money(corpo.SaldoAberturaCentavos), DateTimeOffset.UtcNow, ct)
                .ConfigureAwait(false);
            return resultado.Sucesso ? Results.Ok(SessaoCaixaDto.DeDominio(resultado.Valor)) : resultado.Erro.ParaRespostaHttp();
        }).RequerPermissao(Modulo.Financeiro, Acao.Editar);

        // Suprimento (reforço de troco) numa sessão aberta. R1 reforçado aqui no mesmo espírito de
        // /financeiro/conciliacao: confere sessao.BusinessId == businessId da sessão ANTES de
        // mutar, 404 se não bater (nunca revela que o id existe noutro tenant).
        api.MapPost("/financeiro/caixa/suprimento", async (
            HttpContext http,
            SuprimentoRequest corpo,
            MovimentarSessaoCaixaUseCase useCase,
            ISessaoCaixaRepository repositorio,
            CancellationToken ct) =>
        {
            var businessId = http.ObterBusinessId();
            var sessaoExistente = await repositorio.ObterPorIdAsync(businessId, corpo.SessaoId, ct).ConfigureAwait(false);
            if (sessaoExistente is null) return Results.NotFound();

            var resultado = await useCase
                .RegistrarSuprimentoAsync(businessId, corpo.SessaoId, new Money(corpo.ValorCentavos), corpo.Motivo, DateTimeOffset.UtcNow, corpo.OperadorId, corpo.OperadorNome, ct)
                .ConfigureAwait(false);
            if (resultado.Falha) return resultado.Erro.ParaRespostaHttp();

            var atualizada = await repositorio.ObterPorIdAsync(businessId, corpo.SessaoId, ct).ConfigureAwait(false);
            return Results.Ok(SessaoCaixaDto.DeDominio(atualizada!));
        }).RequerPermissao(Modulo.Financeiro, Acao.Editar);

        // Sangria (retirada) numa sessão aberta — SessaoCaixa.RegistrarSangria rejeita se o valor
        // exceder o saldo esperado no momento (financeiro.sessao_caixa.sangria_excede_saldo, 422).
        api.MapPost("/financeiro/caixa/sangria", async (
            HttpContext http,
            SangriaRequest corpo,
            MovimentarSessaoCaixaUseCase useCase,
            ISessaoCaixaRepository repositorio,
            CancellationToken ct) =>
        {
            var businessId = http.ObterBusinessId();
            var sessaoExistente = await repositorio.ObterPorIdAsync(businessId, corpo.SessaoId, ct).ConfigureAwait(false);
            if (sessaoExistente is null) return Results.NotFound();

            var resultado = await useCase
                .RegistrarSangriaAsync(businessId, corpo.SessaoId, new Money(corpo.ValorCentavos), corpo.Motivo, DateTimeOffset.UtcNow, corpo.OperadorId, corpo.OperadorNome, ct)
                .ConfigureAwait(false);
            if (resultado.Falha) return resultado.Erro.ParaRespostaHttp();

            var atualizada = await repositorio.ObterPorIdAsync(businessId, corpo.SessaoId, ct).ConfigureAwait(false);
            return Results.Ok(SessaoCaixaDto.DeDominio(atualizada!));
        }).RequerPermissao(Modulo.Financeiro, Acao.Editar);

        // Fecha com a contagem física (cega) — devolve a sessão já com Diferenca calculada
        // (ModalFecharCaixa mostra "sobrou/faltou X" assim que o operador confirma).
        api.MapPost("/financeiro/caixa/fechar", async (
            HttpContext http,
            FecharCaixaRequest corpo,
            FecharSessaoCaixaUseCase useCase,
            ISessaoCaixaRepository repositorio,
            CancellationToken ct) =>
        {
            var businessId = http.ObterBusinessId();
            var sessaoExistente = await repositorio.ObterPorIdAsync(businessId, corpo.SessaoId, ct).ConfigureAwait(false);
            if (sessaoExistente is null) return Results.NotFound();

            var resultado = await useCase
                .ExecutarAsync(businessId, corpo.SessaoId, new Money(corpo.ContadoCentavos), DateTimeOffset.UtcNow, ct)
                .ConfigureAwait(false);
            return resultado.Sucesso ? Results.Ok(SessaoCaixaDto.DeDominio(resultado.Valor)) : resultado.Erro.ParaRespostaHttp();
        }).RequerPermissao(Modulo.Financeiro, Acao.Editar);

        // ANÁLISE POR PROJETO (docs/financeiro/design-analise-por-projeto.md, Parte A) — toggle,
        // CRUD de Projeto e Painel v1. Desligado (default): GET /financeiro/projetos devolve []
        // (§2.2 do design — nunca 404/erro, a UI só não mostra a lista); qualquer escrita com
        // projetoId (aqui ou em Assinatura/LancarConta) → 422 financeiro.projetos.desativado.

        api.MapGet("/financeiro/configuracoes", async (
            HttpContext http,
            IConfiguracaoFinanceiraTenantRepository repositorio,
            CancellationToken ct) =>
        {
            var businessId = http.ObterBusinessId();
            var configuracao = await repositorio.ObterAsync(businessId, ct).ConfigureAwait(false) ?? ConfiguracaoFinanceiraTenant.Padrao(businessId);
            return Results.Ok(ConfiguracaoFinanceiraDto.DeDominio(configuracao));
        }).RequerPermissao(Modulo.Financeiro, Acao.Ver);

        // É AQUI que o toggle liga/desliga — Acao.Editar (mesma permissão de qualquer escrita de
        // configuração do módulo).
        api.MapPut("/financeiro/configuracoes", async (
            HttpContext http,
            ConfiguracaoFinanceiraDto corpo,
            IConfiguracaoFinanceiraTenantRepository repositorio,
            CancellationToken ct) =>
        {
            var businessId = http.ObterBusinessId();
            var resultado = ConfiguracaoFinanceiraTenant.Criar(
                businessId, corpo.AnalisePorProjetoAtiva, corpo.CustoHoraPadraoCentavos, corpo.TempoEntraNoDre,
                corpo.ImobilizadoRoiAtivo, corpo.TaxaDescontoAnualBps, corpo.InicioOperacao);
            if (resultado.Falha) return resultado.Erro.ParaRespostaHttp();

            await repositorio.SalvarAsync(resultado.Valor, ct).ConfigureAwait(false);
            return Results.Ok(ConfiguracaoFinanceiraDto.DeDominio(resultado.Valor));
        }).RequerPermissao(Modulo.Financeiro, Acao.Editar);

        api.MapGet("/financeiro/projetos", async (
            HttpContext http,
            IProjetoRepository repositorio,
            CancellationToken ct,
            bool incluirArquivados = false) =>
        {
            var businessId = http.ObterBusinessId();
            var projetos = await repositorio.ListarAsync(businessId, incluirArquivados, ct).ConfigureAwait(false);
            return Results.Ok(projetos.Select(ProjetoDto.DeDominio));
        }).RequerPermissao(Modulo.Financeiro, Acao.Ver);

        api.MapPost("/financeiro/projetos", async (
            HttpContext http,
            CriarProjetoRequest corpo,
            CriarProjetoUseCase useCase,
            CancellationToken ct) =>
        {
            var businessId = http.ObterBusinessId();
            var resultado = await useCase.ExecutarAsync(new CriarProjetoComando(businessId, corpo.Nome, corpo.Descricao), ct).ConfigureAwait(false);
            return resultado.Sucesso ? Results.Ok(ProjetoDto.DeDominio(resultado.Valor)) : resultado.Erro.ParaRespostaHttp();
        }).RequerPermissao(Modulo.Financeiro, Acao.Editar);

        // Renomear/editar descrição — PATCH semântico (edição parcial). Arquivar/reativar são
        // rotas de AÇÃO próprias (abaixo), nunca um campo de status neste PATCH — mesmo racional
        // de "FSM tem verbo próprio" do resto do módulo (AbrirCaixaRequest/FecharCaixaRequest).
        api.MapPatch("/financeiro/projetos/{id}", async (
            HttpContext http,
            string id,
            EditarProjetoRequest corpo,
            EditarProjetoUseCase useCase,
            CancellationToken ct) =>
        {
            var businessId = http.ObterBusinessId();
            var resultado = await useCase
                .ExecutarAsync(new RenomearProjetoComando(businessId, id, corpo.Nome, corpo.Descricao, corpo.AtualizarDescricao), ct)
                .ConfigureAwait(false);
            return resultado.Sucesso ? Results.Ok(ProjetoDto.DeDominio(resultado.Valor)) : resultado.Erro.ParaRespostaHttp();
        }).RequerPermissao(Modulo.Financeiro, Acao.Editar);

        api.MapPost("/financeiro/projetos/{id}/arquivar", async (
            HttpContext http,
            string id,
            ArquivarReativarProjetoUseCase useCase,
            CancellationToken ct) =>
        {
            var businessId = http.ObterBusinessId();
            var resultado = await useCase.ArquivarAsync(businessId, id, ct).ConfigureAwait(false);
            return resultado.Sucesso ? Results.Ok(ProjetoDto.DeDominio(resultado.Valor)) : resultado.Erro.ParaRespostaHttp();
        }).RequerPermissao(Modulo.Financeiro, Acao.Editar);

        api.MapPost("/financeiro/projetos/{id}/reativar", async (
            HttpContext http,
            string id,
            ArquivarReativarProjetoUseCase useCase,
            CancellationToken ct) =>
        {
            var businessId = http.ObterBusinessId();
            var resultado = await useCase.ReativarAsync(businessId, id, ct).ConfigureAwait(false);
            return resultado.Sucesso ? Results.Ok(ProjetoDto.DeDominio(resultado.Valor)) : resultado.Erro.ParaRespostaHttp();
        }).RequerPermissao(Modulo.Financeiro, Acao.Editar);

        // Painel v1 (design §9) — MRR/churn(hazard)/LTV/MC1 do projeto. 404 se o projeto não
        // existir; painel calcula mesmo com o toggle desligado hoje (leitura não é "escrita com
        // projetoId" — o guard só barra escrita; a rota só é alcançável de qualquer forma se o
        // chamador já sabe o id do projeto).
        api.MapGet("/financeiro/projetos/{id}/painel", async (
            HttpContext http,
            string id,
            PainelDoProjetoService servico,
            CancellationToken ct) =>
        {
            var businessId = http.ObterBusinessId();
            var resultado = await servico.CalcularAsync(businessId, id, ct).ConfigureAwait(false);
            return resultado.Sucesso ? Results.Ok(resultado.Valor) : resultado.Erro.ParaRespostaHttp(StatusCodes.Status404NotFound);
        }).RequerPermissao(Modulo.Financeiro, Acao.Ver);

        // Tagging aditivo em Assinatura (design §8.5) — vincular/desvincular (projetoId: null).
        api.MapPost("/financeiro/assinaturas/{id}/projeto", async (
            HttpContext http,
            string id,
            VincularProjetoAssinaturaRequest corpo,
            VincularProjetoAssinaturaUseCase useCase,
            CancellationToken ct) =>
        {
            var businessId = http.ObterBusinessId();
            var resultado = await useCase.ExecutarAsync(businessId, id, corpo.ProjetoId, ct).ConfigureAwait(false);
            return resultado.Sucesso
                ? Results.Ok(new { id = resultado.Valor.Id, projetoId = resultado.Valor.ProjetoId })
                : resultado.Erro.ParaRespostaHttp();
        }).RequerPermissao(Modulo.Financeiro, Acao.Editar);

        // ANÁLISE POR PROJETO — PARTE B (P3): ATIVO DE CAPITAL (design-pai §8.3, generalizado por
        // docs/financeiro/design-imobilizado-roi.md). Mesmo gating da Parte A — toggle desligado ⇒ 422.

        api.MapGet("/financeiro/ativos", async (
            HttpContext http,
            IAtivoDeCapitalRepository repositorio,
            CancellationToken ct,
            string? projetoId = null) =>
        {
            var businessId = http.ObterBusinessId();
            var ativos = await repositorio.ListarAsync(businessId, projetoId, ct).ConfigureAwait(false);
            return Results.Ok(ativos.Select(AtivoDeCapitalDto.DeDominio));
        }).RequerPermissao(Modulo.Financeiro, Acao.Ver);

        api.MapPost("/financeiro/ativos", async (
            HttpContext http,
            CriarAtivoDeCapitalRequest corpo,
            CriarAtivoDeCapitalUseCase useCase,
            CancellationToken ct) =>
        {
            var businessId = http.ObterBusinessId();

            if (!Enum.TryParse<NaturezaAtivo>(corpo.Natureza, ignoreCase: true, out var natureza))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["natureza"] = [$"Natureza '{corpo.Natureza}' inválida."] });
            if (!Enum.TryParse<CategoriaAtivo>(corpo.Categoria, ignoreCase: true, out var categoria))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["categoria"] = [$"Categoria '{corpo.Categoria}' inválida."] });

            var parcelas = corpo.Parcelas?.Select(p => new ParcelaInvestimento(p.Vencimento, p.ValorCentavos)).ToList();
            var comando = new CriarAtivoDeCapitalComando(
                businessId, corpo.Nome, natureza, categoria, corpo.CustoAquisicaoCentavos, corpo.DataAquisicao, corpo.VidaUtilMeses,
                corpo.ValorResidualCentavos, corpo.InicioDepreciacao, corpo.QuantidadeUnidades, corpo.ProjetoId, parcelas, corpo.ContaAPagarId);

            var resultado = await useCase.ExecutarAsync(comando, ct).ConfigureAwait(false);
            return resultado.Sucesso ? Results.Ok(AtivoDeCapitalDto.DeDominio(resultado.Valor)) : resultado.Erro.ParaRespostaHttp();
        }).RequerPermissao(Modulo.Financeiro, Acao.Editar);

        api.MapPost("/financeiro/ativos/{id}/baixar", async (
            HttpContext http,
            string id,
            BaixarAtivoDeCapitalRequest corpo,
            BaixarAtivoDeCapitalUseCase useCase,
            CancellationToken ct) =>
        {
            var businessId = http.ObterBusinessId();
            var resultado = await useCase.ExecutarAsync(new BaixarAtivoDeCapitalComando(businessId, id, corpo.Motivo, corpo.Competencia, corpo.ValorVendaCentavos), ct).ConfigureAwait(false);
            return resultado.Sucesso ? Results.Ok(AtivoDeCapitalDto.DeDominio(resultado.Valor)) : resultado.Erro.ParaRespostaHttp();
        }).RequerPermissao(Modulo.Financeiro, Acao.Editar);

        // IMOBILIZADO + PAINEL DE ROI DO NEGÓCIO (docs/financeiro/design-imobilizado-roi.md) — o
        // SEGUNDO toggle opt-in (imobilizadoRoiAtivo), independente do de Análise por Projeto (§2.1).
        // O Imobilizado REUSA o MESMO agregado AtivoDeCapital/AtivoDeCapitalDto de cima (natureza
        // Tangível + categorias equipamento/moveis/placa/reforma/computador) — "um handler só, dois
        // gates" (§8.1): CriarAtivoDeCapitalUseCase.ExecutarImobilizadoAsync troca só o gate.
        // Desligado (default): GET /financeiro/imobilizado e GET /financeiro/aportes devolvem []
        // (§2.2 — nunca 404/erro); GET /financeiro/roi-negocio devolve 404 (é um painel, não uma
        // listagem); qualquer escrita → 422 financeiro.imobilizado.desativado.

        api.MapGet("/financeiro/imobilizado", async (
            HttpContext http,
            IAtivoDeCapitalRepository repositorio,
            IConfiguracaoFinanceiraTenantRepository configuracoes,
            CancellationToken ct) =>
        {
            var businessId = http.ObterBusinessId();
            var gating = await FinanceiroOptInGuard.ExigirImobilizadoRoiAsync(businessId, configuracoes, ct).ConfigureAwait(false);
            if (gating.Falha) return Results.Ok(Array.Empty<AtivoDeCapitalDto>());

            var ativos = await repositorio.ListarAsync(businessId, ct: ct).ConfigureAwait(false);
            return Results.Ok(ativos.Select(AtivoDeCapitalDto.DeDominio));
        }).RequerPermissao(Modulo.Financeiro, Acao.Ver);

        api.MapPost("/financeiro/imobilizado", async (
            HttpContext http,
            CriarAtivoDeCapitalRequest corpo,
            CriarAtivoDeCapitalUseCase useCase,
            CancellationToken ct) =>
        {
            var businessId = http.ObterBusinessId();

            if (!Enum.TryParse<NaturezaAtivo>(corpo.Natureza, ignoreCase: true, out var natureza))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["natureza"] = [$"Natureza '{corpo.Natureza}' inválida."] });
            if (!Enum.TryParse<CategoriaAtivo>(corpo.Categoria, ignoreCase: true, out var categoria))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["categoria"] = [$"Categoria '{corpo.Categoria}' inválida."] });

            var parcelas = corpo.Parcelas?.Select(p => new ParcelaInvestimento(p.Vencimento, p.ValorCentavos)).ToList();
            var comando = new CriarAtivoDeCapitalComando(
                businessId, corpo.Nome, natureza, categoria, corpo.CustoAquisicaoCentavos, corpo.DataAquisicao, corpo.VidaUtilMeses,
                corpo.ValorResidualCentavos, corpo.InicioDepreciacao, corpo.QuantidadeUnidades, corpo.ProjetoId, parcelas, corpo.ContaAPagarId);

            var resultado = await useCase.ExecutarImobilizadoAsync(comando, ct).ConfigureAwait(false);
            return resultado.Sucesso ? Results.Ok(AtivoDeCapitalDto.DeDominio(resultado.Valor)) : resultado.Erro.ParaRespostaHttp();
        }).RequerPermissao(Modulo.Financeiro, Acao.Editar);

        api.MapPost("/financeiro/imobilizado/{id}/baixar", async (
            HttpContext http,
            string id,
            BaixarAtivoDeCapitalRequest corpo,
            BaixarAtivoDeCapitalUseCase useCase,
            IConfiguracaoFinanceiraTenantRepository configuracoes,
            CancellationToken ct) =>
        {
            var businessId = http.ObterBusinessId();
            var gating = await FinanceiroOptInGuard.ExigirImobilizadoRoiAsync(businessId, configuracoes, ct).ConfigureAwait(false);
            if (gating.Falha) return gating.Erro.ParaRespostaHttp();

            var resultado = await useCase.ExecutarAsync(new BaixarAtivoDeCapitalComando(businessId, id, corpo.Motivo, corpo.Competencia, corpo.ValorVendaCentavos), ct).ConfigureAwait(false);
            return resultado.Sucesso ? Results.Ok(AtivoDeCapitalDto.DeDominio(resultado.Valor)) : resultado.Erro.ParaRespostaHttp();
        }).RequerPermissao(Modulo.Financeiro, Acao.Editar);

        // Aportes de capital (§3.3/§8.2) — capital de giro/investimento inicial, FORA da partida
        // dobrada. Deletável fisicamente (DI5).

        api.MapGet("/financeiro/aportes", async (
            HttpContext http,
            IAporteDeCapitalRepository repositorio,
            IConfiguracaoFinanceiraTenantRepository configuracoes,
            CancellationToken ct) =>
        {
            var businessId = http.ObterBusinessId();
            var gating = await FinanceiroOptInGuard.ExigirImobilizadoRoiAsync(businessId, configuracoes, ct).ConfigureAwait(false);
            if (gating.Falha) return Results.Ok(Array.Empty<AporteDeCapitalDto>());

            var aportes = await repositorio.ListarAsync(businessId, ct).ConfigureAwait(false);
            return Results.Ok(aportes.Select(AporteDeCapitalDto.DeDominio));
        }).RequerPermissao(Modulo.Financeiro, Acao.Ver);

        api.MapPost("/financeiro/aportes", async (
            HttpContext http,
            RegistrarAporteDeCapitalRequest corpo,
            RegistrarAporteDeCapitalUseCase useCase,
            CancellationToken ct) =>
        {
            var businessId = http.ObterBusinessId();
            var comando = new RegistrarAporteDeCapitalComando(businessId, corpo.ValorCentavos, corpo.Data, corpo.Descricao);
            var resultado = await useCase.ExecutarAsync(comando, ct).ConfigureAwait(false);
            return resultado.Sucesso ? Results.Ok(AporteDeCapitalDto.DeDominio(resultado.Valor)) : resultado.Erro.ParaRespostaHttp();
        }).RequerPermissao(Modulo.Financeiro, Acao.Editar);

        api.MapDelete("/financeiro/aportes/{id}", async (
            HttpContext http,
            string id,
            ExcluirAporteDeCapitalUseCase useCase,
            CancellationToken ct) =>
        {
            var businessId = http.ObterBusinessId();
            var excluido = await useCase.ExecutarAsync(businessId, id, ct).ConfigureAwait(false);
            return excluido ? Results.NoContent() : Results.NotFound();
        }).RequerPermissao(Modulo.Financeiro, Acao.Editar);

        // Painel de ROI do negócio (§7) — 404 com o toggle desligado (é um painel, não uma
        // listagem — nunca [] silencioso aqui).
        api.MapGet("/financeiro/roi-negocio", async (
            HttpContext http,
            RoiDoNegocioService servico,
            CancellationToken ct) =>
        {
            var businessId = http.ObterBusinessId();
            var resultado = await servico.CalcularAsync(businessId, ct).ConfigureAwait(false);
            return resultado.Sucesso ? Results.Ok(resultado.Valor) : Results.NotFound(new { erro = resultado.Erro.Codigo, mensagem = resultado.Erro.Mensagem });
        }).RequerPermissao(Modulo.Financeiro, Acao.Ver);

        // ANÁLISE POR PROJETO — PARTE B (P4): APONTAMENTO DE TEMPO (design §8.4) — só minutos
        // (decisão do dono). Mesmo gating.

        api.MapPost("/financeiro/apontamentos", async (
            HttpContext http,
            RegistrarApontamentoRequest corpo,
            RegistrarApontamentoUseCase useCase,
            CancellationToken ct) =>
        {
            var businessId = http.ObterBusinessId();
            var comando = new RegistrarApontamentoComando(
                businessId, corpo.Minutos, corpo.Data, corpo.OperadorId, corpo.OperadorNome,
                corpo.ProjetoId, corpo.ClienteId, corpo.ClienteNome, corpo.AssinaturaId, corpo.OrdemServicoId, corpo.Descricao);
            var resultado = await useCase.ExecutarAsync(comando, ct).ConfigureAwait(false);
            return resultado.Sucesso ? Results.Ok(ApontamentoDeTempoDto.DeDominio(resultado.Valor)) : resultado.Erro.ParaRespostaHttp();
        }).RequerPermissao(Modulo.Financeiro, Acao.Editar);

        api.MapGet("/financeiro/apontamentos", async (
            HttpContext http,
            IApontamentoDeTempoRepository repositorio,
            CancellationToken ct,
            DateOnly? de = null,
            DateOnly? ate = null,
            string? projetoId = null,
            string? clienteId = null) =>
        {
            var businessId = http.ObterBusinessId();
            var (desde, ateQuando) = ResolverPeriodo(de, ate);
            var lista = await repositorio
                .ListarAsync(businessId, desde.InicioDoDia(), ateQuando.FimDoDia(), projetoId, clienteId, ct)
                .ConfigureAwait(false);
            return Results.Ok(lista.Select(ApontamentoDeTempoDto.DeDominio));
        }).RequerPermissao(Modulo.Financeiro, Acao.Ver);

        api.MapDelete("/financeiro/apontamentos/{id}", async (
            HttpContext http,
            string id,
            ExcluirApontamentoUseCase useCase,
            CancellationToken ct) =>
        {
            var businessId = http.ObterBusinessId();
            var excluido = await useCase.ExecutarAsync(businessId, id, ct).ConfigureAwait(false);
            return excluido ? Results.NoContent() : Results.NotFound();
        }).RequerPermissao(Modulo.Financeiro, Acao.Editar);

        // "Onde vai meu tempo" (design §9.7) — cross-projeto/cliente, ordenado por minutos desc.
        api.MapGet("/financeiro/tempo/resumo", async (
            HttpContext http,
            ResumoDeTempoService servico,
            CancellationToken ct,
            DateOnly? de = null,
            DateOnly? ate = null) =>
        {
            var businessId = http.ObterBusinessId();
            var (desde, ateQuando) = ResolverPeriodo(de, ate);
            var resultado = await servico.CalcularAsync(businessId, desde.InicioDoDia(), ateQuando.FimDoDia(), ct).ConfigureAwait(false);
            return Results.Ok(resultado);
        }).RequerPermissao(Modulo.Financeiro, Acao.Ver);

        // ─────────────────────────────────────────────────────────────────────────────────────
        // LENTES VERTICAIS OPT-IN (multi-MEI, docs/financeiro/ideias-matemonstro.md) — cada rota é
        // um read-model pequeno sobre dado que já existe; nenhuma delas exige toggle de
        // configuração (opt-in é por PRESENÇA DE DADO — sem venda/apontamento/técnico no período,
        // a resposta vem vazia, e é a própria UI quem decide mostrar a lente pro tipo de negócio
        // certo). Varejo (Curva ABC/Giro/Ruptura) mora em /estoque/analises/* — o dado é de lá.
        // ─────────────────────────────────────────────────────────────────────────────────────

        // ALIMENTAÇÃO/DELIVERY — food cost % (CMV do prato ÷ receita), por produto no período.
        // `produtoId` opcional filtra um único prato (drill-down do cardápio).
        api.MapGet("/financeiro/alimentacao/food-cost", async (
            HttpContext http,
            FoodCostService servico,
            CancellationToken ct,
            string? produtoId = null,
            DateOnly? de = null,
            DateOnly? ate = null) =>
        {
            var businessId = http.ObterBusinessId();
            var (desde, ateQuando) = ResolverPeriodo(de, ate);
            var linhas = await servico.CalcularAsync(businessId, desde, ateQuando, produtoId, ct).ConfigureAwait(false);
            return Results.Ok(linhas);
        }).RequerPermissao(Modulo.Financeiro, Acao.Ver);

        // ALIMENTAÇÃO/DELIVERY — engenharia de cardápio: matriz margem×popularidade em 4
        // quadrantes (Estrela/VacaLeiteira/Enigma/Abacaxi).
        api.MapGet("/financeiro/alimentacao/engenharia-cardapio", async (
            HttpContext http,
            EngenhariaDeCardapioService servico,
            CancellationToken ct,
            DateOnly? de = null,
            DateOnly? ate = null) =>
        {
            var businessId = http.ObterBusinessId();
            var (desde, ateQuando) = ResolverPeriodo(de, ate);
            var linhas = await servico.ClassificarAsync(businessId, desde, ateQuando, ct).ConfigureAwait(false);
            return Results.Ok(linhas);
        }).RequerPermissao(Modulo.Financeiro, Acao.Ver);

        // SERVIÇOS/BELEZA — ocupação/produtividade por profissional (horas apontadas ÷ horas
        // disponíveis). `horasDisponiveisPorDia` opcional (default 8h) — não há cadastro de
        // agenda/turno no sistema hoje, então quem sabe a capacidade real é quem chama.
        api.MapGet("/financeiro/servicos/ocupacao", async (
            HttpContext http,
            OcupacaoService servico,
            CancellationToken ct,
            decimal? horasDisponiveisPorDia = null,
            DateOnly? de = null,
            DateOnly? ate = null) =>
        {
            var businessId = http.ObterBusinessId();
            var (desde, ateQuando) = ResolverPeriodo(de, ate);
            var linhas = await servico
                .CalcularAsync(businessId, desde.InicioDoDia(), ateQuando.FimDoDia(), horasDisponiveisPorDia, ct)
                .ConfigureAwait(false);
            return Results.Ok(linhas);
        }).RequerPermissao(Modulo.Financeiro, Acao.Ver);

        // SERVIÇOS/BELEZA — receita e margem aproximada por técnico/profissional (OS faturadas
        // com TecnicoId, corrente Serviço).
        api.MapGet("/financeiro/servicos/receita-por-profissional", async (
            HttpContext http,
            ReceitaPorProfissionalService servico,
            CancellationToken ct,
            DateOnly? de = null,
            DateOnly? ate = null) =>
        {
            var businessId = http.ObterBusinessId();
            var (desde, ateQuando) = ResolverPeriodo(de, ate);
            var linhas = await servico.CalcularAsync(businessId, desde.InicioDoDia(), ateQuando.FimDoDia(), ct).ConfigureAwait(false);
            return Results.Ok(linhas);
        }).RequerPermissao(Modulo.Financeiro, Acao.Ver);

        // PREÇO POR DIVISOR (matemonstro idéia 2) — preço-piso/sugerido dado o custo e os
        // %-sobre-preço (MDR da forma de pagamento + alíquota efetiva do Radar + comissão).
        // `precoAtualCentavos` opcional devolve a margem REAL que o preço praticado hoje entrega
        // (o fato "no crédito, este item rende só X% real"). `null` na resposta ⇒ pedido
        // matematicamente impossível (percentuais + margem ≥ 100%), nunca um preço negativo.
        api.MapGet("/financeiro/precificacao/preco-por-divisor", async (
            HttpContext http,
            PrecoPorDivisorService servico,
            CancellationToken ct,
            long custoCentavos,
            decimal margemDesejadaPercent = 0m,
            string? formaDePagamentoId = null,
            decimal comissaoPercent = 0m,
            bool incluirAliquotaEfetiva = true,
            long? precoAtualCentavos = null) =>
        {
            var businessId = http.ObterBusinessId();
            var resultado = await servico
                .CalcularAsync(businessId, custoCentavos, margemDesejadaPercent, formaDePagamentoId, comissaoPercent, incluirAliquotaEfetiva, precoAtualCentavos, ct)
                .ConfigureAwait(false);
            return resultado is null
                ? Results.UnprocessableEntity(new { erro = "financeiro.preco_por_divisor.sem_solucao", mensagem = "Percentuais somados à margem desejada somam 100% ou mais — nenhum preço finito cobre isso." })
                : Results.Ok(resultado);
        }).RequerPermissao(Modulo.Financeiro, Acao.Ver);
    }

    /// <summary>Default de 30 dias terminando hoje (UTC) quando <paramref name="de"/>/
    /// <paramref name="ate"/> não vêm na query string — mesmo horizonte de
    /// <c>diasHistorico</c>/<c>diasProjecao</c> de <c>/financeiro/fluxo</c>.</summary>
    private static (DateOnly De, DateOnly Ate) ResolverPeriodo(DateOnly? de, DateOnly? ate)
    {
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var ateResolvido = ate ?? hoje;
        var deResolvido = de ?? ateResolvido.AddDays(-30);
        return (deResolvido, ateResolvido);
    }
}
