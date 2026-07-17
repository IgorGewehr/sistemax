using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Abstractions.Autorizacao;
using SistemaX.Modules.Abstractions.Consultor;
using SistemaX.Modules.Financeiro.Application.Caixa;
using SistemaX.Modules.Financeiro.Application.CasosDeUso;
using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Application.Quant;
using SistemaX.Modules.Financeiro.Application.ReadModels;
using SistemaX.Modules.Financeiro.Domain.Caixa;
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

        // #7 — ponto de equilíbrio vivo do mês + dia do breakeven.
        api.MapGet("/financeiro/ponto-equilibrio", async (
            HttpContext http,
            PontoDeEquilibrioService servico,
            CancellationToken ct) =>
        {
            var businessId = http.ObterBusinessId();
            var resultado = await servico.CalcularAsync(businessId, ct).ConfigureAwait(false);
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
