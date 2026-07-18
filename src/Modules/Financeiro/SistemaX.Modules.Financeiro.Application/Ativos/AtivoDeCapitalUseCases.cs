using SistemaX.Modules.Financeiro.Application.Configuracao;
using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Application.Projetos;
using SistemaX.Modules.Financeiro.Domain.Ativos;
using SistemaX.Modules.Financeiro.Domain.Comum;
using SistemaX.Modules.Financeiro.Domain.Contabil;
using SistemaX.Modules.Financeiro.Domain.ContasAPagarReceber;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Application.Ativos;

/// <summary>DTO de fio de <see cref="AtivoDeCapital"/> — nunca o agregado direto.</summary>
public sealed record AtivoDeCapitalDto(
    string Id, string? ProjetoId, string Nome, string Natureza, string Categoria,
    long CustoAquisicaoCentavos, long ValorResidualCentavos, DateOnly DataAquisicao, DateOnly InicioDepreciacao,
    int VidaUtilMeses, int QuantidadeUnidades, string? ContaAPagarId, string Status,
    DateTimeOffset? UltimaCompetenciaReconhecida, DateTimeOffset? EncerradoEm, DateTimeOffset? BaixadoEm,
    string? MotivoBaixa, long ValorContabilAtualCentavos, long AmortizacaoMensalCentavos)
{
    public static AtivoDeCapitalDto DeDominio(AtivoDeCapital a) => new(
        a.Id, a.ProjetoId, a.Nome, a.Natureza.ToString(), a.Categoria.ToString(),
        a.CustoAquisicao.Centavos, a.ValorResidual.Centavos, a.DataAquisicao, a.InicioDepreciacao,
        a.VidaUtilMeses, a.QuantidadeUnidades, a.ContaAPagarId, a.Status.ToString(),
        a.UltimaCompetenciaReconhecida, a.EncerradoEm, a.BaixadoEm, a.MotivoBaixa,
        AtivoDeCapitalQuant.ValorContabilAtualCentavos(a), AtivoDeCapitalQuant.ValorNaCompetencia(a, a.ProximaCompetenciaDevida));
}

public sealed record ParcelaInvestimento(DateTimeOffset Vencimento, long ValorCentavos);

/// <summary>"Registrar investimento" (design-pai §3.3/§8.3) — cria o <c>AtivoDeCapital</c> e,
/// opcionalmente NO MESMO gesto, a <c>ContaAPagar</c> parcelada do caixa (categoria
/// <c>ativo-de-capital</c>, mesmo <c>ProjetoId</c>). <see cref="Parcelas"/> e
/// <see cref="ContaAPagarId"/> são mutuamente exclusivos com "nenhum dos dois" (investimento pago
/// fora do sistema — só a competência entra no cronograma).</summary>
public sealed record CriarAtivoDeCapitalComando(
    string BusinessId, string Nome, NaturezaAtivo Natureza, CategoriaAtivo Categoria,
    long CustoAquisicaoCentavos, DateOnly DataAquisicao, int VidaUtilMeses,
    long ValorResidualCentavos = 0, DateOnly? InicioDepreciacao = null, int QuantidadeUnidades = 1,
    string? ProjetoId = null, IReadOnlyCollection<ParcelaInvestimento>? Parcelas = null, string? ContaAPagarId = null);

