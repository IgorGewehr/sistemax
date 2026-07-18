using SistemaX.Modules.Financeiro.Application.Projetos;
using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Domain.Comum;
using SistemaX.Modules.Financeiro.Domain.Contabil;
using SistemaX.Modules.Financeiro.Domain.ContasAPagarReceber;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Application.CasosDeUso;

/// <summary>
/// Lançamento MANUAL/nativo — aluguel, água, luz: fatos que não nascem de outro módulo via
/// evento de integração (docs/financeiro-features.md, mapa §3: "Financeiro (nativo) — tudo que
/// não vem de outro módulo"). <paramref name="IdempotencyKey"/> é obrigatória (regra dura R3 do
/// projeto: toda escrita que cria recurso aceita uma chave de idempotência, aqui equivalente ao
/// header X-Idempotency-Key do resto do sistema).
/// </summary>
/// <param name="Corrente">Dimensão "corrente de receita" (P0-1) — quem chama este caso de uso é
/// quem melhor sabe se este lançamento manual pertence a uma das três correntes; <c>null</c>
/// (default) fica não-classificado nesta dimensão, mesmo tratamento de qualquer conta sem sinal
/// claro.</param>
/// <param name="ProjetoId">Dimensão "Projeto" (docs/financeiro/design-analise-por-projeto.md §3.2)
/// — barrada (422, <see cref="Projetos.AnalisePorProjetoGuard"/>) enquanto o toggle do tenant
/// estiver desligado.</param>
public sealed record LancarContaComando(
    string BusinessId,
    string Descricao,
    string CategoriaId,
    DateTimeOffset DataCompetencia,
    Money ValorTotal,
    IReadOnlyCollection<Parcela> Parcelas,
    string IdempotencyKey,
    string? CentroDeCustoId = null,
    string? ContraparteId = null,
    CorrenteDeReceita? Corrente = null,
    string? ProjetoId = null);

public sealed class LancarContaAPagarUseCase(
    IContaAPagarRepository contasAPagar, ILancamentoContabilRepository lancamentos, IConfiguracaoFinanceiraTenantRepository configuracoes)
{
    public async Task<Result<ContaAPagar>> ExecutarAsync(LancarContaComando comando, CancellationToken ct = default)
    {
        var gating = await AnalisePorProjetoGuard.ExigirAtivaSeProjetoIdAsync(comando.BusinessId, comando.ProjetoId, configuracoes, ct).ConfigureAwait(false);
        if (gating.Falha) return Result.Falhar<ContaAPagar>(gating.Erro);

        var origem = new SourceRef("financeiro-nativo", comando.IdempotencyKey);
        var existente = await contasAPagar.BuscarPorOrigemAsync(comando.BusinessId, origem.Chave, ct);
        if (existente is not null) return Result.Ok(existente);

        var contaResultado = ContaAPagar.Criar(
            comando.BusinessId, origem, comando.Descricao, comando.CategoriaId, comando.DataCompetencia,
            comando.ValorTotal, comando.Parcelas, comando.CentroDeCustoId, comando.ContraparteId, comando.Corrente, comando.ProjetoId);
        if (contaResultado.Falha) return contaResultado;

        await contasAPagar.SalvarAsync(contaResultado.Valor, ct);

        var lancamentoResultado = LancamentoContabilFactory.DeContaAPagar(contaResultado.Valor);
        if (lancamentoResultado.Falha) return Result.Falhar<ContaAPagar>(lancamentoResultado.Erro);

        await lancamentos.SalvarAsync(lancamentoResultado.Valor, ct);
        return contaResultado;
    }
}

public sealed class LancarContaAReceberUseCase(
    IContaAReceberRepository contasAReceber, ILancamentoContabilRepository lancamentos, IConfiguracaoFinanceiraTenantRepository configuracoes)
{
    public async Task<Result<ContaAReceber>> ExecutarAsync(LancarContaComando comando, CancellationToken ct = default)
    {
        var gating = await AnalisePorProjetoGuard.ExigirAtivaSeProjetoIdAsync(comando.BusinessId, comando.ProjetoId, configuracoes, ct).ConfigureAwait(false);
        if (gating.Falha) return Result.Falhar<ContaAReceber>(gating.Erro);

        var origem = new SourceRef("financeiro-nativo", comando.IdempotencyKey);
        var existente = await contasAReceber.BuscarPorOrigemAsync(comando.BusinessId, origem.Chave, ct);
        if (existente is not null) return Result.Ok(existente);

        var contaResultado = ContaAReceber.Criar(
            comando.BusinessId, origem, comando.Descricao, comando.CategoriaId, comando.DataCompetencia,
            comando.ValorTotal, comando.Parcelas, comando.CentroDeCustoId, comando.ContraparteId, comando.Corrente,
            projetoId: comando.ProjetoId);
        if (contaResultado.Falha) return contaResultado;

        await contasAReceber.SalvarAsync(contaResultado.Valor, ct);

        var lancamentoResultado = LancamentoContabilFactory.DeContaAReceber(contaResultado.Valor);
        if (lancamentoResultado.Falha) return Result.Falhar<ContaAReceber>(lancamentoResultado.Erro);

        await lancamentos.SalvarAsync(lancamentoResultado.Valor, ct);
        return contaResultado;
    }
}
