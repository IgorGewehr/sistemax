using SistemaX.Modules.Financeiro.Domain.Configuracao;

namespace SistemaX.Modules.Financeiro.Application.Ports;

/// <summary>Persistência de <see cref="ConfiguracaoFinanceiraTenant"/> — espelha
/// <c>SistemaX.Modules.Fiscal.Application.Ports.IConfiguracaoFiscalTenantRepository</c>. Ausência
/// de linha = <see cref="ConfiguracaoFinanceiraTenant.Padrao"/> (tudo desligado) — nunca gravado
/// automaticamente, o leitor cai no fallback.</summary>
public interface IConfiguracaoFinanceiraTenantRepository
{
    Task<ConfiguracaoFinanceiraTenant?> ObterAsync(string tenantId, CancellationToken ct = default);

    Task SalvarAsync(ConfiguracaoFinanceiraTenant configuracao, CancellationToken ct = default);
}
