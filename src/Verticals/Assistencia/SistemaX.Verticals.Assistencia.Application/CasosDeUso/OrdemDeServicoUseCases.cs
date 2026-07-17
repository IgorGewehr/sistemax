using SistemaX.SharedKernel;
using SistemaX.Verticals.Assistencia.Application.Ports;

namespace SistemaX.Verticals.Assistencia.Application.CasosDeUso;

/// <summary>
/// Abre a OS — primeira escrita do agregado no repositório (mesmo papel de
/// <c>IniciarVendaUseCase</c> em Vendas.Application). Não levanta evento de integração: nada
/// financeiro nasce só de abrir uma OS.
/// </summary>
public sealed class AbrirOsUseCase(IOrdemDeServicoRepository ordens)
{
    public async Task<Result<OrdemDeServico>> ExecutarAsync(
        string tenantId, string numero, ClienteRef cliente, Equipamento equipamento, string defeitoRelatado,
        DateTimeOffset agora, DateTimeOffset? previsaoEntrega = null, string? osOrigemId = null, CancellationToken ct = default)
    {
        OrdemDeServico os;
        try
        {
            os = OrdemDeServico.Abrir(tenantId, numero, cliente, equipamento, defeitoRelatado, agora, previsaoEntrega, osOrigemId);
        }
        catch (ArgumentException ex)
        {
            return Result.Falhar<OrdemDeServico>(new Error("os.dados_invalidos", ex.Message));
        }

        await ordens.SalvarAsync(os, ct);
        return Result.Ok(os);
    }
}

/// <summary>
/// Transições da FSM que NÃO levantam evento de domínio nenhum — "busca → chama o método do
/// agregado → salva", o mesmo padrão de <c>MontarVendaUseCase</c> (Vendas.Application). As
/// transições que publicam evento de integração (aprovação, aplicação/estorno de peça,
/// conclusão de execução, entrega, devolução, cancelamento) vivem em
/// <c>OrdemDeServicoFaturamentoUseCases.cs</c> — são as únicas que dependem de
/// <c>IIntegrationEventBus</c>.
/// </summary>
public sealed class GerenciarOrdemDeServicoUseCase(IOrdemDeServicoRepository ordens)
{
    public Task<Result> AtribuirTecnicoAsync(string osId, string tecnicoId, string tecnicoNome, CancellationToken ct = default)
        => MutarAsync(osId, os => os.AtribuirTecnico(tecnicoId, tecnicoNome), ct);

    public Task<Result> AlterarPrevisaoEntregaAsync(string osId, DateTimeOffset novaPrevisao, CancellationToken ct = default)
        => MutarAsync(osId, os => os.AlterarPrevisaoEntrega(novaPrevisao), ct);

    public Task<Result> RegistrarDiagnosticoAsync(string osId, string diagnostico, DateTimeOffset agora, CancellationToken ct = default)
        => MutarAsync(osId, os => os.RegistrarDiagnostico(diagnostico, agora), ct);

    public Task<Result> EnviarOrcamentoAsync(
        string osId, IReadOnlyList<PecaOrcada> pecasPrevistas, Money maoDeObra, int validadeDias, DateTimeOffset agora, CancellationToken ct = default)
        => MutarAsync(osId, os => os.EnviarOrcamento(pecasPrevistas, maoDeObra, validadeDias, agora), ct);

    public Task<Result> RegistrarReprovacaoAsync(
        string osId, CanalAprovacao canal, DateTimeOffset agora, string? motivo = null,
        string? registradoPorId = null, string? registradoPorNome = null, CancellationToken ct = default)
        => MutarAsync(osId, os => os.RegistrarReprovacao(canal, agora, motivo, registradoPorId, registradoPorNome), ct);

    public Task<Result> IniciarExecucaoAsync(string osId, DateTimeOffset agora, CancellationToken ct = default)
        => MutarAsync(osId, os => os.IniciarExecucao(agora), ct);

    public Task<Result> AjustarMaoDeObraFinalAsync(string osId, Money novoValor, bool clienteAvisado, CancellationToken ct = default)
        => MutarAsync(osId, os => os.AjustarMaoDeObraFinal(novoValor, clienteAvisado), ct);

    private async Task<Result> MutarAsync(string osId, Func<OrdemDeServico, Result> acao, CancellationToken ct)
    {
        var os = await ordens.ObterPorIdAsync(osId, ct);
        if (os is null)
            return Result.Falhar(new Error("os.nao_encontrada", $"Ordem de serviço '{osId}' não encontrada."));

        var resultado = acao(os);
        if (resultado.Falha) return resultado;

        await ordens.SalvarAsync(os, ct);
        return Result.Ok();
    }
}
