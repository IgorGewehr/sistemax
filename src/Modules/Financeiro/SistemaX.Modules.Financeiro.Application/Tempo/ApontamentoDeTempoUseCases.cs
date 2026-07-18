using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Application.Projetos;
using SistemaX.Modules.Financeiro.Domain.Tempo;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Application.Tempo;

/// <summary>DTO de fio de <see cref="ApontamentoDeTempo"/> — nunca o agregado direto.
/// <c>custoCentavos</c> é sempre <c>null</c> nesta fatia (decisão travada do dono — ver a entidade)
/// mas o campo já nasce no shape para não quebrar o consumidor quando a valorização chegar.</summary>
public sealed record ApontamentoDeTempoDto(
    string Id, string? ProjetoId, string? ClienteId, string? ClienteNome, string? AssinaturaId, string? OrdemServicoId,
    int Minutos, DateTimeOffset Data, string OperadorId, string OperadorNome, string? Descricao, long? CustoCentavos)
{
    public static ApontamentoDeTempoDto DeDominio(ApontamentoDeTempo a) => new(
        a.Id, a.ProjetoId, a.ClienteId, a.ClienteNome, a.AssinaturaId, a.OrdemServicoId,
        a.Minutos, a.Data, a.OperadorId, a.OperadorNome, a.Descricao, a.CustoCentavos);
}

/// <summary>"Registrar atendimento" (design §5.1, o gesto de 5 segundos): cliente OU assinatura →
/// minutos → descrição opcional → salvar. Se veio <see cref="AssinaturaId"/>, o SERVIDOR deriva
/// <c>clienteId</c>/<c>clienteNome</c>/<c>projetoId</c> dela — o dono nunca classifica duas vezes
/// (§5.1: "o servidor deriva, o dono nunca classifica duas vezes").</summary>
public sealed record RegistrarApontamentoComando(
    string BusinessId, int Minutos, DateTimeOffset Data, string OperadorId, string OperadorNome,
    string? ProjetoId = null, string? ClienteId = null, string? ClienteNome = null, string? AssinaturaId = null,
    string? OrdemServicoId = null, string? Descricao = null);

public sealed class RegistrarApontamentoUseCase(
    IApontamentoDeTempoRepository apontamentos, IAssinaturaRepository assinaturas,
    IConfiguracaoFinanceiraTenantRepository configuracoes, IRelogio relogio)
{
    public async Task<Result<ApontamentoDeTempo>> ExecutarAsync(RegistrarApontamentoComando comando, CancellationToken ct = default)
    {
        var gating = await AnalisePorProjetoGuard.ExigirAtivaAsync(comando.BusinessId, configuracoes, ct).ConfigureAwait(false);
        if (gating.Falha) return Result.Falhar<ApontamentoDeTempo>(gating.Erro);

        var projetoId = comando.ProjetoId;
        var clienteId = comando.ClienteId;
        var clienteNome = comando.ClienteNome;

        if (comando.AssinaturaId is not null)
        {
            var assinatura = await assinaturas.BuscarAsync(comando.BusinessId, comando.AssinaturaId, ct).ConfigureAwait(false);
            if (assinatura is null)
                return Result.Falhar<ApontamentoDeTempo>(new Error("financeiro.apontamento.assinatura_nao_encontrada", $"Assinatura '{comando.AssinaturaId}' não encontrada."));

            projetoId ??= assinatura.ProjetoId;
            clienteId ??= assinatura.ClienteId;
            clienteNome ??= assinatura.ClienteNome;
        }

        var resultado = ApontamentoDeTempo.Criar(
            comando.BusinessId, comando.Minutos, comando.Data, comando.OperadorId, comando.OperadorNome, relogio.Agora(),
            projetoId, clienteId, clienteNome, comando.AssinaturaId, comando.OrdemServicoId, comando.Descricao);
        if (resultado.Falha) return resultado;

        await apontamentos.SalvarAsync(resultado.Valor, ct).ConfigureAwait(false);
        return resultado;
    }
}

/// <summary>Errou, apaga e relança (design §3.4) — delete físico, sem bloqueio de FSM.</summary>
public sealed class ExcluirApontamentoUseCase(IApontamentoDeTempoRepository apontamentos)
{
    public Task<bool> ExecutarAsync(string businessId, string apontamentoId, CancellationToken ct = default)
        => apontamentos.ExcluirAsync(businessId, apontamentoId, ct);
}
