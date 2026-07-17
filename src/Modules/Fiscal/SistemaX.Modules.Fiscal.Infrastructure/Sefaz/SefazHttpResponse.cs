namespace SistemaX.Modules.Fiscal.Infrastructure.Sefaz;

/// <summary>Corpo de resposta do gateway de emissão — espelha 1:1 <c>SefazResponse</c> de
/// <c>saas-erp/lib/services/sefaz-gateway.ts</c> (mesmo serviço, mesmo contrato JSON).</summary>
internal sealed class SefazHttpResponse
{
    public bool Success { get; set; }

    /// <summary>"autorizado" | "rejeitado" | "processando" | "denegado" | "cancelado" | "erro".</summary>
    public string? Status { get; set; }

    public string? CodigoStatus { get; set; }
    public string? MotivoStatus { get; set; }
    public string? ChaveAcesso { get; set; }
    public string? Protocolo { get; set; }
    public string? DataRecebimento { get; set; }
    public string? Xml { get; set; }
    public string? NRec { get; set; }
    public List<string>? Erros { get; set; }

    // NFS-e
    public long? NumeroNfse { get; set; }
    public string? CodigoVerificacao { get; set; }
    public string? LinkVisualizacao { get; set; }
    public string? DataEmissao { get; set; }

    // Erro estruturado (sefaz-api) — usado só para diagnóstico/log, nunca pra decidir status.
    public string? Error { get; set; }
    public string? Message { get; set; }
}
