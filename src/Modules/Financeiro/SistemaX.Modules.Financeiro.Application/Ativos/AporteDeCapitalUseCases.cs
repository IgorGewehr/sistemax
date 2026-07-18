using SistemaX.Modules.Financeiro.Application.Configuracao;
using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Domain.Ativos;
using SistemaX.Modules.Financeiro.Domain.Comum;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Application.Ativos;

/// <summary>DTO de fio de <see cref="AporteDeCapital"/> — nunca o agregado direto.</summary>
public sealed record AporteDeCapitalDto(string Id, long ValorCentavos, DateOnly Data, string Descricao, DateTimeOffset CriadoEm)
{
    public static AporteDeCapitalDto DeDominio(AporteDeCapital a) => new(a.Id, a.Valor.Centavos, a.Data, a.Descricao, a.CriadoEm);
}

public sealed record RegistrarAporteDeCapitalComando(string BusinessId, long ValorCentavos, DateOnly Data, string Descricao);

/// <summary>"Registrar aporte" (docs/financeiro/design-imobilizado-roi.md §3.3/§8.2) — o gesto de um
/// campo (valor+data+descrição). Gate <c>FinanceiroOptInGuard.ExigirImobilizadoRoiAsync</c>: o
/// aporte só existe pra alimentar o Painel de ROI, então nasce sob o MESMO toggle do painel.</summary>
public sealed class RegistrarAporteDeCapitalUseCase(
    IAporteDeCapitalRepository aportes, IConfiguracaoFinanceiraTenantRepository configuracoes, IRelogio relogio)
{
    public async Task<Result<AporteDeCapital>> ExecutarAsync(RegistrarAporteDeCapitalComando comando, CancellationToken ct = default)
    {
        var gating = await FinanceiroOptInGuard.ExigirImobilizadoRoiAsync(comando.BusinessId, configuracoes, ct).ConfigureAwait(false);
        if (gating.Falha) return Result.Falhar<AporteDeCapital>(gating.Erro);

        var resultado = AporteDeCapital.Criar(comando.BusinessId, new Money(comando.ValorCentavos), comando.Data, comando.Descricao, relogio.Agora());
        if (resultado.Falha) return resultado;

        await aportes.SalvarAsync(resultado.Valor, ct).ConfigureAwait(false);
        return resultado;
    }
}

/// <summary>Errou, apaga e relança (DI5 do design) — delete físico, sem bloqueio de FSM (o aporte
/// não tem uma).</summary>
public sealed class ExcluirAporteDeCapitalUseCase(IAporteDeCapitalRepository aportes)
{
    public Task<bool> ExecutarAsync(string businessId, string aporteId, CancellationToken ct = default)
        => aportes.ExcluirAsync(businessId, aporteId, ct);
}
