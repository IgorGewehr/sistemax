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
/// </summary>
public sealed record ConfiguracaoFinanceiraTenant(
    string TenantId,
    bool AnalisePorProjetoAtiva,
    long? CustoHoraPadraoCentavos,
    bool TempoEntraNoDre)
{
    public static ConfiguracaoFinanceiraTenant Padrao(string tenantId) => new(tenantId, false, null, false);

    public static Result<ConfiguracaoFinanceiraTenant> Criar(
        string tenantId, bool analisePorProjetoAtiva = false, long? custoHoraPadraoCentavos = null, bool tempoEntraNoDre = false)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            return Result.Falhar<ConfiguracaoFinanceiraTenant>(new Error("financeiro.configuracao_tenant.tenant_obrigatorio", "TenantId é obrigatório."));

        if (custoHoraPadraoCentavos is < 0)
            return Result.Falhar<ConfiguracaoFinanceiraTenant>(new Error("financeiro.configuracao_tenant.custo_hora_invalido", "Custo/hora padrão não pode ser negativo."));

        return Result.Ok(new ConfiguracaoFinanceiraTenant(tenantId, analisePorProjetoAtiva, custoHoraPadraoCentavos, tempoEntraNoDre));
    }
}
