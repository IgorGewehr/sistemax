using Microsoft.Extensions.DependencyInjection;
using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Abstractions.Consultor;
using SistemaX.Modules.Financeiro.Application.Ativos;
using SistemaX.Modules.Financeiro.Application.Caixa;
using SistemaX.Modules.Financeiro.Application.CasosDeUso;
using SistemaX.Modules.Financeiro.Application.Consultor;
using SistemaX.Modules.Financeiro.Application.EventosDeIntegracao.Handlers;
using SistemaX.Modules.Financeiro.Application.Mrr;
using SistemaX.Modules.Financeiro.Application.Projetos;
using SistemaX.Modules.Financeiro.Application.ReadModels;
using SistemaX.Modules.Financeiro.Application.Tempo;

namespace SistemaX.Modules.Financeiro.Application;

/// <summary>
/// Módulo Financeiro — o CORAÇÃO do ERP (docs/financeiro/financeiro-features.md, missão do
/// módulo). Registra os casos de uso, os read-models de leitura e os handlers dos eventos de
/// integração que o Financeiro ASSINA: <c>venda.concluida</c>, <c>venda.estornada</c>,
/// <c>compra.recebida</c>, <c>os.faturada</c>, <c>pedido.pago</c>, <c>folha.lancada</c>
/// (ver <c>SistemaX.Modules.Abstractions.IntegrationEvents</c>).
///
/// DECISÃO DE DESIGN — por que <c>ParcelaVencida</c>/<c>ParcelaBaixada</c> normalmente não têm
/// handler aqui: pelo próprio catálogo de docs/financeiro-datamodel.md §4.2, a ORIGEM desses
/// eventos é o PRÓPRIO Financeiro (<see cref="AvaliarParcelasVencidasUseCase"/>/
/// <c>BaixarParcelaUseCase</c>) — registrar um handler para consumir um evento que o módulo mesmo
/// produz seria, em geral, um ciclo sem propósito. EXCEÇÃO — <c>DunningAssinaturaHandler</c> (P1-4,
/// docs/financeiro/revisao-domain-fit-cnpj.md): dentro do Financeiro, <c>Assinatura</c> é um
/// agregado DIFERENTE de <c>ContaAReceber</c>/<c>ContaAPagar</c> — ela precisa reagir a "sua"
/// cobrança vencer/liquidar pra acionar a FSM de dunning, exatamente como qualquer outro
/// consumidor cross-agregado. Não é o mesmo ciclo (produtor e consumidor são agregados distintos).
///
/// DECISÃO DE DESIGN — por que os ADAPTERS concretos dos ports (repositórios) NÃO são
/// registrados aqui: o grafo de referência de projeto da solução é
/// <c>Infrastructure → Application → Domain</c> (nunca o inverso) — este assembly não pode
/// referenciar os adapters concretos de <c>SistemaX.Modules.Financeiro.Infrastructure</c> sem
/// criar uma dependência circular. Quem registra os adapters é
/// <c>FinanceiroInfrastructureModule</c> (Infrastructure), um segundo <see cref="IModule"/> com
/// <c>DependeDe: ["financeiro"]</c> — o Host descobre e registra os dois via DI, na ordem certa.
/// </summary>
public sealed class FinanceiroModule : IModule
{
    public string Codigo => "financeiro";
    public string Nome => "Financeiro";

