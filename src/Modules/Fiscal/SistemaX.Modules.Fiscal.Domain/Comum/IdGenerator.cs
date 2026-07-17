namespace SistemaX.Modules.Fiscal.Domain.Comum;

/// <summary>
/// Toda entidade do Fiscal usa ULID como id (string) — ordenável por tempo de criação, gerável no
/// terminal/PDV sem coordenação com servidor central (R6 do CLAUDE.md). O NÚMERO fiscal em si
/// (mod. 55/65) nunca é gerado aqui — esse é o papel de <c>SequenciaFiscal</c> (autoridade
/// dedicada, ver docs/fiscal/arquitetura.md §5), este gerador só cria o Id interno do agregado.
/// </summary>
public static class IdGenerator
{
    public static string NovoId() => Ulid.NewUlid().ToString();
}
