using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Application.Configuracao;

/// <summary>
/// O CONTRATO do opt-in de Imobilizado/ROI (docs/financeiro/design-imobilizado-roi.md §2.2) — o
/// SEGUNDO toggle de <c>ConfiguracaoFinanceiraTenant</c>, independente de
/// <c>Projetos.AnalisePorProjetoGuard</c> (que continua sendo o lar do toggle
/// <c>AnalisePorProjetoAtiva</c>): o design pede "um único helper para as DUAS features" — aqui a
/// forma escolhida é DOIS helpers igualmente enxutos, um por toggle, em vez de fundir os dois num
/// só (que acopraria dois recursos com ciclos de vida de opt-in deliberadamente independentes —
/// §2.1: "o dono pode querer Imobilizado+ROI no dia zero e a Análise por Projeto só meses
/// depois"). Ambos seguem o MESMO contrato: leitura de <c>IConfiguracaoFinanceiraTenantRepository</c>,
/// ausência de linha = desligado, 422 documentado na escrita.
///
/// Exceção deliberada (§2.2): bens <c>AtivoDeCapital</c> criados pela Análise por Projeto (rota
/// <c>POST /financeiro/ativos</c>, gate <c>AnalisePorProjetoGuard</c>) são válidos mesmo com este
/// toggle desligado — o agregado é COMPARTILHADO; cada toggle governa a SUA rota
/// (<c>POST /financeiro/imobilizado</c> aqui), nunca a existência do dado.
/// </summary>
public static class FinanceiroOptInGuard
{
    public static readonly Error Desativado = new(
        "financeiro.imobilizado.desativado",
        "Imobilizado/ROI está desativado para este tenant — ative em Configurações antes de cadastrar bens/aportes.");

    public static async Task<Result> ExigirImobilizadoRoiAsync(
        string businessId, IConfiguracaoFinanceiraTenantRepository configuracoes, CancellationToken ct = default)
    {
        var configuracao = await configuracoes.ObterAsync(businessId, ct).ConfigureAwait(false);
        var ativo = configuracao?.ImobilizadoRoiAtivo ?? false;
        return ativo ? Result.Ok() : Result.Falhar(Desativado);
    }
}