    public void Registrar(IServiceCollection services, IModuleContext contexto)
    {
        services.AddScoped<ResolvedorDePrazoDeCompensacao>();
        services.AddScoped<IIntegrationEventHandler<VendaConcluida>, VendaConcluidaHandler>();
        services.AddScoped<IIntegrationEventHandler<VendaEstornada>, VendaEstornadaHandler>();
        services.AddScoped<IIntegrationEventHandler<CompraRecebida>, CompraRecebidaHandler>();
        services.AddScoped<IIntegrationEventHandler<CompraEstornada>, CompraEstornadaHandler>();
        services.AddScoped<IIntegrationEventHandler<OsFaturada>, OsFaturadaHandler>();
        services.AddScoped<IIntegrationEventHandler<PedidoPago>, PedidoPagoHandler>();
        services.AddScoped<IIntegrationEventHandler<FolhaLancada>, FolhaLancadaHandler>();

        // P1-4 — dunning consumindo os próprios eventos do Financeiro (exceção documentada acima).
        services.AddScoped<IIntegrationEventHandler<ParcelaVencida>, DunningAssinaturaHandler>();
        services.AddScoped<IIntegrationEventHandler<ParcelaBaixada>, DunningAssinaturaHandler>();

        services.AddScoped<EstornarMovimentoUseCase>();
        services.AddScoped<LancarContaAPagarUseCase>();
        services.AddScoped<LancarContaAReceberUseCase>();
        services.AddScoped<BaixarParcelaUseCase>();
        services.AddScoped<ConciliarMovimentoUseCase>();
        services.AddScoped<AvaliarParcelasVencidasUseCase>();

        // Assinaturas / receita recorrente (a lente de Recorrentes)
        services.AddScoped<CriarAssinaturaUseCase>();
        services.AddScoped<CancelarAssinaturaUseCase>();
        services.AddScoped<PausarReativarAssinaturaUseCase>();
        services.AddScoped<AlterarValorAssinaturaUseCase>();
        services.AddScoped<VincularProjetoAssinaturaUseCase>();

        // Análise por Projeto (docs/financeiro/design-analise-por-projeto.md, Parte A) — CRUD de
        // Projeto (opt-in via AnalisePorProjetoGuard) + Painel v1 (MRR/churn/LTV/MC1 por projeto).
        services.AddScoped<CriarProjetoUseCase>();
        services.AddScoped<EditarProjetoUseCase>();
        services.AddScoped<ArquivarReativarProjetoUseCase>();
        services.AddScoped<PainelDoProjetoService>();

        // Análise por Projeto — Parte B: P3 (ativo de capital amortizável/depreciável GERAL,
        // docs/financeiro/design-analise-por-projeto.md §3.3, docs/financeiro/design-imobilizado-roi.md
        // §3.1) + P4 (apontamento de tempo, só minutos por decisão do dono — §3.4).
        services.AddScoped<CriarAtivoDeCapitalUseCase>();
        services.AddScoped<BaixarAtivoDeCapitalUseCase>();
        services.AddScoped<ReconhecerAmortizacoesUseCase>();
        services.AddScoped<RegistrarApontamentoUseCase>();
        services.AddScoped<ExcluirApontamentoUseCase>();
        services.AddScoped<ResumoDeTempoService>();

        // Imobilizado + Painel de ROI do negócio (docs/financeiro/design-imobilizado-roi.md) —
        // o Imobilizado tangível REUSA CriarAtivoDeCapitalUseCase/BaixarAtivoDeCapitalUseCase acima
        // (mesmo agregado, §2.2 "um handler só, dois gates" — ver ExecutarImobilizadoAsync).
        // AporteDeCapital é a única entidade nova (leve, fora da partida dobrada).
        services.AddScoped<RegistrarAporteDeCapitalUseCase>();
        services.AddScoped<ExcluirAporteDeCapitalUseCase>();
        services.AddScoped<RoiDoNegocioService>();

        // Motor de recorrência (geração de contas/cobranças)
        services.AddScoped<GerarContasRecorrentesUseCase>();
        services.AddScoped<GerarCobrancasAssinaturasUseCase>();

        // P1-4 — dunning (relógio de graça) + painel de movimentos de MRR.
        services.AddScoped<AvaliarDunningAssinaturasUseCase>();
        services.AddScoped<PainelDeMovimentosMrrService>();

        services.AddScoped<FluxoDeCaixaService>();
        services.AddScoped<DreGerencialService>();
        services.AddScoped<AccrualsService>();
        services.AddScoped<ConcentracaoDeReceitaService>();
        services.AddScoped<QuantoSobrouDeVerdadeService>();
        services.AddScoped<AlertaFinanceiroService>();
        services.AddScoped<ReceitaRecorrenteService>();

        // Telas restantes do Financeiro (docs/wiring/financeiro-telas-restantes.md) — Entradas &
        // Saídas (extrato unificado), Relatórios (contas em aberto + aging) e Recorrentes
        // (detalhe por assinatura + lente Contas fixas). DRE (§B) e MRR (§C) reusam serviços já
        // registrados acima (DreGerencialService/ReceitaRecorrenteService) — nada novo pra eles.
        services.AddScoped<ExtratoUnificadoService>();
        services.AddScoped<ContasEmAbertoService>();
        services.AddScoped<AssinaturaDetalheService>();
        services.AddScoped<ContasFixasService>();

        // Bancário (docs/wiring/financeiro-telas-restantes.md §3) — contas/caixas com saldo
        // derivado e formas de pagamento com MDR/lag, o LAR ÚNICO que fato_recebiveis também consome.
        services.AddScoped<ContasBancariasService>();
        services.AddScoped<FormasDePagamentoService>();
        services.AddScoped<ResolvedorDeDescricaoDeMovimento>();
        services.AddScoped<MovimentosBancariosService>();
        services.AddScoped<MovimentosSemanaisService>();
        services.AddScoped<ConciliacaoBancariaService>();
        services.AddScoped<TaxasPorFormaService>();

        // Fluxo de Caixa (docs/wiring/financeiro-telas-restantes.md §4) — o RITUAL de caixa físico
        // (abrir gaveta/sangria/suprimento/fechar contando), não a projeção de saldo de
        // FluxoDeCaixaService/GET /financeiro/fluxo (colisão de nome, não de conceito).
        services.AddScoped<AbrirSessaoCaixaUseCase>();
        services.AddScoped<MovimentarSessaoCaixaUseCase>();
        services.AddScoped<FecharSessaoCaixaUseCase>();

        // F1 do plano de inteligência do Financeiro — orquestração do motor quant
        // (Application.Quant, funções puras) sobre os ports reais.
        services.AddScoped<PrevisaoDeCaixaService>();
        services.AddScoped<PontoDeEquilibrioService>();
        services.AddScoped<InadimplenciaService>();
        services.AddScoped<RadarDoSimplesService>();

        // Fase 2 do plano de inteligência do Financeiro — Super Consultor real (ADR-0005 §3.5):
        // o Financeiro registra o SEU provider de fatos (R5 — "cada módulo registra o seu via
        // DI"). O orquestrador module-agnostic (ConsultorService/IConsultorNarrador/
        // IConsultorInsightCache) é registrado no composition root (SistemaXHost), não aqui —
        // ele não pertence a nenhum módulo específico.
        services.AddScoped<IConsultorFactProvider, FinanceiroConsultorFactProvider>();
    }
}
