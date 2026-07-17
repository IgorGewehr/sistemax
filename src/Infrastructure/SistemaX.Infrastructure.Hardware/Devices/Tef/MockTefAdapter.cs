using System.Collections.Concurrent;
using SistemaX.Infrastructure.Hardware.Common;
using SistemaX.SharedKernel;

namespace SistemaX.Infrastructure.Hardware.Devices.Tef;

/// <summary>
/// Adapter simulado para desenvolvimento/testes — sem adquirente real por trás. O estado das
/// transações é <c>static</c> (compartilhado entre instâncias) de propósito: simula o que um
/// adquirente de verdade faria (lembrar da transação mesmo que o processo do PDV crie um novo
/// objeto adapter após reconectar) — é isso que permite testar
/// <see cref="TefFallbackCoordinator"/> consultando o status "no adquirente" depois de um timeout
/// simulado sem perder o estado.
/// </summary>
public sealed class MockTefAdapter(string provider = "mock") : ITefAdapter
{
    private static readonly ConcurrentDictionary<string, TefTransactionResult> Transacoes = new();

    /// <summary>Atraso simulado de rede/adquirente antes de responder — configurável em testes para forçar timeout no coordinator.</summary>
    public TimeSpan AtrasoSimulado { get; init; } = TimeSpan.FromMilliseconds(200);

    public string Provider { get; } = provider;

    public DeviceHealth Health { get; private set; } = new(DeviceStatus.Connected, null, DateTimeOffset.UtcNow);

    public Task<Result> ConnectAsync(CancellationToken ct = default)
    {
        Health = new DeviceHealth(DeviceStatus.Connected, null, DateTimeOffset.UtcNow);
        return Task.FromResult(Result.Ok());
    }

    public Task DisconnectAsync(CancellationToken ct = default)
    {
        Health = Health with { Status = DeviceStatus.Disconnected };
        return Task.CompletedTask;
    }

    public async Task<Result<TefTransactionResult>> StartTransactionAsync(TefTransactionRequest request, CancellationToken ct = default)
    {
        // O processamento "no adquirente" roda em CancellationToken.None deliberadamente: um
        // adquirente real continua processando a cobrança mesmo que o cliente cancele a espera
        // local (timeout) — é exatamente essa independência que torna um timeout local ambíguo
        // (não significa "não foi cobrado") e que exige consultar o status depois, em vez de
        // simplesmente reenviar (ver TefFallbackCoordinator). Se este método usasse `ct` também
        // no Delay/gravação, cancelar aqui apagaria a "cobrança" — o oposto do que se quer testar.
        var processamentoNoAdquirente = Task.Run(async () =>
        {
            await Task.Delay(AtrasoSimulado, CancellationToken.None).ConfigureAwait(false);

            // Chave pela IdempotencyKey — demonstra o contrato que um adapter real DEVE seguir
            // (ver TefTransactionRequest.IdempotencyKey): reenviar a MESMA chave nunca gera nova cobrança.
            return Transacoes.GetOrAdd(request.IdempotencyKey, _ => new TefTransactionResult(
                Status: TefTransactionStatus.Approved,
                Nsu: Random.Shared.Next(100_000, 999_999).ToString(),
                CodigoAutorizacao: Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
                Bandeira: "MOCK",
                MensagemAdquirente: "Aprovado (simulado)"));
        }, CancellationToken.None);

        var cancellationTask = Task.Delay(Timeout.InfiniteTimeSpan, ct);
        var winner = await Task.WhenAny(processamentoNoAdquirente, cancellationTask).ConfigureAwait(false);

        if (winner == processamentoNoAdquirente)
        {
            return Result.Ok(await processamentoNoAdquirente.ConfigureAwait(false));
        }

        // Nosso lado cancelou (timeout do coordinator) — mas processamentoNoAdquirente CONTINUA
        // rodando em background e vai gravar em Transacoes quando terminar, exatamente como um
        // adquirente real terminaria de processar independente do cliente ter desistido de esperar.
        ct.ThrowIfCancellationRequested();
        throw new InvalidOperationException("Estado inalcançável: cancellationTask só completa quando ct é cancelado.");
    }

    public Task<Result<TefStatusConsultaResult>> GetTransactionStatusAsync(string idempotencyKey, CancellationToken ct = default)
    {
        if (Transacoes.TryGetValue(idempotencyKey, out var resultado))
        {
            return Task.FromResult(Result.Ok(new TefStatusConsultaResult(resultado.Status, resultado.Nsu, resultado.MensagemAdquirente)));
        }

        // O "adquirente" simulado nunca viu essa chave — de fato não foi processada.
        return Task.FromResult(Result.Ok(new TefStatusConsultaResult(TefTransactionStatus.Unknown, null, "Transação não encontrada no adquirente (simulado).")));
    }
}
