using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Domain.Categorizacao;

/// <summary>Grupo estrutural fixo da árvore de DRE gerencial (docs/financeiro-features.md §4.4).</summary>
public enum GrupoLinhaDre
{
    ReceitaBruta,
    Deducoes,
    Cmv,
    DespesaOperacional,
    ResultadoFinanceiro,
    ProLaboreDistribuicao
}

/// <summary>
/// Nó de uma árvore FIXA de DRE (Receita Bruta → Deduções → CMV/CPV → Despesas Operacionais →
/// Resultado). Não é editável pelo usuário — é o catálogo estrutural que toda <see cref="Categoria"/>
/// mapeia para exatamente uma linha.
/// </summary>
public sealed class LinhaDre : Entity<string>
{
    public string Nome { get; }
    public GrupoLinhaDre Grupo { get; }
    public int OrdemExibicao { get; }

    private LinhaDre(string id, string nome, GrupoLinhaDre grupo, int ordemExibicao)
    {
        Id = id;
        Nome = nome;
        Grupo = grupo;
        OrdemExibicao = ordemExibicao;
    }

    public static LinhaDre Criar(string id, string nome, GrupoLinhaDre grupo, int ordemExibicao)
        => new(id, nome, grupo, ordemExibicao);
}

/// <summary>
/// Catálogo fixo mínimo de linhas de DRE — suficiente para o DRE gerencial simplificado do MVP
/// (docs/financeiro/financeiro-features.md, priorização #9). Expandir com sub-linhas conforme a
/// Fase 2 (centro de custo detalhado) evoluir.
/// </summary>
public static class LinhasDrePadrao
{
    public static readonly LinhaDre ReceitaBruta = LinhaDre.Criar("dre-receita-bruta", "Receita Bruta", GrupoLinhaDre.ReceitaBruta, 1);
    public static readonly LinhaDre Deducoes = LinhaDre.Criar("dre-deducoes", "Deduções e Impostos sobre Venda", GrupoLinhaDre.Deducoes, 2);
    public static readonly LinhaDre Cmv = LinhaDre.Criar("dre-cmv", "Custo/Comissão Direta (CMV/CPV)", GrupoLinhaDre.Cmv, 3);
    public static readonly LinhaDre DespesaOperacional = LinhaDre.Criar("dre-despesa-operacional", "Despesas Operacionais (fixas)", GrupoLinhaDre.DespesaOperacional, 4);
    public static readonly LinhaDre ResultadoFinanceiro = LinhaDre.Criar("dre-resultado-financeiro", "Resultado Financeiro (juros/tarifas)", GrupoLinhaDre.ResultadoFinanceiro, 5);
    public static readonly LinhaDre ProLaboreDistribuicao = LinhaDre.Criar("dre-pro-labore", "Pró-labore/Distribuição", GrupoLinhaDre.ProLaboreDistribuicao, 6);

    public static IReadOnlyCollection<LinhaDre> Todas { get; } =
    [
        ReceitaBruta, Deducoes, Cmv, DespesaOperacional, ResultadoFinanceiro, ProLaboreDistribuicao
    ];
}
