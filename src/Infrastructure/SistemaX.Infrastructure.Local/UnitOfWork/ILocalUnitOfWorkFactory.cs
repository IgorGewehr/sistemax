namespace SistemaX.Infrastructure.Local.UnitOfWork;

/// <summary>Abre um novo <see cref="ILocalUnitOfWork"/> — um <c>BEGIN</c> SQLite. Ver <see cref="ILocalUnitOfWork"/>.</summary>
public interface ILocalUnitOfWorkFactory
{
    Task<ILocalUnitOfWork> BeginAsync(CancellationToken ct = default);
}
