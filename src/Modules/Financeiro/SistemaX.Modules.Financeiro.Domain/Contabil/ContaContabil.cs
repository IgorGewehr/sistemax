using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Domain.Contabil;

/// <summary>
/// Conta do plano de contas de CONTROLE (não é exposta na UI operacional — só o motor de
/// partida dobrada e o "modo detalhado" opt-in do DRE enxergam isto). Catálogo fixo e enxuto,
/// ver <see cref="PlanoDeContasPadrao"/>.
/// </summary>
public sealed class ContaContabil : Entity<string>
{
    public string Codigo { get; }
    public string Nome { get; }
    public TipoContaContabil Tipo { get; }

    private ContaContabil(string codigo, string nome, TipoContaContabil tipo)
    {
        Id = codigo;
        Codigo = codigo;
        Nome = nome;
        Tipo = tipo;
    }

    public static ContaContabil Criar(string codigo, string nome, TipoContaContabil tipo) => new(codigo, nome, tipo);
}
