namespace SistemaX.Infrastructure.Local.Identity;

/// <summary>
/// Identidade estável DESTE terminal (PDV/servidor de loja). Gerada uma vez no primeiro boot e
/// persistida — nunca recalculada — porque é usada para: (a) namespacing de sequências locais
/// (ex.: <c>"venda:{TerminalId}"</c>), (b) prevenção de eco no pull da camada de Sync ("não
/// recebo de volta as minhas próprias mudanças"), e (c) atribuição de autoria em conflitos
/// resolvidos por "terminal vence".
/// </summary>
public interface ITerminalIdentity
{
    /// <summary>ULID gerado no primeiro boot deste terminal. Estável entre reinicializações.</summary>
    Task<string> GetTerminalIdAsync(CancellationToken ct = default);
}
