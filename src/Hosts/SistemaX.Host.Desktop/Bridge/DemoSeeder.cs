using SistemaX.Modules.Estoque.Application.Ports;
using SistemaX.Modules.Estoque.Domain.Catalogo;
using SistemaX.Modules.Financeiro.Application.CasosDeUso;
using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Domain.Recorrencia;
using SistemaX.SharedKernel;

namespace SistemaX.Host.Desktop.Bridge;

/// <summary>
/// Semente IDEMPOTENTE (só popula se a coleção estiver vazia) para os dois endpoints reais da
/// F1a terem dado de verdade pra mostrar via <c>curl</c>/UI: assinaturas
/// (<c>GET /api/financeiro/receita-recorrente</c>) e produtos (<c>GET /api/estoque/produtos</c>).
/// É o MESMO dado que o demo de console do F0 escrevia no stdout — só que agora populando os
/// repositórios reais do host, no lugar de imprimir texto. TEMPORÁRIO: cai quando a UI ganhar os
/// formulários de cadastro (wizard de primeiro-boot + telas de Estoque/Assinaturas, roadmap F1
/// restante) — até lá, ver essa semente como o "cadastro inicial" de uma loja-demo.
/// </summary>
public static class DemoSeeder
{
    public static async Task SemearAsync(IServiceProvider provider, string businessId, CancellationToken ct = default)
    {
        await using var scope = provider.CreateAsyncScope();
        var sp = scope.ServiceProvider;

        await SemearAssinaturasAsync(sp, businessId, ct).ConfigureAwait(false);
        await SemearProdutosAsync(sp, businessId, ct).ConfigureAwait(false);
    }

    private static async Task SemearAssinaturasAsync(IServiceProvider sp, string businessId, CancellationToken ct)
    {
        var repo = sp.GetRequiredService<IAssinaturaRepository>();
        if ((await repo.ListarAsync(businessId, ct).ConfigureAwait(false)).Count > 0)
        {
            return;
        }

        var criar = sp.GetRequiredService<CriarAssinaturaUseCase>();
        var cancelar = sp.GetRequiredService<CancelarAssinaturaUseCase>();

        async Task NovaAsync(string servicoId, string servicoNome, string cliente, long centavos, int mesesAtras)
            => await criar.ExecutarAsync(new CriarAssinaturaComando(
                businessId, cliente.ToLowerInvariant().Replace(' ', '-'), cliente, servicoId, servicoNome,
                new Money(centavos), FrequenciaRecorrencia.Mensal, 5, DateTimeOffset.UtcNow.AddMonths(-mesesAtras))).ConfigureAwait(false);

        await NovaAsync("servicepro", "ServicePro", "Mercado Sao Joao", 34900, 2).ConfigureAwait(false);
        await NovaAsync("servicepro", "ServicePro", "Padaria Pao Quente", 34900, 3).ConfigureAwait(false);
        await NovaAsync("servicepro", "ServicePro", "Auto Pecas Silva", 34900, 4).ConfigureAwait(false);
        await NovaAsync("gestao-raiz", "Gestao Raiz Fiscal", "Distribuidora Norte", 89000, 0).ConfigureAwait(false);
        await NovaAsync("gestao-raiz", "Gestao Raiz Fiscal", "Posto Bandeira", 120000, 5).ConfigureAwait(false);
        await NovaAsync("brain", "Brain", "Consultoria Abraao", 22000, 3).ConfigureAwait(false);

        async Task ChurnAsync(string servicoId, string servicoNome, string cliente, long centavos, string motivo)
        {
            var resultado = await criar.ExecutarAsync(new CriarAssinaturaComando(
                businessId, cliente.ToLowerInvariant().Replace(' ', '-'), cliente, servicoId, servicoNome,
                new Money(centavos), FrequenciaRecorrencia.Mensal, 5, DateTimeOffset.UtcNow.AddMonths(-6))).ConfigureAwait(false);
            await cancelar.ExecutarAsync(businessId, resultado.Valor.Id, motivo).ConfigureAwait(false);
        }

        await ChurnAsync("servicepro", "ServicePro", "Salao Bella", 34900, "cancelou o plano").ConfigureAwait(false);

        var gerarCobrancas = sp.GetRequiredService<GerarCobrancasAssinaturasUseCase>();
        await gerarCobrancas.ExecutarAsync(businessId, DateTimeOffset.UtcNow).ConfigureAwait(false);
    }

    private static async Task SemearProdutosAsync(IServiceProvider sp, string businessId, CancellationToken ct)
    {
        var repo = sp.GetRequiredService<IProdutoRepository>();
        if ((await repo.ListarAsync(businessId, ct).ConfigureAwait(false)).Count > 0)
        {
            return;
        }

        async Task NovoAsync(string nome, UnidadeDeMedida unidade, long precoCentavos, string categoria)
        {
            var resultado = Produto.Criar(businessId, nome, unidade, precoVenda: new Money(precoCentavos), categoria: categoria);
            if (resultado.Sucesso)
            {
                await repo.SalvarAsync(resultado.Valor, ct).ConfigureAwait(false);
            }
        }

        await NovoAsync("Refrigerante Lata 350ml", UnidadeDeMedida.UN, 550, "Bebidas").ConfigureAwait(false);
        await NovoAsync("Pão Francês", UnidadeDeMedida.KG, 1490, "Padaria").ConfigureAwait(false);
        await NovoAsync("Óleo de Soja 900ml", UnidadeDeMedida.UN, 890, "Mercearia").ConfigureAwait(false);
    }
}
