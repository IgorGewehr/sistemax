namespace SistemaX.Modules.Fiscal.Application.Ports;

/// <summary>
/// Fronteira MÍNIMA de transação ambiente que a Application pode abrir sem depender de
/// <c>SistemaX.Infrastructure.Local</c> (grafo <c>Infrastructure → Application → Domain</c>,
/// docs/fiscal/arquitetura.md §7 — <c>Fiscal.Application</c> não referencia nenhum adapter
/// concreto). Fecha o gap descrito em <c>EmitirDocumentoFiscalUseCase</c>: aloca o número
/// (<see cref="ISequenciaFiscalRepository"/>) e persiste o <c>DocumentoFiscal</c> em
/// <c>NumeroAlocado</c> (<see cref="IDocumentoFiscalRepository"/>) dentro da MESMA transação
/// local — nunca dois passos separados (§5). Um crash entre as duas chamadas faz rollback do
/// WAL: nem número nem documento avançam, nunca "número consumido, documento não gravado".
///
/// Implementado em Fiscal.Infrastructure: quando a persistência é SQLite, delega para
/// <c>ILocalSessao</c> (a mesma unidade de trabalho ambiente que os 8 repositórios SQLite deste
/// módulo já sabem consultar via <c>ILocalSessao.Atual</c>); quando é InMemory, é um no-op — nada
/// sobrevive a um crash de qualquer forma nesse modo, então não há transação real para coordenar.
/// </summary>
public interface IUnidadeDeTrabalhoFiscal
{
    /// <summary>Inicia (ou reusa, se já iniciada) a transação ambiente. Chamar ANTES de qualquer
    /// repositório que precise participar dela (<c>ISequenciaFiscalRepository.AlocarProximoAsync</c>
    /// + <c>IDocumentoFiscalRepository.SalvarAsync</c> do mesmo documento).</summary>
    Task IniciarAsync(CancellationToken ct = default);

    /// <summary>Confirma a transação ambiente — só depois disso o número e o documento estão
    /// efetivamente comprometidos juntos.</summary>
    Task CommitAsync(CancellationToken ct = default);

    /// <summary>Desfaz a transação ambiente. Chamar sempre que uma falha de negócio (não
    /// excepcional) interrompe o fluxo depois de <see cref="IniciarAsync"/> — libera a conexão de
    /// escrita imediatamente em vez de esperar o fim do escopo de DI.</summary>
    Task RollbackAsync(CancellationToken ct = default);
}
