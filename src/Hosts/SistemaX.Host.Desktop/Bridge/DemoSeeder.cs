using SistemaX.Infrastructure.Local.Kv;
using SistemaX.Modules.Estoque.Application.CasosDeUso;
using SistemaX.Modules.Estoque.Application.Comum;
using SistemaX.Modules.Estoque.Application.Ports;
using SistemaX.Modules.Estoque.Domain.Catalogo;
using SistemaX.Modules.Estoque.Domain.Comum;
using SistemaX.Modules.Financeiro.Application.Ativos;
using SistemaX.Modules.Financeiro.Application.Caixa;
using SistemaX.Modules.Financeiro.Application.CasosDeUso;
using SistemaX.Modules.Financeiro.Application.Categorias;
using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Application.Projetos;
using SistemaX.Modules.Financeiro.Domain.Ativos;
using SistemaX.Modules.Financeiro.Domain.Caixa;
using SistemaX.Modules.Financeiro.Domain.Comum;
using SistemaX.Modules.Financeiro.Domain.Configuracao;
using SistemaX.Modules.Financeiro.Domain.Projetos;
using SistemaX.Modules.Financeiro.Domain.Recorrencia;
using SistemaX.Modules.Vendas.Application.CasosDeUso;
using SistemaX.Modules.Vendas.Domain;
using SistemaX.SharedKernel;
using SistemaX.Verticals.Assistencia;
using SistemaX.Verticals.Assistencia.Application.CasosDeUso;
using SistemaX.Verticals.Assistencia.Application.Ports;

namespace SistemaX.Host.Desktop.Bridge;

/// <summary>
/// Semente IDEMPOTENTE (todo passo tem sua própria guarda por existência — rodar em TODO boot
/// nunca duplica) do tenant demo (<c>loja-demo</c>): povoa as 3 correntes de receita (Recorrente/
/// Serviço/Comércio), os dois projetos de exemplo (DigiSat/Aevo, docs/financeiro/design-analise-
/// por-projeto.md) e o Imobilizado/ROI do negócio (docs/financeiro/design-imobilizado-roi.md) — tudo
/// via CASOS DE USO REAIS (nunca escrita direta em fact table), pra que os painéis do Financeiro
/// (Visão Geral, DRE por corrente, Projetos, ROI do negócio, Recebíveis, Radar do Simples) mostrem
/// número de verdade assim que o founder loga (PIN 1234) — nunca tela vazia.
///
/// ORDEM IMPORTA: toggles (§1) antes de Projeto/Imobilizado (gated por
/// <c>AnalisePorProjetoGuard</c>/<c>FinanceiroOptInGuard</c>) antes de Assinatura/Ativo/Aporte
/// (referenciam <c>ProjetoId</c>) antes das liquidações (precisam da <c>ContaAPagar</c>/
/// <c>ContaAReceber</c> já materializada). Cada passo resolve seu próprio escopo de DI — nenhum
/// estado é compartilhado entre passos além dos ids devolvidos (tenant/projeto).
///
/// IDEMPOTÊNCIA — 3 técnicas, conforme o que cada porta oferece:
/// (1) chave natural + busca antes de criar (Projeto por nome, Assinatura por cliente+serviço,
///     AtivoDeCapital por nome+projeto, Recorrencia/Aporte por descrição — mesmo racional de
///     <c>FinanceiroBootstrapSeeder</c>);
/// (2) o próprio caso de uso (<c>GerarCobrancasAssinaturasUseCase</c>/<c>GerarContasRecorrentesUseCase</c>/
///     <c>ReconhecerAmortizacoesUseCase</c>/<c>BaixarParcelaUseCase</c> são crons idempotentes por
///     natureza — chamados em TODO boot, de propósito, pra a demo seguir "andando" mês a mês);
/// (3) <see cref="IAppKeyValueStore"/> como flag manual só para os dois lotes SEM chave natural
///     nem read-model de listagem barato (Vendas/OS — <c>IVendaRepository</c> não expõe
///     <c>ListarAsync</c> por tenant, ver doc da classe).
/// </summary>
public static class DemoSeeder
{
    public static async Task SemearAsync(IServiceProvider provider, string businessId, CancellationToken ct = default)
    {
        await using var scope = provider.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var agora = DateTimeOffset.UtcNow;

        // 1) Toggles — os painéis de Projeto/ROI só aparecem com os dois opt-ins ligados.
        await SemearConfiguracaoAsync(sp, businessId, ct).ConfigureAwait(false);

        // 2) Catálogo (Comércio + peças de Serviço) — precisa existir antes de Vendas/OS.
        await SemearProdutosAsync(sp, businessId, ct).ConfigureAwait(false);

        // 3) Projetos DigiSat/Aevo (dimensão que Assinatura/AtivoDeCapital/Recorrencia referenciam).
        var (digisat, aevo) = await SemearProjetosAsync(sp, businessId, ct).ConfigureAwait(false);

        // 4) Ativo de capital: licença DigiSat (intangível, projeto) + Imobilizado (tangível, geral).
        await SemearAtivosDeCapitalAsync(sp, businessId, digisat?.Id, agora, ct).ConfigureAwait(false);

        // 5) Assinaturas (corrente Recorrente) — demo original + DigiSat + Aevo, um lote só.
        await SemearAssinaturasAsync(sp, businessId, digisat?.Id, aevo?.Id, ct).ConfigureAwait(false);

        // 6) Custo de IA recorrente do Aevo (despesa mensal tageada no projeto).
        await SemearRecorrenciaCustoDeIaAsync(sp, businessId, aevo?.Id, agora, ct).ConfigureAwait(false);

        // 7) Aportes de capital de giro (denominador do ROI do negócio).
        await SemearAportesDeCapitalAsync(sp, businessId, agora, ct).ConfigureAwait(false);

        // 8) Vendas avulsas (corrente Comércio) + Ordens de Serviço faturadas (corrente Serviço).
        await SemearVendasAsync(sp, businessId, agora, ct).ConfigureAwait(false);
        await SemearOrdensDeServicoAsync(sp, businessId, agora, ct).ConfigureAwait(false);

        // 9) Crons idempotentes — rodam em TODO boot (nunca só na primeira vez): materializam
        // cobranças/contas recorrentes devidas e reconhecem competência de amortização/depreciação
        // até hoje, pra a demo "andar" mês a mês mesmo sem o BackgroundService real ainda ter
        // ticado nesta sessão.
        await sp.GetRequiredService<GerarCobrancasAssinaturasUseCase>().ExecutarAsync(businessId, agora, ct).ConfigureAwait(false);
        await sp.GetRequiredService<GerarContasRecorrentesUseCase>().ExecutarAsync(businessId, agora, ct).ConfigureAwait(false);
        await sp.GetRequiredService<ReconhecerAmortizacoesUseCase>().ExecutarAsync(businessId, agora, ct).ConfigureAwait(false);

        // 10) Liquidações — dão forma à série temporal do ROI/payback (caixa realizado), sempre
        // via BaixarParcelaUseCase (idempotente por IdempotencyKey determinística).
        await LiquidarParcelasDeAtivosVencidasAsync(sp, businessId, agora, ct).ConfigureAwait(false);
        await LiquidarRecebiveisDeProjetoAsync(sp, businessId, digisat?.Id, agora, ct).ConfigureAwait(false);
        await LiquidarRecebiveisDeProjetoAsync(sp, businessId, aevo?.Id, agora, ct).ConfigureAwait(false);
    }

