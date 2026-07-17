namespace SistemaX.Modules.Compras.Domain.Notas;

/// <summary>Como a nota entrou no sistema. <see cref="Manual"/> é a única origem sem
/// <c>ChaveDeAcesso</c> (compra sem NF-e — produtor rural, pequeno varejo).</summary>
public enum OrigemNota
{
    XmlUpload,
    SefazDfe,
    Manual
}
