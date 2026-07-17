using SistemaX.Modules.Fiscal.Domain.Regimes;

namespace SistemaX.Modules.Fiscal.Application.Ports;

public interface IConfiguracaoFiscalTenantRepository
{
    Task<ConfiguracaoFiscalTenant?> ObterAsync(string tenantId, CancellationToken ct = default);

    Task SalvarAsync(ConfiguracaoFiscalTenant configuracao, CancellationToken ct = default);
}