    // ── 1) Toggles ──────────────────────────────────────────────────────────────────────────

    private static async Task SemearConfiguracaoAsync(IServiceProvider sp, string businessId, CancellationToken ct)
    {
        var repo = sp.GetRequiredService<IConfiguracaoFinanceiraTenantRepository>();
        var atual = await repo.ObterAsync(businessId, ct).ConfigureAwait(false) ?? ConfiguracaoFinanceiraTenant.Padrao(businessId);
        if (atual.AnalisePorProjetoAtiva && atual.ImobilizadoRoiAtivo)
        {
            return;
        }

        var resultado = ConfiguracaoFinanceiraTenant.Criar(
            businessId, analisePorProjetoAtiva: true, atual.CustoHoraPadraoCentavos, atual.TempoEntraNoDre,
            imobilizadoRoiAtivo: true, atual.TaxaDescontoAnualBps, atual.InicioOperacao);
        if (resultado.Falha)
            throw new InvalidOperationException($"DemoSeeder: falha ao ligar toggles do Financeiro: {resultado.Erro.Mensagem}");

        await repo.SalvarAsync(resultado.Valor, ct).ConfigureAwait(false);
    }

    // ── 2) Catálogo ─────────────────────────────────────────────────────────────────────────

    private static async Task SemearProdutosAsync(IServiceProvider sp, string businessId, CancellationToken ct)
    {
        var repo = sp.GetRequiredService<IProdutoRepository>();
        if ((await repo.ListarAsync(businessId, ct).ConfigureAwait(false)).Count > 0)
        {
            return;
        }

        var entradaManual = sp.GetRequiredService<RegistrarEntradaManualUseCase>();
        var agora = DateTimeOffset.UtcNow;

        async Task NovoAsync(string nome, UnidadeDeMedida unidade, long precoCentavos, string categoria, long custoCentavos, int quantidadeInicial)
        {
            var resultado = Produto.Criar(businessId, nome, unidade, precoVenda: new Money(precoCentavos), categoria: categoria);
            if (!resultado.Sucesso) return;

            var produto = resultado.Valor;
            await repo.SalvarAsync(produto, ct).ConfigureAwait(false);

            var entrada = await entradaManual.ExecutarAsync(
                businessId, produto.Id, Quantidade.DeInteiro(quantidadeInicial), new Money(custoCentavos), "Estoque inicial — demo",
                EstoqueConstantes.OperadorSistema, EstoqueConstantes.OperadorSistemaNome, agora, ct).ConfigureAwait(false);
            if (entrada.Falha)
                throw new InvalidOperationException($"DemoSeeder: falha na entrada de estoque de '{nome}': {entrada.Erro.Mensagem}");
        }

        // Comércio (venda de balcão) — mesmo catálogo de sempre.
        await NovoAsync("Refrigerante Lata 350ml", UnidadeDeMedida.UN, 550, "Bebidas", 320, 60).ConfigureAwait(false);
        await NovoAsync("Pão Francês", UnidadeDeMedida.KG, 1490, "Padaria", 700, 30).ConfigureAwait(false);
        await NovoAsync("Óleo de Soja 900ml", UnidadeDeMedida.UN, 890, "Mercearia", 550, 40).ConfigureAwait(false);

        // Serviço (peça aplicada em Ordem de Serviço) — CMV real via custo médio da entrada acima.
        await NovoAsync("Tela Celular Compatível", UnidadeDeMedida.UN, 15000, "Peças", 8000, 15).ConfigureAwait(false);
        await NovoAsync("Fonte Notebook Dell 65W", UnidadeDeMedida.UN, 8990, "Peças", 4500, 10).ConfigureAwait(false);
    }

