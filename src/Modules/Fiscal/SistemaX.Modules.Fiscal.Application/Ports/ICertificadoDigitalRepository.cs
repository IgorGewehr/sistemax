namespace SistemaX.Modules.Fiscal.Application.Ports;

/// <summary>Certificado digital A1 (.pfx + senha) do tenant — sensível, nunca gravado/logado em
/// texto puro fora deste repositório (mesma disciplina do <c>certificate-manager.ts</c> do
/// saas-erp). Gap #2 de emissao-mapping.md §4.6/§11 — resolvido a cada transmissão a partir de um
/// cofre; o agregado <c>DocumentoFiscal</c> nunca sabe de certificado (é dado de infraestrutura
/// pura). Só é consultado fora do modo MOCK — em MOCK a transmissão nunca assina XML de
/// verdade.</summary>
public sealed record CertificadoDigital(string PfxBase64, string Senha);

public interface ICertificadoDigitalRepository
{
    Task<CertificadoDigital?> ObterAsync(string tenantId, CancellationToken ct = default);

    Task SalvarAsync(string tenantId, CertificadoDigital certificado, CancellationToken ct = default);
}
