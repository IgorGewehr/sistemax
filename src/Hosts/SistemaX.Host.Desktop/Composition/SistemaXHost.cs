using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SistemaX.Infrastructure.Local.DependencyInjection;
using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Abstractions.Consultor;
using SistemaX.Modules.Abstractions.Runtime;
using SistemaX.Modules.Compras.Application;
using SistemaX.Modules.Compras.Application.Endpoints;
using SistemaX.Modules.Compras.Infrastructure;
using SistemaX.Modules.Estoque.Application;
using SistemaX.Modules.Estoque.Application.Endpoints;
using SistemaX.Modules.Estoque.Infrastructure;
using SistemaX.Modules.Financeiro.Application;
using SistemaX.Modules.Financeiro.Application.Endpoints;
using SistemaX.Modules.Financeiro.Infrastructure;
using SistemaX.Modules.Financeiro.Infrastructure.Cron;
using SistemaX.Modules.Fiscal.Application;
using SistemaX.Modules.Fiscal.Application.Endpoints;
using SistemaX.Modules.Fiscal.Infrastructure;
using SistemaX.Modules.Fiscal.Infrastructure.Cron;
using SistemaX.Modules.Identidade.Application;
using SistemaX.Modules.Identidade.Application.Endpoints;
using SistemaX.Modules.Identidade.Infrastructure;
using SistemaX.Modules.Vendas.Application;
using SistemaX.Modules.Vendas.Application.Endpoints;
using SistemaX.Modules.Vendas.Infrastructure;
using SistemaX.Verticals.Assistencia.Application;
using SistemaX.Verticals.Assistencia.Application.Endpoints;
using SistemaX.Verticals.Assistencia.Infrastructure;

namespace SistemaX.Host.Desktop.Composition;