    // ── 3) Projetos ─────────────────────────────────────────────────────────────────────────

    private static async Task<(Projeto? DigiSat, Projeto? Aevo)> SemearProjetosAsync(IServiceProvider sp, string businessId, CancellationToken ct)
    {
        var projetos = sp.GetRequiredService<IProjetoRepository>();
        var criar = sp.GetRequiredService<CriarProjetoUseCase>();

        async Task<Projeto> ObterOuCriarAsync(string nome, string descricao)
        {
            var existente = await projetos.BuscarPorNomeAsync(businessId, nome, ct).ConfigureAwait(false);
            if (existente is not null) return existente;

            var resultado = await criar.ExecutarAsync(new CriarProjetoComando(businessId, nome, descricao), ct).ConfigureAwait(false);
            if (resultado.Falha)
                throw new InvalidOperationException($"DemoSeeder: falha ao criar projeto '{nome}': {resultado.Erro.Mensagem}");
            return resultado.Valor;
        }

        var digisat = await ObterOuCriarAsync("DigiSat", "Licenciamento do sistema DigiSat para lojas parceiras.").ConfigureAwait(false);
        var aevo = await ObterOuCriarAsync("Aevo", "Plataforma Aevo — assinaturas de clientes + custo de infraestrutura de IA.").ConfigureAwait(false);
        return (digisat, aevo);
    }

    // ── 4) Ativo de capital (DigiSat + Imobilizado) ────────────────────────────────────────

