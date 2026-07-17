namespace SistemaX.Host.Desktop.Updates;

/// <summary>
/// Atualização automática (ADR-0004) — OPCIONAL por design: só liga se houver uma URL de feed
/// configurada (<c>HostConfig.AtualizacaoFeedUrl</c> em <c>config.json</c>, ou
/// <c>SISTEMAX_UPDATE_FEED_URL</c>). Sem feed, <see cref="Habilitado"/> é <c>false</c> e
/// <see cref="VerificarEAplicarAsync"/> é um NO-OP que só loga — este serviço nunca inventa,
/// assume ou promete um feed que não foi configurado por quem operou o deploy.
/// </summary>
public interface IServicoDeAtualizacao
{
    /// <summary>Verdadeiro só quando há feed configurado — nunca um default inventado.</summary>
    bool Habilitado { get; }

    /// <summary>
    /// Checa o feed, baixa uma atualização disponível e agenda a aplicação para o próximo
    /// fechamento natural do app (nunca força reinício no meio de uma venda/sessão — coerente com
    /// o princípio local-first do ADR-0001). NUNCA lança: qualquer falha (rede, feed fora do ar,
    /// instância não instalada via Velopack) é logada como aviso e o app segue rodando normal.
    /// </summary>
    Task VerificarEAplicarAsync(CancellationToken cancellationToken);
}
