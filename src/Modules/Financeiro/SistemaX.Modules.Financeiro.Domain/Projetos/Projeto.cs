using SistemaX.Modules.Financeiro.Domain.Comum;
using SistemaX.Modules.Financeiro.Domain.Fsm;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Domain.Projetos;

/// <summary>
/// VALORES PINADOS (persistidos como INTEGER — nunca reordenar, mesma regra de
/// <see cref="Comum.CorrenteDeReceita"/>).
/// </summary>
public enum StatusProjeto
{
    Ativo = 0,
    Arquivado = 1
}

/// <summary>
/// PROJETO — a dimensão "linha de produto" do Financeiro (docs/financeiro/design-analise-por-projeto.md
/// §3.1): agregado leve por tenant que <c>Assinatura</c>/<c>Recorrencia</c>/<c>ContaAReceber</c>/
/// <c>ContaAPagar</c>/<c>MovimentoFinanceiro</c> podem opcionalmente carregar como <c>ProjetoId?</c>
/// (2ª dimensão irmã de <see cref="Comum.CorrenteDeReceita"/> — mesmo padrão nullable/aditivo,
/// <c>null</c> = "sem projeto" = comportamento de hoje, intacto). Existe só sob o toggle
/// <c>ConfiguracaoFinanceiraTenant.AnalisePorProjetoAtiva</c> — ver
/// <c>Application.Projetos.AnalisePorProjetoGuard</c>.
///
/// <see cref="Arquivar"/> NÃO desvincula nada: assinaturas/contas/movimentos mantêm o
/// <c>ProjetoId</c> (histórico intacto, painel continua consultável) — o projeto só some das
/// listas default e dos selects de tagging. Sem "excluir físico" aqui (MVP): projeto é histórico
/// imutável, mesma filosofia de <c>MovimentoFinanceiro</c>.
/// </summary>
public sealed class Projeto : AggregateRoot<string>
{
    public string BusinessId { get; }
    public string Nome { get; private set; }
    public string? Descricao { get; private set; }
    public StatusProjeto Status { get; private set; }
    public DateTimeOffset CriadoEm { get; }
    public DateTimeOffset? ArquivadoEm { get; private set; }

    private Projeto(string id, string businessId, string nome, string? descricao, DateTimeOffset criadoEm)
    {
        Id = id;
        BusinessId = businessId;
        Nome = nome;
        Descricao = descricao;
        Status = StatusProjeto.Ativo;
        CriadoEm = criadoEm;
    }

    public static Result<Projeto> Criar(string businessId, string nome, string? descricao, DateTimeOffset criadoEm)
    {
        if (string.IsNullOrWhiteSpace(businessId))
            return Result.Falhar<Projeto>(new Error("financeiro.projeto.business_obrigatorio", "BusinessId é obrigatório."));

        if (string.IsNullOrWhiteSpace(nome))
            return Result.Falhar<Projeto>(new Error("financeiro.projeto.nome_obrigatorio", "Nome do projeto é obrigatório."));

        var projeto = new Projeto(IdGenerator.NovoId(), businessId, nome.Trim(), descricao, criadoEm);
        projeto.Raise(new ProjetoCriado(projeto.Id, businessId, projeto.Nome, criadoEm));
        return Result.Ok(projeto);
    }

    /// <summary>REIDRATAÇÃO a partir do banco — não valida, não levanta evento.</summary>
    public static Projeto Reconstituir(
        string id, string businessId, string nome, string? descricao, StatusProjeto status,
        DateTimeOffset criadoEm, DateTimeOffset? arquivadoEm)
    {
        var projeto = new Projeto(id, businessId, nome, descricao, criadoEm)
        {
            Status = status,
            ArquivadoEm = arquivadoEm
        };
        return projeto;
    }

    public Result Renomear(string novoNome)
    {
        if (string.IsNullOrWhiteSpace(novoNome))
            return Result.Falhar(new Error("financeiro.projeto.nome_obrigatorio", "Nome do projeto é obrigatório."));

        Nome = novoNome.Trim();
        return Result.Ok();
    }

    public Result AtualizarDescricao(string? descricao)
    {
        Descricao = descricao;
        return Result.Ok();
    }

    public Result Arquivar(DateTimeOffset quando)
    {
        var transicao = StatusProjetoFsm.AssertirTransicao(Status, StatusProjeto.Arquivado);
        if (transicao.Falha) return transicao;

        Status = StatusProjeto.Arquivado;
        ArquivadoEm = quando;
        Raise(new ProjetoArquivado(Id, BusinessId, quando));
        return Result.Ok();
    }

    public Result Reativar()
    {
        var transicao = StatusProjetoFsm.AssertirTransicao(Status, StatusProjeto.Ativo);
        if (transicao.Falha) return transicao;

        Status = StatusProjeto.Ativo;
        ArquivadoEm = null;
        Raise(new ProjetoReativado(Id, BusinessId));
        return Result.Ok();
    }
}