    private static async Task SemearAtivosDeCapitalAsync(IServiceProvider sp, string businessId, string? digisatProjetoId, DateTimeOffset agora, CancellationToken ct)
    {
        var ativosRepo = sp.GetRequiredService<IAtivoDeCapitalRepository>();
        var criarAtivo = sp.GetRequiredService<CriarAtivoDeCapitalUseCase>();
        var mesAtual = new DateOnly(agora.Year, agora.Month, 1);

        async Task<bool> ExisteAsync(string? projetoId, string nome)
            => (await ativosRepo.ListarAsync(businessId, projetoId, ct).ConfigureAwait(false)).Any(a => a.Nome == nome);

        // DigiSat — licença intangível (docs/financeiro/design-analise-por-projeto.md §3.3): custo
        // R$6.895,00 (7×985) parcelado em 7×, vida útil 36 meses, 5 unidades (capacidade/ociosidade
        // do painel). Comprada há 7 meses — todas as parcelas já venceram (liquidadas no passo 10)
        // e 8 competências já reconhecidas (passo 9), dando forma real a payback/ociosidade/margem.
        if (digisatProjetoId is not null && !await ExisteAsync(digisatProjetoId, "Licença DigiSat (5 unidades)").ConfigureAwait(false))
        {
            var inicioDigiSat = mesAtual.AddMonths(-7);
            var parcelasDigiSat = ParcelasMensais(inicioDigiSat, 7, 98_500);

            var comando = new CriarAtivoDeCapitalComando(
                businessId, "Licença DigiSat (5 unidades)", NaturezaAtivo.Intangivel, CategoriaAtivo.LicencaSoftware,
                CustoAquisicaoCentavos: 689_500, DataAquisicao: inicioDigiSat, VidaUtilMeses: 36,
                QuantidadeUnidades: 5, ProjetoId: digisatProjetoId, Parcelas: parcelasDigiSat);

            var resultado = await criarAtivo.ExecutarAsync(comando, ct).ConfigureAwait(false);
            if (resultado.Falha)
                throw new InvalidOperationException($"DemoSeeder: falha ao criar ativo DigiSat: {resultado.Erro.Mensagem}");
        }

        // Imobilizado (docs/financeiro/design-imobilizado-roi.md) — mesmo elenco do mockup
        // roi-negocio.html: reforma, bancada, móveis, placa e computador. Reforma/móveis/placa
        // pagos fora do sistema (trilho B do capex — só a competência entra no cronograma);
        // bancada/computador parcelados (trilho A — liquidados no passo 10).
        async Task NovoImobilizadoAsync(string nome, CategoriaAtivo categoria, long custoCentavos, int vidaUtilMeses, int mesesAtras, IReadOnlyList<ParcelaInvestimento>? parcelas)
        {
            if (await ExisteAsync(null, nome).ConfigureAwait(false)) return;

            var dataAquisicao = mesAtual.AddMonths(-mesesAtras);
            var comando = new CriarAtivoDeCapitalComando(
                businessId, nome, NaturezaAtivo.Tangivel, categoria, custoCentavos, dataAquisicao, vidaUtilMeses, Parcelas: parcelas);

            var resultado = await criarAtivo.ExecutarImobilizadoAsync(comando, ct).ConfigureAwait(false);
            if (resultado.Falha)
                throw new InvalidOperationException($"DemoSeeder: falha ao criar ativo imobilizado '{nome}': {resultado.Erro.Mensagem}");
        }

        await NovoImobilizadoAsync("Reforma da loja", CategoriaAtivo.Reforma, 800_000, 60, mesesAtras: 3, parcelas: null).ConfigureAwait(false);
        await NovoImobilizadoAsync(
            "Bancada de atendimento", CategoriaAtivo.Equipamento, 240_000, 60, mesesAtras: 3,
            parcelas: ParcelasMensais(mesAtual.AddMonths(-3), 3, 80_000)).ConfigureAwait(false);
        await NovoImobilizadoAsync("Móveis de recepção", CategoriaAtivo.Moveis, 360_000, 60, mesesAtras: 2, parcelas: null).ConfigureAwait(false);
        await NovoImobilizadoAsync("Placa de comunicação visual", CategoriaAtivo.ComunicacaoVisual, 180_000, 36, mesesAtras: 2, parcelas: null).ConfigureAwait(false);
        await NovoImobilizadoAsync(
            "Computador de bancada", CategoriaAtivo.Computador, 320_000, 36, mesesAtras: 1,
            // 4 parcelas mensais a partir de 1 mês atrás: 2 já vencidas (liquidadas), 2 futuras em
            // aberto — dá dado real pra "parcelas em aberto" da projeção de payback (§9.5/§7.7).
            parcelas: ParcelasMensais(mesAtual.AddMonths(-1), 4, 80_000)).ConfigureAwait(false);
    }

    private static List<ParcelaInvestimento> ParcelasMensais(DateOnly inicio, int quantidade, long valorCentavos)
        => Enumerable.Range(0, quantidade).Select(i => new ParcelaInvestimento(NovaData(inicio.AddMonths(i), dia: 5), valorCentavos)).ToList();

    private static DateTimeOffset NovaData(DateOnly mes, int dia)
    {
        var diaValido = Math.Min(dia, DateTime.DaysInMonth(mes.Year, mes.Month));
        return new DateTimeOffset(mes.Year, mes.Month, diaValido, 12, 0, 0, TimeSpan.Zero);
    }

    // ── 5) Assinaturas (corrente Recorrente) ───────────────────────────────────────────────

