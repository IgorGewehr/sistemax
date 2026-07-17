using SistemaX.Modules.Financeiro.Domain.Comum;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Domain.Categorizacao;

/// <summary>
/// Dimensão analítica ORTOGONAL à categoria — filial, setor, projeto. Permite DRE por centro de
/// custo sem duplicar categorias (docs/financeiro-datamodel.md §2.1).
/// </summary>
public sealed class CentroDeCusto : Entity<string>
{
    public string BusinessId { get; }
    public string Nome { get; }
    public bool Ativo { get; private set; }

    private CentroDeCusto(string id, string businessId, string nome)
    {
        Id = id;
        BusinessId = businessId;
        Nome = nome;
        Ativo = true;
    }

    public static CentroDeCusto Criar(string businessId, string nome) => new(IdGenerator.NovoId(), businessId, nome);

    public void Desativar() => Ativo = false;
}
