using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Domain.Configuracao;

/// <summary>
/// O toggle opt-in absoluto da Análise por Projeto (docs/financeiro/design-analise-por-projeto.md
/// §2.1) — espelho exato de <c>SistemaX.Modules.Fiscal.Domain.Regimes.ConfiguracaoFiscalTenant</c>:
/// um <c>record</c> de configuração por tenant, uma linha por tenant, port + repositório
/// SQLite/InMemory com contract tests 2×. AUSÊNCIA DE LINHA = tudo desligado — zero seed
/// necessário, tenant novo nasce exatamente como hoje (<see cref="Padrao"/> é o fallback do
/// leitor, nunca gravado automaticamente).
///
/// <see cref="AnalisePorProjetoAtiva"/> é o único campo que a Parte A (fundação + painel v1) lê —
/// <see cref="CustoHoraPadraoCentavos"/>/<see cref="TempoEntraNoDre"/> existem desde já (mesmo
/// shape do design, para não exigir outra migração ALTER quando a Parte B — apontamento de tempo —
/// chegar) mas nenhum código ainda os consome.
///
/// <see cref="ImobilizadoRoiAtivo"/>/<see cref="TaxaDescontoAnualBps"/>/<see cref="InicioOperacao"/>
/// (docs/financeiro/design-imobilizado-roi.md §2.1) — o SEGUNDO toggle independente deste record:
/// o dono pode querer Imobilizado+ROI no dia zero e a Análise por Projeto só meses depois (ou
/// vice-versa). <see cref="TaxaDescontoAnualBps"/> em bps (1200 = 12% a.a.); <c>null</c> = payback
/// descontado omitido — NUNCA um default silencioso inventado pelo sistema (é input do quant,
/// nunca palpite do ERP). <see cref="InicioOperacao"/> é o override do marco <c>m0</c> do ROI —
/// <c>null</c> (o comum) deriva <c>m0</c> do 1º fato de investimento (§7.2); só é necessário para
/// tenant legado que ligar o toggle tarde.
/// </summary>
public sealed record ConfiguracaoFinanceiraTenant(
    string TenantId,
    bool AnalisePorProjetoAtiva,
    long? CustoHoraPadraoCentavos,
    bool TempoEntraNoDre,
    bool ImobilizadoRoiAtivo = false,
    int? TaxaDescontoAnualBps = null,
    DateOnly? InicioOperacao = null)
{
    public static ConfiguracaoFinanceiraTenant Padrao(string tenantId) => new(tenantId, false, null, false, false, null, null);

    public static Result<ConfiguracaoFinanceiraTenant> Criar(
        string tenantId, bool analisePorProjetoAtiva = false, long? custoHoraPadraoCentavos = null, bool tempoEntraNoDre = false,
        bool imobilizadoRoiAtivo = false, int? taxaDescontoAnualBps = null, DateOnly? inicioOperacao = null)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            return Result.Falhar<ConfiguracaoFinanceiraTenant>(new Error("financeiro.configuracao_tenant.tenant_obrigatorio", "TenantId é obrigatório."));

        if (custoHoraPadraoCentavos is < 0)
            return Result.Falhar<ConfiguracaoFinanceiraTenant>(new Error("financeiro.configuracao_tenant.custo_hora_invalido", "Custo/hora padrão não pode ser negativo."));

        if (taxaDescontoAnualBps is < 0)
            return Result.Falhar<ConfiguracaoFinanceiraTenant>(new Error("financeiro.configuracao_tenant.taxa_desconto_invalida", "Taxa de desconto anual não pode ser negativa."));

        return Result.Ok(new ConfiguracaoFinanceiraTenant(
            tenantId, analisePorProjetoAtiva, custoHoraPadraoCentavos, tempoEntraNoDre, imobilizadoRoiAtivo, taxaDescontoAnualBps, inicioOperacao));
    }
}