    private static async Task SemearAssinaturasAsync(IServiceProvider sp, string businessId, string? digisatProjetoId, string? aevoProjetoId, CancellationToken ct)
    {
        var repo = sp.GetRequiredService<IAssinaturaRepository>();
        if ((await repo.ListarAsync(businessId, ct).ConfigureAwait(false)).Count > 0)
        {
            return;
        }

        var criar = sp.GetRequiredService<CriarAssinaturaUseCase>();
        var cancelar = sp.GetRequiredService<CancelarAssinaturaUseCase>();

        async Task NovaAsync(string servicoId, string servicoNome, string cliente, long centavos, int mesesAtras, string? projetoId = null)
            => await criar.ExecutarAsync(new CriarAssinaturaComando(
                businessId, cliente.ToLowerInvariant().Replace(' ', '-'), cliente, servicoId, servicoNome,
                new Money(centavos), FrequenciaRecorrencia.Mensal, 5, DateTimeOffset.UtcNow.AddMonths(-mesesAtras), projetoId)).ConfigureAwait(false);

        await NovaAsync("servicepro", "ServicePro", "Mercado Sao Joao", 34900, 2).ConfigureAwait(false);
        await NovaAsync("servicepro", "ServicePro", "Padaria Pao Quente", 34900, 3).ConfigureAwait(false);
        await NovaAsync("servicepro", "ServicePro", "Auto Pecas Silva", 34900, 4).ConfigureAwait(false);
        await NovaAsync("gestao-raiz", "Gestao Raiz Fiscal", "Distribuidora Norte", 89000, 0).ConfigureAwait(false);
        await NovaAsync("gestao-raiz", "Gestao Raiz Fiscal", "Posto Bandeira", 120000, 5).ConfigureAwait(false);
        await NovaAsync("brain", "Brain", "Consultoria Abraao", 22000, 3).ConfigureAwait(false);

        // DigiSat — a assinatura que consome a licença semeada no passo 4 (payback/margem reais).
        await NovaAsync("digisat-pdv", "DigiSat PDV", "Farmacia Vida Nova", 28000, 5, digisatProjetoId).ConfigureAwait(false);

        // Aevo — 2 assinaturas somando MRR ~R$1.200 (mockup roi-negocio.html/projeto.html).
        await NovaAsync("aevo-plataforma", "Aevo Plataforma", "Agencia Nexus Criativa", 70000, 4, aevoProjetoId).ConfigureAwait(false);
        await NovaAsync("aevo-plataforma", "Aevo Plataforma", "Estudio Aevo Labs", 50000, 3, aevoProjetoId).ConfigureAwait(false);

        async Task ChurnAsync(string servicoId, string servicoNome, string cliente, long centavos, string motivo)
        {
            var resultado = await criar.ExecutarAsync(new CriarAssinaturaComando(
                businessId, cliente.ToLowerInvariant().Replace(' ', '-'), cliente, servicoId, servicoNome,
                new Money(centavos), FrequenciaRecorrencia.Mensal, 5, DateTimeOffset.UtcNow.AddMonths(-6))).ConfigureAwait(false);
            await cancelar.ExecutarAsync(businessId, resultado.Valor.Id, motivo).ConfigureAwait(false);
        }

        await ChurnAsync("servicepro", "ServicePro", "Salao Bella", 34900, "cancelou o plano").ConfigureAwait(false);
    }

    // ── 6) Custo de IA recorrente do Aevo ──────────────────────────────────────────────────

    private static async Task SemearRecorrenciaCustoDeIaAsync(IServiceProvider sp, string businessId, string? aevoProjetoId, DateTimeOffset agora, CancellationToken ct)
    {
        if (aevoProjetoId is null) return;

        const string descricao = "Custo de infraestrutura de IA — Aevo";
        var repo = sp.GetRequiredService<IRecorrenciaRepository>();
        var existente = (await repo.ListarAtivasAsync(businessId, ct).ConfigureAwait(false)).Any(r => r.Descricao == descricao);
        if (existente) return;

        var mesAtual = new DateOnly(agora.Year, agora.Month, 1);
        var criado = Recorrencia.Criar(
            businessId, descricao, TipoContaRecorrente.APagar, new Money(34_000), "custo-infraestrutura-ia",
            FrequenciaRecorrencia.Mensal, NovaData(mesAtual.AddMonths(-4), dia: 5), diaFixo: 5, projetoId: aevoProjetoId);
        if (criado.Falha)
            throw new InvalidOperationException($"DemoSeeder: falha ao criar recorrência de custo de IA do Aevo: {criado.Erro.Mensagem}");

        await repo.SalvarAsync(criado.Valor, ct).ConfigureAwait(false);
    }

    // ── 7) Aportes de capital ───────────────────────────────────────────────────────────────