/// <summary>
/// COMPOSITION ROOT. É aqui — e SÓ aqui — que se decide "quais módulos esta instalação tem".
/// O Core nunca sabe. Monta o catálogo via <see cref="ModuleRegistry"/> (ordem topológica por
/// dependência) e liga o barramento de eventos de integração + a infraestrutura local
/// (SQLite/UoW/outbox/backup/crash-recovery — ver <c>AddSistemaXLocalInfrastructure</c>).
///
/// Para habilitar um vertical (ex.: Assistência, Posto), basta um <c>.Adicionar(new XModule())</c>
/// aqui. Nenhum outro arquivo do sistema muda — é a regra de ouro da arquitetura na prática.
///
/// MUDANÇA DA F1a vs. F0: <see cref="RegistrarModulos"/> registra DENTRO de um
/// <see cref="IServiceCollection"/> que já existe (o do <c>WebApplicationBuilder</c> do Kestrel
/// embutido) em vez de criar seu PRÓPRIO <c>ServiceProvider</c> isolado — o F0 fazia isso porque
/// era só um demo de console; o app real precisa que o ASP.NE Core seja DONO do container (é
/// dele que Kestrel/roteamento/DI de request dependem). Devolve o <see cref="ModuleRegistry"/>
/// montado para o Host enumerar <c>IModuleEndpoints</c> depois de <c>builder.Build()</c> — ver
/// <c>Program.cs</c>.
///
/// COBERTURA DE ENDPOINTS (achado de auditoria, guard-rail pra não reabrir) — dos 11 módulos do
/// RBAC (<c>Autorizacao.Modulo</c>: Dashboard, Vendas, Pdv, Financeiro, Estoque, Compras, Ordens,
/// Clientes, Agenda, Fiscal, Configuracoes), Vendas/Estoque/Financeiro/Identidade (rota
/// <c>/usuarios/*</c>, sob <c>Modulo.Configuracoes:Acao.GerenciarUsuarios</c> — ver ADR-0003)
/// TINHAM um <c>*EndpointsModule</c> (implementa <see cref="IModuleEndpoints"/>) e portanto rota
/// HTTP protegida por <c>.RequerPermissao(...)</c> (ver <c>PermissaoEndpointExtensions</c>).
/// SEGUNDA RODADA de auditoria (docs/arquitetura — exposição HTTP dos módulos "prontos-mas-não-
/// expostos"): Compras (fornecedores + notas de compra) e Fiscal (documentos + CC-e +
/// configuração/CSC) já tinham módulo de domínio aqui embaixo SEM endpoint — agora têm
/// <see cref="ComprasEndpointsModule"/>/<see cref="FiscalEndpointsModule"/>. O vertical Assistência
/// (Ordem de Serviço, RBAC <c>Modulo.Ordens</c>) também tinha domínio+casos de uso completos sem
/// nenhuma rota — agora tem <see cref="AssistenciaEndpointsModule"/>. Pdv, Dashboard e o resto de
/// Configuracoes continuam sem módulo de domínio (não existem ainda — não confundir com "sem
/// endpoint"). <c>Modulo.Clientes</c>/<c>Modulo.Agenda</c> do RBAC NÃO têm módulo de domínio nem
/// no saas-erp-irmão portado pra cá: o front (<c>web/src/pages/Clientes.tsx</c>/<c>Agenda.tsx</c>)
/// roda inteiramente sobre mock (<c>web/src/mocks/</c>) — reportado, não inventado; portar esse
/// domínio é trabalho de FUTURA rodada, fora do escopo desta auditoria. QUANDO qualquer módulo
/// ganhar sua primeira rota HTTP, o `*EndpointsModule` correspondente TEM que nascer com
/// `.RequerPermissao(Modulo.X, Acao.Y)` na mesma volta de PR — senão reabre exatamente o buraco
/// "qualquer sessão Bearer válida chama qualquer endpoint" que motivou este arquivo (ver
/// <c>Permissoes.cs</c> §1).
/// </summary>
public static class SistemaXHost
{
    public static ModuleRegistry RegistrarModulos(
        IServiceCollection services,
        CamadaExecucao camada,
        IConfiguration configuracao,
        string businessId)
    {
        var contexto = new ModuleContext(camada, configuracao);

        var registry = new ModuleRegistry()
            .Adicionar(new FinanceiroModule())                // o coração
            .Adicionar(new FinanceiroInfrastructureModule())   // adapters (in-memory por padrão)
            .Adicionar(new FinanceiroEndpointsModule())        // /api/financeiro/*
            .Adicionar(new VendasModule())                     // PDV / frente de caixa
            .Adicionar(new VendasInfrastructureModule())
            .Adicionar(new VendasEndpointsModule())            // /api/vendas/*
            .Adicionar(new EstoqueModule())                    // produtos + saldos + razão de estoque
            .Adicionar(new EstoqueInfrastructureModule())
            .Adicionar(new EstoqueEndpointsModule())           // /api/estoque/*
            .Adicionar(new ComprasModule())                    // fornecedores + notas de compra
            .Adicionar(new ComprasInfrastructureModule())
            .Adicionar(new ComprasEndpointsModule())           // /api/compras/*
            .Adicionar(new FiscalModule())                     // NF-e/NFC-e/NFS-e/MDF-e — core tributário
            .Adicionar(new FiscalInfrastructureModule())
            .Adicionar(new FiscalEndpointsModule())            // /api/fiscal/*
            .Adicionar(new IdentidadeModule())                  // usuários reais, papel de verdade (ADR-0003)
            .Adicionar(new IdentidadeInfrastructureModule())
            .Adicionar(new IdentidadeEndpointsModule())         // /api/usuarios/*
            .Adicionar(new AssistenciaModule())                 // vertical: assistência técnica (Ordem de Serviço)
            .Adicionar(new AssistenciaInfrastructureModule())
            .Adicionar(new AssistenciaEndpointsModule());       // /api/assistencia/*

        registry.RegistrarTodos(services, contexto);

        services.AddSingleton<IIntegrationEventBus, InProcessIntegrationEventBus>();

        // Super Consultor (Fase 2 do plano de inteligência do Financeiro — ADR-0005 §3.5):
        // orquestrador MODULE-AGNOSTIC — não pertence a nenhum <see cref="IModule"/> específico,
        // por isso vive aqui no composition root. Coleta de TODO <c>IConsultorFactProvider</c>
        // registrado acima (hoje só o do Financeiro; Estoque/Vendas/Compras/Fiscal se plugam
        // depois só adicionando outro `services.AddScoped&lt;IConsultorFactProvider, X&gt;()` no
        // módulo deles — zero mudança aqui). Narrador registrado nesta rodada é o
        // <see cref="NarradorTemplate"/> (determinístico, custo zero, de propósito — ver tarefa do
        // Super Consultor): um <c>NarradorLLM</c> futuro troca de lugar aqui via DI, sem tocar em
        // <see cref="ConsultorService"/>, no ranking ou na UI. O cache precisa ser singleton — é
        // um dicionário em memória que precisa sobreviver entre requisições.
        services.AddSingleton<IConsultorInsightCache, InMemoryConsultorInsightCache>();
        services.AddScoped<IConsultorNarrador, NarradorTemplate>();
        services.AddScoped<ConsultorService>();

        // Infraestrutura local (SQLite + UoW/sessão + outbox + backup + migrations +
        // crash-recovery) — ligada por padrão para TODO host que passar por este composition
        // root. Um único arquivo por instalação (ver ARCHITECTURE.md §2.4 e
        // docs/persistencia/persistencia-sqlite.md). Registrada como IHostedService: o Generic
        // Host chama BootstrapAsync automaticamente dentro de `app.StartAsync()` — F0 chamava
        // manualmente porque não tinha Generic Host ainda (só um ServiceProvider solto).
        services.AddSistemaXLocalInfrastructure(o =>
        {
            var dataDir = Path.Combine(AppContext.BaseDirectory, "data");
            o.DatabasePath = Path.Combine(dataDir, "sistemax.db");
            o.BackupDirectory = Path.Combine(dataDir, "backups");
        });

        // Autonomia do motor financeiro/fiscal (crons sem gatilho manual) — os dois jobs de
        // background que dependem de saber "qual tenant processar" sem HttpContext nenhum. Registro
        // AQUI (composition root), nunca dentro de FinanceiroInfrastructureModule/FiscalInfrastructureModule:
        // mesmo racional de ProjectionCatchUpHostedService (AddSistemaXLocalInfrastructure, acima)
        // — fixtures de teste que montam um ModuleRegistry parcial (ex.: PermissaoHttpFixture) não
        // devem herdar um BackgroundService que precisa de ITenantsDeInstalacao, que elas não
        // registram.
        services.AddSingleton<ITenantsDeInstalacao>(new TenantsDeInstalacaoFixo(businessId));

        services.AddOptions<FinanceiroCronOptions>();
        services.AddHostedService<AvaliarParcelasVencidasBackgroundService>();

        // P0-3 (docs/financeiro/revisao-domain-fit-cnpj.md) — sem este job, nenhuma assinatura
        // real virava ContaAReceber/receita sozinha (só o DemoSeeder chamava o gerador).
        services.AddHostedService<FaturarAssinaturasBackgroundService>();

        services.AddOptions<FiscalCronOptions>();
        services.AddHostedService<RetransmissaoFiscalBackgroundService>();

        // Exposto pra Program.cs enumerar `OfType<IModuleEndpoints>()` depois do builder.Build().
        // Registramos a INSTÂNCIA (não o tipo) de propósito: precisamos dos MESMOS objetos IModule
        // que entraram no grafo de dependência acima, não uma segunda instância resolvida por DI.
        services.AddSingleton(registry);

        return registry;
    }
}