public sealed class CriarAtivoDeCapitalUseCase(
    IAtivoDeCapitalRepository ativos, IContaAPagarRepository contasAPagar, ILancamentoContabilRepository lancamentos,
    IConfiguracaoFinanceiraTenantRepository configuracoes, IRelogio relogio)
{
    /// <summary>Alias da Análise por Projeto (design-pai §8.3) — <c>POST /financeiro/ativos</c>,
    /// gate <c>AnalisePorProjetoGuard</c>.</summary>
    public async Task<Result<AtivoDeCapital>> ExecutarAsync(CriarAtivoDeCapitalComando comando, CancellationToken ct = default)
    {
        var gating = await AnalisePorProjetoGuard.ExigirAtivaAsync(comando.BusinessId, configuracoes, ct).ConfigureAwait(false);
        if (gating.Falha) return Result.Falhar<AtivoDeCapital>(gating.Erro);

        return await ExecutarCoreAsync(comando, ct).ConfigureAwait(false);
    }

    /// <summary>Alias do Imobilizado (docs/financeiro/design-imobilizado-roi.md §2.2/§8.1) —
    /// <c>POST /financeiro/imobilizado</c>, gate <c>FinanceiroOptInGuard.ExigirImobilizadoRoiAsync</c>
    /// (o SEGUNDO toggle, independente). "Um handler só, dois gates" (§8.1): a criação em si (Domain
    /// + ContaAPagar opcional + lançamento contábil) é IDÊNTICA — <see cref="ExecutarCoreAsync"/> —
    /// só o portão de entrada muda.</summary>
    public async Task<Result<AtivoDeCapital>> ExecutarImobilizadoAsync(CriarAtivoDeCapitalComando comando, CancellationToken ct = default)
    {
        var gating = await FinanceiroOptInGuard.ExigirImobilizadoRoiAsync(comando.BusinessId, configuracoes, ct).ConfigureAwait(false);
        if (gating.Falha) return Result.Falhar<AtivoDeCapital>(gating.Erro);

        return await ExecutarCoreAsync(comando, ct).ConfigureAwait(false);
    }

    private async Task<Result<AtivoDeCapital>> ExecutarCoreAsync(CriarAtivoDeCapitalComando comando, CancellationToken ct)
    {
        var agora = relogio.Agora();
        var inicioDepreciacao = comando.InicioDepreciacao ?? comando.DataAquisicao;

        var criado = AtivoDeCapital.Criar(
            comando.BusinessId, comando.Nome, comando.Natureza, comando.Categoria,
            new Money(comando.CustoAquisicaoCentavos), new Money(comando.ValorResidualCentavos),
            comando.DataAquisicao, inicioDepreciacao, comando.VidaUtilMeses, agora, comando.QuantidadeUnidades,
            comando.ProjetoId, comando.ContaAPagarId);
        if (criado.Falha) return criado;

        var ativo = criado.Valor;

        if (comando.ContaAPagarId is null && comando.Parcelas is { Count: > 0 } parcelasInvestimento)
        {
            var contaResultado = await CriarContaAPagarDoInvestimentoAsync(ativo, parcelasInvestimento, agora, ct).ConfigureAwait(false);
            if (contaResultado.Falha) return Result.Falhar<AtivoDeCapital>(contaResultado.Erro);

            ativo.VincularContaAPagar(contaResultado.Valor.Id);
        }

        await ativos.SalvarAsync(ativo, ct).ConfigureAwait(false);
        return Result.Ok(ativo);
    }

    private async Task<Result<ContaAPagar>> CriarContaAPagarDoInvestimentoAsync(
        AtivoDeCapital ativo, IReadOnlyCollection<ParcelaInvestimento> parcelasInvestimento, DateTimeOffset agora, CancellationToken ct)
    {
        var origem = new SourceRef("financeiro-ativo", ativo.Id);
        var parcelas = parcelasInvestimento
            .Select((p, indice) => Parcela.Criar(indice + 1, p.Vencimento, new Money(p.ValorCentavos)))
            .ToList();

        var contaResultado = ContaAPagar.Criar(
            ativo.BusinessId, origem, $"Investimento — {ativo.Nome}", LancamentoContabilFactory.CategoriaAtivoDeCapital,
            agora, ativo.CustoAquisicao, parcelas, projetoId: ativo.ProjetoId);
        if (contaResultado.Falha) return contaResultado;

        await contasAPagar.SalvarAsync(contaResultado.Valor, ct).ConfigureAwait(false);

        var lancamentoResultado = LancamentoContabilFactory.DeContaAPagar(contaResultado.Valor);
        if (lancamentoResultado.Sucesso)
        {
            await lancamentos.SalvarAsync(lancamentoResultado.Valor, ct).ConfigureAwait(false);
        }

        return contaResultado;
    }
}

public sealed record BaixarAtivoDeCapitalComando(string BusinessId, string AtivoId, string Motivo, DateOnly Competencia);

/// <summary>Baixa antecipada/write-off (§4.5/§4.6 dos dois designs) — reconhece de uma vez o valor
/// contábil restante (residual incluso) e transiciona a FSM.</summary>
public sealed class BaixarAtivoDeCapitalUseCase(IAtivoDeCapitalRepository ativos, IRelogio relogio)
{
    public async Task<Result<AtivoDeCapital>> ExecutarAsync(BaixarAtivoDeCapitalComando comando, CancellationToken ct = default)
    {
        var ativo = await ativos.ObterPorIdAsync(comando.BusinessId, comando.AtivoId, ct).ConfigureAwait(false);
        if (ativo is null)
            return Result.Falhar<AtivoDeCapital>(new Error("financeiro.ativo.nao_encontrado", $"Ativo '{comando.AtivoId}' não encontrado."));

        var valorContabil = AtivoDeCapitalQuant.ValorContabilAtualCentavos(ativo);
        var resultado = ativo.Baixar(comando.Motivo, comando.Competencia, valorContabil, relogio.Agora());
        if (resultado.Falha) return Result.Falhar<AtivoDeCapital>(resultado.Erro);

        await ativos.SalvarAsync(ativo, ct).ConfigureAwait(false);
        return Result.Ok(ativo);
    }
}
