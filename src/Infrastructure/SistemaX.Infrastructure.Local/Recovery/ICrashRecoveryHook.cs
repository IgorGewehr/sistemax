using Microsoft.Data.Sqlite;

namespace SistemaX.Infrastructure.Local.Recovery;

/// <summary>
/// Ponto de extensão de crash-recovery específico de domínio, executado no BOOT depois que o
/// banco local já está íntegro (pós <see cref="ICorruptionRecoveryService"/>). Este projeto
/// (Infrastructure.Local) não conhece "venda", "rascunho de carrinho" ou qualquer conceito de
/// negócio — quem sabe o que precisa checar/recuperar ao ligar é o módulo dono do dado (ex.:
/// Vendas.Infrastructure verificando se há uma venda em status intermediário órfã).
///
/// Mesma filosofia de plugin do <c>IModule</c> (ver SistemaX.Modules.Abstractions): o Core nunca
/// conhece hooks concretos, só descobre via DI (<c>IEnumerable&lt;ICrashRecoveryHook&gt;</c>) e
/// os executa em sequência através de <see cref="CrashRecoveryRunner"/>.
///
/// IMPORTANTE (lição do Supermarket-OS §1): um hook de recuperação NUNCA deve reaplicar estado
/// ambíguo silenciosamente quando dinheiro real pode estar envolvido — se encontrar algo a
/// recuperar, prefira sinalizar (log/flag para a UI perguntar ao operador) a decidir sozinho.
/// </summary>
public interface ICrashRecoveryHook
{
    /// <summary>Nome estável do hook, usado em logs (ex.: "vendas.rascunho-em-andamento").</summary>
    string Nome { get; }

    /// <summary>
    /// Executado com uma conexão já aberta (pragmas aplicados) mas SEM transação — o hook decide
    /// se e como agrupar suas próprias operações em transação.
    /// </summary>
    Task ExecutarAsync(SqliteConnection connection, CancellationToken ct);
}
