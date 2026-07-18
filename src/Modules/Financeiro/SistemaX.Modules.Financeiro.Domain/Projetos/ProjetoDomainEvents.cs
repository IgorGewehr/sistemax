using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Domain.Projetos;

/// <summary>Eventos de domínio de <see cref="Projeto"/> — privados ao módulo (docs/financeiro/
/// design-analise-por-projeto.md §6.1): nada cross-módulo acontece quando um projeto muda, então
/// nenhum destes vira evento de integração.</summary>
public sealed record ProjetoCriado(string ProjetoId, string BusinessId, string Nome, DateTimeOffset CriadoEm) : DomainEvent;

public sealed record ProjetoArquivado(string ProjetoId, string BusinessId, DateTimeOffset Quando) : DomainEvent;

public sealed record ProjetoReativado(string ProjetoId, string BusinessId) : DomainEvent;
