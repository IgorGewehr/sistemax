using SistemaX.Modules.Financeiro.Domain.Projetos;

namespace SistemaX.Modules.Financeiro.Application.Ports;

/// <summary>Persistência de <see cref="Projeto"/> — dimensão "linha de produto" do Financeiro
/// (docs/financeiro/design-analise-por-projeto.md). Espelha <c>IAssinaturaRepository</c>.</summary>
public interface IProjetoRepository
{
    Task<IReadOnlyList<Projeto>> ListarAsync(string businessId, bool incluirArquivados, CancellationToken ct = default);

    Task<Projeto?> ObterPorIdAsync(string businessId, string projetoId, CancellationToken ct = default);

    /// <summary>Busca case-insensitive por nome — insumo do índice único de unicidade
    /// (docs/financeiro/design-analise-por-projeto.md §3.1) e do "criei errado, era esse aqui".</summary>
    Task<Projeto?> BuscarPorNomeAsync(string businessId, string nome, CancellationToken ct = default);

    Task SalvarAsync(Projeto projeto, CancellationToken ct = default);
}
