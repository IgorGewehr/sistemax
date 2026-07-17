using Microsoft.Extensions.DependencyInjection;
using SistemaX.Infrastructure.Local.DependencyInjection;
using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Abstractions.Runtime;
using SistemaX.Modules.Financeiro.Application.Analitico;
using SistemaX.Modules.Financeiro.Application.Caixa;
using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Infrastructure.InMemory;
using SistemaX.Modules.Financeiro.Infrastructure.Relogio;
using SistemaX.Modules.Financeiro.Infrastructure.Sqlite;

namespace SistemaX.Modules.Financeiro.Infrastructure;

/// <summary>
/// Segundo <see cref="IModule"/> do Financeiro — registra os ADAPTERS concretos dos ports
/// declarados em <c>SistemaX.Modules.Financeiro.Application.Ports</c>. Vive num módulo separado
/// de <c>FinanceiroModule</c> (Application) porque o grafo de referência de projeto da solução é
/// <c>Infrastructure → Application → Domain</c> — só a Infrastructure enxerga tanto os ports
/// quanto os adapters concretos ao mesmo tempo.
///
/// <see cref="DependeDe"/> aponta para <c>"financeiro"</c> (o módulo de Application) — o Host
/// deve registrar os dois; a ordem entre eles não importa para <c>IServiceCollection</c> (DI só
/// resolve na primeira requisição), mas a dependência documenta a intenção para quem futuramente
/// escrever o discovery de módulos no Host.
///
/// HOJE: adapter in-memory (<c>ConcurrentDictionary</c>), suficiente para rodar o módulo e os
/// testes sem infraestrutura externa. EXTENSÍVEL PARA SQLITE: <c>SistemaX.Infrastructure.Local</c>
/// (referenciado por este projeto) já carrega <c>Microsoft.Data.Sqlite</c> — trocar cada
/// <c>InMemoryXxxRepository</c> por um <c>SqliteXxxRepository</c> que implementa o MESMO port não
/// exige nenhuma mudança em Domain/Application.
/// </summary>
public sealed class FinanceiroInfrastructureModule : IModule
{
    public string Codigo => "financeiro.infra";
    public string Nome => "Financeiro — Infraestrutura";
    public IReadOnlyCollection<string> DependeDe => ["financeiro"];

