using SistemaX.Infrastructure.Local.UnitOfWork;
using SistemaX.Modules.Fiscal.Application.Ports;

namespace SistemaX.Modules.Fiscal.Infrastructure.Sqlite;

/// <summary>
/// Adapter real de <see cref="IUnidadeDeTrabalhoFiscal"/> — delega 1:1 para <see cref="ILocalSessao"/>
/// (a unidade de trabalho ambiente do resto do repo, docs/persistencia/persistencia-sqlite.md).
/// Só existe para que <c>Fiscal.Application</c> não precise referenciar
/// <c>SistemaX.Infrastructure.Local</c> diretamente (docs/fiscal/arquitetura.md §7) — quem resolve
/// <see cref="ILocalSessao"/> concreta é sempre esta camada de Infrastructure.
/// </summary>
public sealed class UnidadeDeTrabalhoFiscalSqlite(ILocalSessao sessao) : IUnidadeDeTrabalhoFiscal
{
    public async Task IniciarAsync(CancellationToken ct = default) => await sessao.IniciarAsync(ct).ConfigureAwait(false);

    public Task CommitAsync(CancellationToken ct = default) => sessao.CommitAsync(ct);

    public Task RollbackAsync(CancellationToken ct = default) => sessao.RollbackAsync(ct);
}
