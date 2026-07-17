using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Domain.Assinaturas;
using SistemaX.Modules.Financeiro.Domain.Recorrencia;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Application.CasosDeUso;

public sealed record CriarAssinaturaComando(
    string BusinessId, string ClienteId, string ClienteNome, string ServicoId, string ServicoNome,
    Money ValorPorCiclo, FrequenciaRecorrencia Ciclo, int DiaCobranca, DateTimeOffset DataInicio);

public sealed class CriarAssinaturaUseCase(IAssinaturaRepository assinaturas)
{
    public async Task<Result<Assinatura>> ExecutarAsync(CriarAssinaturaComando comando, CancellationToken ct = default)
    {
        var resultado = Assinatura.Criar(
            comando.BusinessId, comando.ClienteId, comando.ClienteNome, comando.ServicoId, comando.ServicoNome,
            comando.ValorPorCiclo, comando.Ciclo, comando.DiaCobranca, comando.DataInicio);
        if (resultado.Falha) return resultado;

        await assinaturas.SalvarAsync(resultado.Valor, ct);
        return resultado;
    }
}

public sealed class CancelarAssinaturaUseCase(IAssinaturaRepository assinaturas, IRelogio relogio)
{
    public async Task<Result> ExecutarAsync(string businessId, string assinaturaId, string motivo, CancellationToken ct = default)
    {
        var assinatura = await assinaturas.BuscarAsync(businessId, assinaturaId, ct);
        if (assinatura is null)
            return Result.Falhar(new Error("financeiro.assinatura.nao_encontrada", "Assinatura não encontrada."));

        var resultado = assinatura.Cancelar(motivo, relogio.Agora());
        if (resultado.Falha) return resultado;

        await assinaturas.SalvarAsync(assinatura, ct);
        return Result.Ok();
    }
}

public sealed class PausarReativarAssinaturaUseCase(IAssinaturaRepository assinaturas)
{
    public async Task<Result> PausarAsync(string businessId, string assinaturaId, CancellationToken ct = default)
        => await MutarAsync(businessId, assinaturaId, a => a.Pausar(), ct);

    public async Task<Result> ReativarAsync(string businessId, string assinaturaId, CancellationToken ct = default)
        => await MutarAsync(businessId, assinaturaId, a => a.Reativar(), ct);

    private async Task<Result> MutarAsync(string businessId, string assinaturaId, Func<Assinatura, Result> acao, CancellationToken ct)
    {
        var assinatura = await assinaturas.BuscarAsync(businessId, assinaturaId, ct);
        if (assinatura is null)
            return Result.Falhar(new Error("financeiro.assinatura.nao_encontrada", "Assinatura não encontrada."));

        var resultado = acao(assinatura);
        if (resultado.Falha) return resultado;

        await assinaturas.SalvarAsync(assinatura, ct);
        return Result.Ok();
    }
}