    private static async Task SemearAportesDeCapitalAsync(IServiceProvider sp, string businessId, DateTimeOffset agora, CancellationToken ct)
    {
        var repo = sp.GetRequiredService<IAporteDeCapitalRepository>();
        var registrar = sp.GetRequiredService<RegistrarAporteDeCapitalUseCase>();
        var mesAtual = new DateOnly(agora.Year, agora.Month, 1);

        async Task NovoAsync(string descricao, long valorCentavos, int mesesAtras)
        {
            var existentes = await repo.ListarAsync(businessId, ct).ConfigureAwait(false);
            if (existentes.Any(a => a.Descricao == descricao)) return;

            var resultado = await registrar.ExecutarAsync(
                new RegistrarAporteDeCapitalComando(businessId, valorCentavos, mesAtual.AddMonths(-mesesAtras), descricao), ct).ConfigureAwait(false);
            if (resultado.Falha)
                throw new InvalidOperationException($"DemoSeeder: falha ao registrar aporte '{descricao}': {resultado.Erro.Mensagem}");
        }

        await NovoAsync("Aporte inicial de capital de giro", 500_000, 6).ConfigureAwait(false);
        await NovoAsync("Reforço de caixa", 200_000, 2).ConfigureAwait(false);
    }

    // ── 8a) Vendas avulsas (corrente Comércio) ─────────────────────────────────────────────

    private static async Task SemearVendasAsync(IServiceProvider sp, string businessId, DateTimeOffset agora, CancellationToken ct)
    {
        var kv = sp.GetRequiredService<IAppKeyValueStore>();
        var chave = $"demo-seed:vendas:{businessId}";
        if (await kv.GetAsync(chave, ct).ConfigureAwait(false) == "1") return;

        var produtosRepo = sp.GetRequiredService<IProdutoRepository>();
        var catalogo = await produtosRepo.ListarAsync(businessId, ct).ConfigureAwait(false);
        var refrigerante = catalogo.FirstOrDefault(p => p.Nome == "Refrigerante Lata 350ml");
        var pao = catalogo.FirstOrDefault(p => p.Nome == "Pão Francês");
        var oleo = catalogo.FirstOrDefault(p => p.Nome == "Óleo de Soja 900ml");
        if (refrigerante is null || pao is null || oleo is null) return; // catálogo ainda não semeado — tenta no próximo boot

        var iniciar = sp.GetRequiredService<IniciarVendaUseCase>();
        var montar = sp.GetRequiredService<MontarVendaUseCase>();
        var concluir = sp.GetRequiredService<ConcluirVendaUseCase>();

        async Task NovaVendaAsync(MetodoPagamento metodo, IReadOnlyList<(Produto Produto, int Quantidade)> itens)
        {
            var vendaResultado = await iniciar.ExecutarAsync(businessId, ct).ConfigureAwait(false);
            if (vendaResultado.Falha)
                throw new InvalidOperationException($"DemoSeeder: falha ao abrir venda demo: {vendaResultado.Erro.Mensagem}");
            var venda = vendaResultado.Valor;

            var total = Money.Zero;
            foreach (var (produto, quantidade) in itens)
            {
                var item = await montar.AdicionarItemAsync(venda.Id, produto.Id, produto.Nome, quantidade, produto.PrecoVenda, ct).ConfigureAwait(false);
                if (item.Falha)
                    throw new InvalidOperationException($"DemoSeeder: falha ao adicionar '{produto.Nome}' na venda demo: {item.Erro.Mensagem}");
                total += produto.PrecoVenda * quantidade;
            }

            var valorRecebido = metodo == MetodoPagamento.Dinheiro ? total : (Money?)null;
            var pagamento = await montar.RegistrarPagamentoAsync(venda.Id, metodo, total, valorRecebido, agora, ct).ConfigureAwait(false);
            if (pagamento.Falha)
                throw new InvalidOperationException($"DemoSeeder: falha ao registrar pagamento da venda demo: {pagamento.Erro.Mensagem}");

            var conclusao = await concluir.ExecutarAsync(venda.Id, ct).ConfigureAwait(false);
            if (conclusao.Falha)
                throw new InvalidOperationException($"DemoSeeder: falha ao concluir venda demo: {conclusao.Erro.Mensagem}");
        }

        // À vista (dinheiro/pix) — vira ContaAReceber já liquidada + MovimentoFinanceiro imediato.
        await NovaVendaAsync(MetodoPagamento.Dinheiro, [(refrigerante, 3), (oleo, 2)]).ConfigureAwait(false);
        await NovaVendaAsync(MetodoPagamento.Pix, [(pao, 2)]).ConfigureAwait(false);

        // Cartão de crédito — recebível aberto com MDR/lag (painel de Recebíveis).
        await NovaVendaAsync(MetodoPagamento.Credito, [(refrigerante, 5), (pao, 1)]).ConfigureAwait(false);

        await kv.SetAsync(chave, "1", ct).ConfigureAwait(false);
    }

    // ── 8b) Ordens de Serviço faturadas (corrente Serviço) ─────────────────────────────────

