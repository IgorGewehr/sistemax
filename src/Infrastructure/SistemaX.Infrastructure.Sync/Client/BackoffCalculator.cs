namespace SistemaX.Infrastructure.Sync.Client;

/// <summary>Backoff exponencial simples, capado — <c>base * 2^(tentativa-1)</c>, nunca acima de <paramref name="max"/> (usado abaixo).</summary>
public static class BackoffCalculator
{
    public static TimeSpan Calculate(int attempt, TimeSpan baseDelay, TimeSpan max)
    {
        if (attempt <= 0)
        {
            return TimeSpan.Zero;
        }

        var factor = Math.Pow(2, Math.Min(attempt - 1, 32)); // teto no expoente evita overflow de double antes mesmo da comparação com max
        var candidateMs = baseDelay.TotalMilliseconds * factor;

        if (double.IsInfinity(candidateMs) || candidateMs > max.TotalMilliseconds)
        {
            return max;
        }

        return TimeSpan.FromMilliseconds(candidateMs);
    }
}
