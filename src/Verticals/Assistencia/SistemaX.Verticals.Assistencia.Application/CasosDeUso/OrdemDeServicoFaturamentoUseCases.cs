using SistemaX.Modules.Abstractions;
using SistemaX.SharedKernel;
using SistemaX.Verticals.Assistencia.Application.Ports;

namespace SistemaX.Verticals.Assistencia.Application.CasosDeUso;

/// <summary>
/// As transições da FSM que levantam evento(s) de DOMÍNIO traduzidos para evento(s) de INTEGRAÇÃO
/// (P0-2, docs/financeiro/revisao-domain-fit-cnpj.md): aprovação (reserva de peça), aplicação de
/// peça orçada/extra (consumo), conclusão de execução (libera sobra), entrega/devolução sem reparo
/// (faturamento — <c>OsFaturada</c>) e cancelamento (libera + estorna). Mesmo padrão exato de
/// <c>ConcluirVendaUseCase</c>/<c>EstornarVendaUseCase</c> (Vendas.Application): "busca → chama o
/// método do agregado → COMMIT LOCAL → SÓ DEPOIS publica, na ordem em que o agregado os levantou →
/// limpa a lista". Nunca o inverso — publicar antes do commit deixaria o Financeiro/Estoque
/// reagirem a um fato que pode não ter sido persistido.
///
/// As demais transições (sem efeito de integração) continuam em
/// <c>GerenciarOrdemDeServicoUseCase</c> (<c>OrdemDeServicoUseCases.cs</c>).
/// </summary>
public sealed class OrdemDeServicoFaturamentoUseCases(IOrdemDeServicoRepository ordens, IIntegrationEventBus barramentoDeEventos)
{
    /// <summary>Aprova o orçamento — cada peça prevista com produto de catálogo levanta
    /// <see cref="PecaReservadaDomainEvent"/>.</summary>
    public Task<Result> RegistrarAprovacaoAsync(
        string osId, CanalAprovacao canal, DateTimeOffset agora, string? registradoPorId = null,
        string? registradoPorNome = null, CancellationToken ct = default)
        => MutarEPublicarAsync(osId, os => os.RegistrarAprovacao(canal, agora, registradoPorId, registradoPorNome), ct);

    /// <summary>Aplica peça pré-orçada — levanta <see cref="PecaConsumidaDomainEvent"/> quando a
    /// linha tem produto de catálogo.</summary>
    public Task<Result> AplicarPecaAsync(string osId, string linhaId, DateTimeOffset agora, CancellationToken ct = default)
        => MutarEPublicarAsync(osId, os => os.AplicarPeca(linhaId, agora), ct);

    /// <summary>Peça fora do orçamento (guarda de valor: cliente precisa estar avisado) — mesmo
    /// evento de consumo da peça pré-orçada.</summary>
    public Task<Result> AdicionarPecaExtraAsync(
        string osId, string? produtoId, string descricao, int quantidade, Money precoUnitario,
        bool clienteAvisado, DateTimeOffset agora, CancellationToken ct = default)
        => MutarEPublicarAsync(
            osId, os => os.AdicionarPecaExtra(produtoId, descricao, quantidade, precoUnitario, clienteAvisado, agora), ct);

    /// <summary>Fecha a execução — peça prevista e reservada mas nunca aplicada libera a reserva
    /// (<see cref="ReservaLiberadaDomainEvent"/> por linha sobrando).</summary>
    public Task<Result> ConcluirExecucaoAsync(string osId, DateTimeOffset agora, CancellationToken ct = default)
        => MutarEPublicarAsync(osId, os => os.ConcluirExecucao(agora), ct);

    /// <summary>Fatura E entrega no mesmo ato — levanta <see cref="OsFaturadaDomainEvent"/> quando
    /// o total geral não é zero (garantia com peça/mão de obra a custo zero não fatura).</summary>
    public Task<Result> EntregarAsync(
        string osId, FormaPagamento formaPagamento, Money desconto, int garantiaDias, DateTimeOffset agora,
        CancellationToken ct = default)
        => MutarEPublicarAsync(osId, os => os.Entregar(formaPagamento, desconto, garantiaDias, agora), ct);

    /// <summary>Devolve sem reparo — taxa de diagnóstico positiva levanta <c>OsFaturada</c> só de
    /// serviço (sem forma de pagamento: fica em aberto no Financeiro, nunca "atrasado fantasma").</summary>
    public Task<Result> DevolverSemReparoAsync(
        string osId, Money taxaDiagnostico, DateTimeOffset agora, CancellationToken ct = default)
        => MutarEPublicarAsync(osId, os => os.DevolverSemReparo(taxaDiagnostico, agora), ct);

    /// <summary>Cancela — libera reservas restantes e, se já havia execução em andamento, estorna
    /// as baixas já feitas (<see cref="ReservaLiberadaDomainEvent"/> + <see cref="ConsumoEstornadoDomainEvent"/>).</summary>
    public Task<Result> CancelarAsync(string osId, string motivo, DateTimeOffset agora, CancellationToken ct = default)
        => MutarEPublicarAsync(osId, os => os.Cancelar(motivo, agora), ct);

    private async Task<Result> MutarEPublicarAsync(string osId, Func<OrdemDeServico, Result> acao, CancellationToken ct)
    {
        var os = await ordens.ObterPorIdAsync(osId, ct);
        if (os is null)
            return Result.Falhar(new Error("os.nao_encontrada", $"Ordem de serviço '{osId}' não encontrada."));

        var resultado = acao(os);
        if (resultado.Falha) return resultado;

        await ordens.SalvarAsync(os, ct); // commit local confirmado — só depois publica

        foreach (var evento in os.DomainEvents)
        {
            IIntegrationEvent? eventoDeIntegracao = evento switch
            {
                OsFaturadaDomainEvent e => e.ParaEventoDeIntegracao(),
                PecaReservadaDomainEvent e => e.ParaEventoDeIntegracao(),
                PecaConsumidaDomainEvent e => e.ParaEventoDeIntegracao(),
                ReservaLiberadaDomainEvent e => e.ParaEventoDeIntegracao(),
                ConsumoEstornadoDomainEvent e => e.ParaEventoDeIntegracao(),
                _ => null,
            };

            if (eventoDeIntegracao is not null)
                await barramentoDeEventos.PublishAsync(eventoDeIntegracao, ct);
        }

        os.ClearDomainEvents();
        return Result.Ok();
    }
}