    private static async Task SemearOrdensDeServicoAsync(IServiceProvider sp, string businessId, DateTimeOffset agora, CancellationToken ct)
    {
        var kv = sp.GetRequiredService<IAppKeyValueStore>();
        var chave = $"demo-seed:ordens-servico:{businessId}";
        if (await kv.GetAsync(chave, ct).ConfigureAwait(false) == "1") return;

        var produtosRepo = sp.GetRequiredService<IProdutoRepository>();
        var catalogo = await produtosRepo.ListarAsync(businessId, ct).ConfigureAwait(false);
        var tela = catalogo.FirstOrDefault(p => p.Nome == "Tela Celular Compatível");
        var fonte = catalogo.FirstOrDefault(p => p.Nome == "Fonte Notebook Dell 65W");
        if (tela is null || fonte is null) return; // catálogo ainda não semeado — tenta no próximo boot

        var abrir = sp.GetRequiredService<AbrirOsUseCase>();
        var gerenciar = sp.GetRequiredService<GerenciarOrdemDeServicoUseCase>();
        var faturamento = sp.GetRequiredService<OrdemDeServicoFaturamentoUseCases>();

        static void GarantirSucesso(Result resultado, string onde)
        {
            if (resultado.Falha)
                throw new InvalidOperationException($"DemoSeeder: falha ao {onde}: {resultado.Erro.Mensagem}");
        }

        async Task NovaOsAsync(
            string numero, string clienteId, string clienteNome, string equipamentoTipo, string equipamentoMarca, string equipamentoModelo,
            string defeito, string diagnostico, string produtoId, string pecaDescricao, long precoPecaCentavos, long maoDeObraCentavos,
            FormaPagamento formaPagamento)
        {
            var abertura = agora.AddDays(-4);
            var cliente = new ClienteRef(clienteId, clienteNome);
            var equipamento = new Equipamento(equipamentoTipo, equipamentoMarca, equipamentoModelo);

            var abrirResultado = await abrir.ExecutarAsync(businessId, numero, cliente, equipamento, defeito, abertura, ct: ct).ConfigureAwait(false);
            if (abrirResultado.Falha)
                throw new InvalidOperationException($"DemoSeeder: falha ao abrir OS '{numero}': {abrirResultado.Erro.Mensagem}");
            var os = abrirResultado.Valor;

            GarantirSucesso(await gerenciar.AtribuirTecnicoAsync(os.Id, "tecnico-demo", "Rafael Souza", ct).ConfigureAwait(false), "atribuir técnico da OS demo");
            GarantirSucesso(
                await gerenciar.RegistrarDiagnosticoAsync(os.Id, diagnostico, abertura.AddHours(2), ct).ConfigureAwait(false),
                "registrar diagnóstico da OS demo");

            var peca = PecaOrcada.Nova(produtoId, pecaDescricao, 1, new Money(precoPecaCentavos));
            GarantirSucesso(
                await gerenciar.EnviarOrcamentoAsync(os.Id, [peca], new Money(maoDeObraCentavos), 7, abertura.AddHours(3), ct).ConfigureAwait(false),
                "enviar orçamento da OS demo");
            GarantirSucesso(
                await faturamento.RegistrarAprovacaoAsync(os.Id, CanalAprovacao.WhatsApp, abertura.AddHours(4), ct: ct).ConfigureAwait(false),
                "registrar aprovação da OS demo");
            GarantirSucesso(await gerenciar.IniciarExecucaoAsync(os.Id, abertura.AddDays(1), ct).ConfigureAwait(false), "iniciar execução da OS demo");
            GarantirSucesso(
                await faturamento.AplicarPecaAsync(os.Id, peca.LinhaId, abertura.AddDays(1).AddHours(1), ct).ConfigureAwait(false),
                "aplicar peça da OS demo");
            GarantirSucesso(
                await faturamento.ConcluirExecucaoAsync(os.Id, abertura.AddDays(1).AddHours(2), ct).ConfigureAwait(false),
                "concluir execução da OS demo");
            GarantirSucesso(
                await faturamento.EntregarAsync(os.Id, formaPagamento, Money.Zero, 90, abertura.AddDays(2), ct).ConfigureAwait(false),
                "entregar/faturar a OS demo");
        }

        await NovaOsAsync(
            "OS-DEMO-0001", "cliente-marcos-andrade", "Marcos Andrade", "Smartphone", "Samsung", "Galaxy A54",
            "Tela quebrada após queda", "Tela LCD trincada — necessita substituição completa.",
            tela.Id, "Tela Samsung Galaxy A54", 15000, 12000, FormaPagamento.Pix).ConfigureAwait(false);

        await NovaOsAsync(
            "OS-DEMO-0002", "cliente-doce-sabor", "Doceria Doce Sabor", "Notebook", "Dell", "Inspiron 15",
            "Não liga", "Fonte de alimentação queimada — necessita substituição.",
            fonte.Id, "Fonte Notebook Dell 65W", 8990, 15000, FormaPagamento.CartaoCredito).ConfigureAwait(false);

        await kv.SetAsync(chave, "1", ct).ConfigureAwait(false);
    }

