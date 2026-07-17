using RecorrenciaAgg = SistemaX.Modules.Financeiro.Domain.Recorrencia.Recorrencia;

namespace SistemaX.Modules.Financeiro.Application.Ports;

/// <summary>Persistência dos templates de <c>Recorrencia</c> (aluguel, salário, contas fixas).</summary>
public interface IRecorrenciaRepository
{
    Task<IReadOnlyList<RecorrenciaAgg>> ListarAtivasAsync(string businessId, CancellationToken ct = default);
    Task<RecorrenciaAgg?> BuscarAsync(string businessId, string id, CancellationToken ct = default);
    Task SalvarAsync(RecorrenciaAgg recorrencia, CancellationToken ct = default);
}
