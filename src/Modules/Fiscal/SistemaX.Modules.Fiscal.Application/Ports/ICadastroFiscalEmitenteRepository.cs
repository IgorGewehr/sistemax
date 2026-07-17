namespace SistemaX.Modules.Fiscal.Application.Ports;

/// <summary>Dado CADASTRAL do estabelecimento (CNPJ/Razão Social/IE/IM/endereço) — nunca mora em
/// <c>Fiscal.Domain</c> por não ser fato tributário (mesma razão de NCM/CEST morarem no Estoque,
/// ver docs/fiscal/arquitetura.md §4). Gap #4 de emissao-mapping.md §3 — até o módulo dono do
/// cadastro (Empresa/Tenant) existir e publicar evento de integração, este repositório é
/// alimentado por seed manual (Settings), mesmo padrão de <c>IPerfilFiscalNcmRepository</c>.</summary>
public sealed record CadastroFiscalEmitente(
    string TenantId,
    string Cnpj,
    string RazaoSocial,
    string? NomeFantasia,
    string InscricaoEstadual,
    string? InscricaoMunicipal,
    string Logradouro,
    string Numero,
    string? Complemento,
    string Bairro,
    string CodigoMunicipio,
    string Municipio,
    string Cep,
    string? Telefone);

public interface ICadastroFiscalEmitenteRepository
{
    Task<CadastroFiscalEmitente?> ObterAsync(string tenantId, CancellationToken ct = default);

    Task SalvarAsync(CadastroFiscalEmitente cadastro, CancellationToken ct = default);
}