    // ── 10) Liquidações (dão forma à série temporal do ROI/payback) ────────────────────────

    /// <summary>Liquida TODA parcela vencida de qualquer <c>ContaAPagar</c> categoria
    /// <c>ativo-de-capital</c> do tenant (DigiSat + Imobilizado, passo 4) — o trilho A do capex
    /// (docs/financeiro/design-imobilizado-roi.md §7.2, DI7) que <c>RoiDoNegocioService</c> lê.</summary>
    private static async Task LiquidarParcelasDeAtivosVencidasAsync(IServiceProvider sp, string businessId, DateTimeOffset agora, CancellationToken ct)
    {
        var contasAPagar = sp.GetRequiredService<IContaAPagarRepository>();
        var baixar = sp.GetRequiredService<BaixarParcelaUseCase>();

        var contas = await contasAPagar.ListarPorCategoriaAsync(businessId, CategoriaFinanceiraPadrao.AtivoDeCapital, ct).ConfigureAwait(false);
        foreach (var conta in contas)
        {
            foreach (var parcela in conta.Parcelas)
            {
                if (parcela.Status is not (StatusFinanceiro.Aberto or StatusFinanceiro.Parcial or StatusFinanceiro.Atrasado)) continue;
                if (parcela.Vencimento > agora) continue;

                var comando = new BaixarParcelaComando(
                    conta.Id, parcela.Id, parcela.Valor - parcela.ValorPago, parcela.Vencimento,
                    ClassificadorFormaPagamento.ContaCaixaPadraoId, "dinheiro", $"demo-seed:baixa-ativo:{parcela.Id}");

                var resultado = await baixar.BaixarParcelaDeContaAPagarAsync(comando, ct).ConfigureAwait(false);
                if (resultado.Falha)
                    throw new InvalidOperationException($"DemoSeeder: falha ao liquidar parcela de ativo '{parcela.Id}': {resultado.Erro.Mensagem}");
            }
        }
    }

    /// <summary>Liquida os recebíveis do projeto EXCETO o mais recente — deixa sempre pelo menos
    /// um "a receber" de verdade (painel de Recebíveis) enquanto materializa o caixa realizado que
    /// <c>PainelDoProjetoService.CalcularPaybackAsync</c>/<c>RoiDoNegocioService</c> leem. Roda em
    /// TODO boot: uma nova cobrança que vencer entre uma execução e outra é automaticamente pega
    /// pela mesma regra (idempotente por natureza — nunca reliquida o que já está Pago).</summary>
    private static async Task LiquidarRecebiveisDeProjetoAsync(IServiceProvider sp, string businessId, string? projetoId, DateTimeOffset agora, CancellationToken ct)
    {
        if (projetoId is null) return;

        var contasAReceber = sp.GetRequiredService<IContaAReceberRepository>();
        var baixar = sp.GetRequiredService<BaixarParcelaUseCase>();

        var abertas = (await contasAReceber.ListarAbertasAteAsync(businessId, agora, ct).ConfigureAwait(false))
            .Where(c => c.ProjetoId == projetoId)
            .OrderBy(c => c.DataCompetencia)
            .ToList();
        if (abertas.Count <= 1) return; // deixa a única (ou a mais recente) de verdade em aberto

        var contaBancoId = await ResolverContaBancoPrincipalAsync(sp, businessId, ct).ConfigureAwait(false);

        foreach (var conta in abertas.Take(abertas.Count - 1))
        {
            var parcela = conta.Parcelas[0];
            var comando = new BaixarParcelaComando(
                conta.Id, parcela.Id, parcela.Valor - parcela.ValorPago, parcela.Vencimento, contaBancoId, "pix",
                $"demo-seed:baixa-recebivel:{parcela.Id}");

            var resultado = await baixar.BaixarParcelaDeContaAReceberAsync(comando, ct).ConfigureAwait(false);
            if (resultado.Falha)
                throw new InvalidOperationException($"DemoSeeder: falha ao liquidar recebível '{parcela.Id}': {resultado.Erro.Mensagem}");
        }
    }

    private static async Task<string> ResolverContaBancoPrincipalAsync(IServiceProvider sp, string businessId, CancellationToken ct)
    {
        var contas = sp.GetRequiredService<IContaBancariaCaixaRepository>();
        var lista = await contas.ListarAsync(businessId, ct: ct).ConfigureAwait(false);
        return lista.FirstOrDefault(c => c.Tipo == TipoContaBancariaCaixa.ContaCorrente)?.Id ?? ClassificadorFormaPagamento.ContaCaixaPadraoId;
    }
}
