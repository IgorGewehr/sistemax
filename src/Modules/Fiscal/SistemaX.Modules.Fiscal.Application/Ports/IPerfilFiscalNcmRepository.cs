using SistemaX.Modules.Fiscal.Domain.Ncm;
using SistemaX.Modules.Fiscal.Domain.Regimes;

namespace SistemaX.Modules.Fiscal.Application.Ports;

public interface IPerfilFiscalNcmRepository
{
    Task<PerfilFiscalNCM?> ObterAsync(string tenantId, RegimeTributario regime, string ncm, CancellationToken ct = default);

    Task SalvarAsync(PerfilFiscalNCM perfil, CancellationToken ct = default);

    Task<IReadOnlyList<PerfilFiscalNCM>> ListarAsync(string tenantId, RegimeTributario regime, CancellationToken ct = default);
}
