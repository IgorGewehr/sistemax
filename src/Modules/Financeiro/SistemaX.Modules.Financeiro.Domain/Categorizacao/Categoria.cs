using SistemaX.Modules.Financeiro.Domain.Comum;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Domain.Categorizacao;

/// <summary>
/// Classificação gerencial de um lançamento ("Serviços", "Comissões", "Aluguel"...). Toda
/// categoria mapeia para EXATAMENTE uma <see cref="LinhaDre"/> — é isso que faz o DRE nascer
/// de graça da categorização, sem trabalho manual extra (docs/financeiro-features.md §4.4).
/// </summary>
public sealed class Categoria : Entity<string>
{
    public string BusinessId { get; }
    public string Nome { get; }
    public string LinhaDreId { get; }
    public bool Ativa { get; private set; }

    private Categoria(string id, string businessId, string nome, string linhaDreId)
    {
        Id = id;
        BusinessId = businessId;
        Nome = nome;
        LinhaDreId = linhaDreId;
        Ativa = true;
    }

    public static Categoria Criar(string businessId, string nome, string linhaDreId)
        => new(IdGenerator.NovoId(), businessId, nome, linhaDreId);

    public void Desativar() => Ativa = false;
}
