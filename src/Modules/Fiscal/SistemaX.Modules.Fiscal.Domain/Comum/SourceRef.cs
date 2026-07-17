namespace SistemaX.Modules.Fiscal.Domain.Comum;

/// <summary>
/// Referência ao fato de origem (módulo + id) que gerou um <c>DocumentoFiscal</c> — mesma forma e
/// mesmo papel do <c>SourceRef</c> do Financeiro/Estoque (cada módulo tem a sua cópia de
/// propósito: nenhum módulo referencia o Domain de outro). É a chave de idempotência: antes de
/// abrir um documento fiscal, a Application consulta o repositório por ela — se já existir, é
/// replay do mesmo evento de integração, não duplica o documento.
/// </summary>
public sealed record SourceRef(string Modulo, string Id)
{
    public string Chave => $"{Modulo}:{Id}";

    public override string ToString() => Chave;
}
