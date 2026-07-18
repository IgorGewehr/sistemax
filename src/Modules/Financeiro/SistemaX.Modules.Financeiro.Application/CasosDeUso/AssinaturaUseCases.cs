using SistemaX.Modules.Financeiro.Application.Mrr;
using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Application.Projetos;
using SistemaX.Modules.Financeiro.Domain.Assinaturas;
using SistemaX.Modules.Financeiro.Domain.Recorrencia;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Application.CasosDeUso;

public sealed record CriarAssinaturaComando(
    string BusinessId, string ClienteId, string ClienteNome, string ServicoId, string ServicoNome,
    Money ValorPorCiclo, FrequenciaRecorrencia Ciclo, int DiaCobranca, DateTimeOffset DataInicio, string? ProjetoId = null);

public sealed class CriarAssinaturaUseCase(
    IAssinaturaRepository assinaturas, IMovimentoMrrRepository movimentosMrr, IConfiguracaoFinanceiraTenantRepository configuracoes)
{
    public async Task<Result<Assinatura>> ExecutarAsync(CriarAssinaturaComando comando, CancellationToken ct = default)
    {
        var gating = await AnalisePorProjetoGuard.ExigirAtivaSeProjetoIdAsync(comando.BusinessId, comando.ProjetoId, configuracoes, ct).ConfigureAwait(false);
        if (gating.Falha) return Result.Falhar<Assinatura>(gating.Erro);

        var resultado = Assinatura.Criar(
            comando.BusinessId, comando.ClienteId, comando.ClienteNome, comando.ServicoId, comando.ServicoNome,
            comando.ValorPorCiclo, comando.Ciclo, comando.DiaCobranca, comando.DataInicio, comando.ProjetoId);
        if (resultado.Falha) return resultado;

        var assinatura = resultado.Valor;
        await assinaturas.SalvarAsync(assinatura, ct);
        await RegistradorDeMovimentoMrr.RegistrarTodosAsync(assinatura, movimentosMrr, ct);
        return resultado;
    }
}

/// <summary>Tagging/re-tagging pós-criação (design §8.5: <c>POST /financeiro/assinaturas/{id}/projeto</c>)
/// — barrado (422) enquanto o toggle estiver desligado, mesma regra de criação.</summary>
public sealed class VincularProjetoAssinaturaUseCase(
    IAssinaturaRepository assinaturas, IConfiguracaoFinanceiraTenantRepository configuracoes, IRelogio relogio)
{
    public async Task<Result<Assinatura>> ExecutarAsync(string businessId, string assinaturaId, string? projetoId, CancellationToken ct = default)
    {
        var gating = await AnalisePorProjetoGuard.ExigirAtivaSeProjetoIdAsync(businessId, projetoId, configuracoes, ct).ConfigureAwait(false);
        if (gating.Falha) return Result.Falhar<Assinatura>(gating.Erro);

        var assinatura = await assinaturas.BuscarAsync(businessId, assinaturaId, ct);
        if (assinatura is null)
            return Result.Falhar<Assinatura>(new Error("financeiro.assinatura.nao_encontrada", "Assinatura não encontrada."));

        var resultado = assinatura.VincularProjeto(projetoId, relogio.Agora());
        if (resultado.Falha) return Result.Falhar<Assinatura>(resultado.Erro);

        await assinaturas.SalvarAsync(assinatura, ct);
        return Result.Ok(assinatura);
    }
}

public sealed class CancelarAssinaturaUseCase(IAssinaturaRepository assinaturas, IMovimentoMrrRepository movimentosMrr, IRelogio relogio)
{
    public async Task<Result> ExecutarAsync(string businessId, string assinaturaId, string motivo, CancellationToken ct = default)
    {
        var assinatura = await assinaturas.BuscarAsync(businessId, assinaturaId, ct);
        if (assinatura is null)
            return Result.Falhar(new Error("financeiro.assinatura.nao_encontrada", "Assinatura não encontrada."));

        var resultado = assinatura.Cancelar(motivo, relogio.Agora());
        if (resultado.Falha) return resultado;

        await assinaturas.SalvarAsync(assinatura, ct);
        await RegistradorDeMovimentoMrr.RegistrarTodosAsync(assinatura, movimentosMrr, ct);
        return Result.Ok();
    }
}

/// <summary>P1-4 — troca de plano/valor de uma assinatura ativa (<see cref="Assinatura.AlterarValor"/>):
/// expansão se subiu, contração se caiu, no-op (sem movimento) se o MRR normalizado não mudou.</summary>
public sealed class AlterarValorAssinaturaUseCase(IAssinaturaRepository assinaturas, IMovimentoMrrRepository movimentosMrr, IRelogio relogio)
{
    public async Task<Result> ExecutarAsync(string businessId, string assinaturaId, Money novoValorPorCiclo, CancellationToken ct = default)
    {
        var assinatura = await assinaturas.BuscarAsync(businessId, assinaturaId, ct);
        if (assinatura is null)
            return Result.Falhar(new Error("financeiro.assinatura.nao_encontrada", "Assinatura não encontrada."));

        var resultado = assinatura.AlterarValor(novoValorPorCiclo, relogio.Agora());
        if (resultado.Falha) return resultado;

        await assinaturas.SalvarAsync(assinatura, ct);
        await RegistradorDeMovimentoMrr.RegistrarTodosAsync(assinatura, movimentosMrr, ct);
        return Result.Ok();
    }
}

public sealed class PausarReativarAssinaturaUseCase(IAssinaturaRepository assinaturas, IMovimentoMrrRepository movimentosMrr, IRelogio relogio)
{
    public async Task<Result> PausarAsync(string businessId, string assinaturaId, CancellationToken ct = default)
        => await MutarAsync(businessId, assinaturaId, a => a.Pausar(relogio.Agora()), ct);

    public async Task<Result> ReativarAsync(string businessId, string assinaturaId, CancellationToken ct = default)
        => await MutarAsync(businessId, assinaturaId, a => a.Reativar(relogio.Agora()), ct);

    private async Task<Result> MutarAsync(string businessId, string assinaturaId, Func<Assinatura, Result> acao, CancellationToken ct)
    {
        var assinatura = await assinaturas.BuscarAsync(businessId, assinaturaId, ct);
        if (assinatura is null)
            return Result.Falhar(new Error("financeiro.assinatura.nao_encontrada", "Assinatura não encontrada."));

        var resultado = acao(assinatura);
        if (resultado.Falha) return resultado;

        await assinaturas.SalvarAsync(assinatura, ct);
        await RegistradorDeMovimentoMrr.RegistrarTodosAsync(assinatura, movimentosMrr, ct);
        return Result.Ok();
    }
}