    public void Registrar(IServiceCollection services, IModuleContext contexto)
    {
        if (contexto.Configuracao["persistencia"] == "sqlite")
        {
            services.AddScoped<IContaAReceberRepository, SqliteContaAReceberRepository>();
            services.AddModuleSchemaMigration<FinanceiroSchemaMigrationV1>();

            services.AddScoped<IContaAPagarRepository, SqliteContaAPagarRepository>();
            services.AddModuleSchemaMigration<FinanceiroSchemaMigrationV2>();

            services.AddScoped<IMovimentoFinanceiroRepository, SqliteMovimentoFinanceiroRepository>();
            services.AddModuleSchemaMigration<FinanceiroSchemaMigrationV3>();

            services.AddScoped<ILancamentoContabilRepository, SqliteLancamentoContabilRepository>();
            services.AddModuleSchemaMigration<FinanceiroSchemaMigrationV4>();

            services.AddScoped<IConciliacaoRepository, SqliteConciliacaoRepository>();
            services.AddScoped<IExtratoBancarioItemRepository, SqliteExtratoBancarioItemRepository>();
            services.AddModuleSchemaMigration<FinanceiroSchemaMigrationV5>();
            services.AddModuleSchemaMigration<FinanceiroSchemaMigrationV6>();

            services.AddScoped<IRecorrenciaRepository, SqliteRecorrenciaRepository>();
            services.AddModuleSchemaMigration<FinanceiroSchemaMigrationV7>();

            // F0 do plano de inteligência do Financeiro — as fact tables de PROVA
            // (docs/financeiro/inteligencia-arquitetura.md/ADR-0005).
            services.AddScoped<IFatoReceitaDiariaRepository, SqliteFatoReceitaDiariaRepository>();
            services.AddScoped<IFatoCaixaDiarioRepository, SqliteFatoCaixaDiarioRepository>();
            services.AddModuleSchemaMigration<FinanceiroSchemaMigrationV8>();

            // fato_custo_diario — fecha o gap do CMV (CustoBaixadoPorVenda) que persistia no
            // ledger sem nenhum fold reagir a ele.
            services.AddScoped<IFatoCustoDiarioRepository, SqliteFatoCustoDiarioRepository>();
            services.AddModuleSchemaMigration<FinanceiroSchemaMigrationV9>();

            // F1 — fato_margem_produto: motor base (receita/CMV/MC por produto).
            services.AddScoped<IFatoMargemProdutoRepository, SqliteFatoMargemProdutoRepository>();
            services.AddModuleSchemaMigration<FinanceiroSchemaMigrationV10>();

            // Frente 3 da autonomia do motor financeiro — fato_recebiveis (líquido + MDR + lag D+N).
            services.AddScoped<IFatoRecebiveisRepository, SqliteFatoRecebiveisRepository>();
            services.AddModuleSchemaMigration<FinanceiroSchemaMigrationV11>();

            // Bancário (docs/wiring/financeiro-telas-restantes.md §3) — ContaBancariaCaixa e
            // FormaDePagamento deixam de ser conceitos hardcoded/sem persistência: viram o LAR
            // ÚNICO de conta/caixa e MDR/lag, respectivamente. fato_recebiveis passa a consultar
            // IFormaDePagamentoRepository (ver FatoRecebiveisProjection) em vez da antiga
            // ConfiguracaoDeRecebiveisOptions (removida).
            services.AddScoped<IContaBancariaCaixaRepository, SqliteContaBancariaCaixaRepository>();
            services.AddModuleSchemaMigration<FinanceiroSchemaMigrationV12>();

            services.AddScoped<IFormaDePagamentoRepository, SqliteFormaDePagamentoRepository>();
            services.AddModuleSchemaMigration<FinanceiroSchemaMigrationV13>();

            // Fluxo de Caixa (docs/wiring/financeiro-telas-restantes.md §4) — o ritual de caixa
            // físico (SessaoCaixa), domínio novo que não existia até esta reconciliação.
            services.AddScoped<ISessaoCaixaRepository, SqliteSessaoCaixaRepository>();
            services.AddModuleSchemaMigration<FinanceiroSchemaMigrationV14>();

            // Assinaturas (P0-3, docs/financeiro/revisao-domain-fit-cnpj.md) — antes deste fix,
            // este bloco NUNCA registrava o adapter Sqlite: IAssinaturaRepository ficava sempre
            // in-memory mesmo com persistencia == "sqlite" (registro incondicional logo abaixo,
            // fora deste bloco), e o cron de faturamento perdia todas as assinaturas a cada
            // restart do host — o motor de ciclo/idempotência (Assinatura.GerarCobranca) ficava
            // correto mas sem nada pra iterar depois de reiniciar.
            services.AddScoped<IAssinaturaRepository, SqliteAssinaturaRepository>();
            services.AddModuleSchemaMigration<FinanceiroSchemaMigrationV15>();

            // Dimensão "corrente de receita" (P0-1, docs/financeiro/revisao-domain-fit-cnpj.md) —
            // V16-18 acrescentam a coluna corrente (+ backfill retrocompatível) às entidades que
            // carregam receita/custo direto; V19-20 reconstroem as fact tables diárias com a
            // corrente entrando na chave e forçam replay completo via reset do cursor de projeção.
            services.AddModuleSchemaMigration<FinanceiroSchemaMigrationV16>();
            services.AddModuleSchemaMigration<FinanceiroSchemaMigrationV17>();
            services.AddModuleSchemaMigration<FinanceiroSchemaMigrationV18>();
            services.AddModuleSchemaMigration<FinanceiroSchemaMigrationV19>();
            services.AddModuleSchemaMigration<FinanceiroSchemaMigrationV20>();

            // P1-7 (docs/financeiro/revisao-domain-fit-cnpj.md) — ContaAReceber ganha TecnicoId
            // (consulta "qual técnico faturou") e ValorServico/ValorPecas (repartição de relatório).
            services.AddModuleSchemaMigration<FinanceiroSchemaMigrationV21>();

            // P0-4 — mapeamento corrente→anexo do Radar do Simples, configurável por tenant.
            services.AddScoped<IConfiguracaoRadarSimplesRepository, SqliteConfiguracaoRadarSimplesRepository>();
            services.AddModuleSchemaMigration<FinanceiroSchemaMigrationV22>();
        }
        else
        {
            services.AddSingleton<IContaAReceberRepository, InMemoryContaAReceberRepository>();
            services.AddSingleton<IContaAPagarRepository, InMemoryContaAPagarRepository>();
            services.AddSingleton<IMovimentoFinanceiroRepository, InMemoryMovimentoFinanceiroRepository>();
            services.AddSingleton<ILancamentoContabilRepository, InMemoryLancamentoContabilRepository>();
            services.AddSingleton<IConciliacaoRepository, InMemoryConciliacaoRepository>();
            services.AddSingleton<IExtratoBancarioItemRepository, InMemoryExtratoBancarioItemRepository>();
            services.AddSingleton<IRecorrenciaRepository, InMemoryRecorrenciaRepository>();

            services.AddSingleton<IFatoReceitaDiariaRepository, InMemoryFatoReceitaDiariaRepository>();
            services.AddSingleton<IFatoCaixaDiarioRepository, InMemoryFatoCaixaDiarioRepository>();
            services.AddSingleton<IFatoCustoDiarioRepository, InMemoryFatoCustoDiarioRepository>();
            services.AddSingleton<IFatoMargemProdutoRepository, InMemoryFatoMargemProdutoRepository>();
            services.AddSingleton<IFatoRecebiveisRepository, InMemoryFatoRecebiveisRepository>();

            services.AddSingleton<IContaBancariaCaixaRepository, InMemoryContaBancariaCaixaRepository>();
            services.AddSingleton<IFormaDePagamentoRepository, InMemoryFormaDePagamentoRepository>();
            services.AddSingleton<ISessaoCaixaRepository, InMemorySessaoCaixaRepository>();
            services.AddSingleton<IAssinaturaRepository, InMemoryAssinaturaRepository>();
            services.AddSingleton<IConfiguracaoRadarSimplesRepository, InMemoryConfiguracaoRadarSimplesRepository>();
        }

        // Projeções (IProjection) — Scoped em ambos os modos: o repo por trás pode ser Scoped
        // (sqlite, participa de ILocalSessao) ou Singleton (in-memory); Scoped funciona sobre
        // qualquer um dos dois sem virar captive dependency (ver ProjectionRunner.ExecutarTudoAsync,
        // que resolve IProjection dentro do seu próprio escopo, nunca no construtor do Singleton).
        services.AddScoped<IProjection, FatoReceitaDiariaProjection>();
        services.AddScoped<IProjection, FatoCaixaDiarioProjection>();
        services.AddScoped<IProjection, FatoCustoDiarioProjection>();
        services.AddScoped<IProjection, FatoMargemProdutoProjection>();
        services.AddScoped<IProjection, FatoRecebiveisProjection>();

        // MDR/lag de liquidação por forma de pagamento (frente 3): FatoRecebiveisProjection resolve
        // via IFormaDePagamentoRepository (registrado acima, sqlite ou in-memory conforme o modo) —
        // a antiga ConfiguracaoDeRecebiveisOptions (config estática de app, paralela ao domínio) foi
        // removida nesta reconciliação. Os mesmos números de mercado que ela hardcodava agora nascem
        // como dado real via FinanceiroBootstrapSeeder.SemearAsync (idempotente, roda todo boot).

        services.AddSingleton<IRelogio, RelogioSistema>();
    }
}
