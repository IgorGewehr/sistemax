using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Domain.Projetos;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Application.Projetos;

/// <summary>DTO de fio de <see cref="Projeto"/> — nunca o agregado direto (mesma convenção do
/// resto do módulo).</summary>
public sealed record ProjetoDto(
    string Id, string Nome, string? Descricao, string Status, DateTimeOffset CriadoEm, DateTimeOffset? ArquivadoEm)
{
    public static ProjetoDto DeDominio(Projeto p) => new(p.Id, p.Nome, p.Descricao, p.Status.ToString(), p.CriadoEm, p.ArquivadoEm);
}

public sealed record CriarProjetoComando(string BusinessId, string Nome, string? Descricao = null);

/// <summary>Cria um projeto — barrado (422) enquanto o toggle
/// <c>ConfiguracaoFinanceiraTenant.AnalisePorProjetoAtiva</c> estiver desligado (§2.2 do design:
/// a própria dimensão só existe sob opt-in). Nome único por tenant, case-insensitive.</summary>
public sealed class CriarProjetoUseCase(
    IProjetoRepository projetos, IConfiguracaoFinanceiraTenantRepository configuracoes, IRelogio relogio)
{
    public async Task<Result<Projeto>> ExecutarAsync(CriarProjetoComando comando, CancellationToken ct = default)
    {
        var gating = await AnalisePorProjetoGuard.ExigirAtivaAsync(comando.BusinessId, configuracoes, ct).ConfigureAwait(false);
        if (gating.Falha) return Result.Falhar<Projeto>(gating.Erro);

        var existente = await projetos.BuscarPorNomeAsync(comando.BusinessId, comando.Nome, ct).ConfigureAwait(false);
        if (existente is not null)
            return Result.Falhar<Projeto>(new Error("financeiro.projeto.nome_duplicado", $"Já existe um projeto chamado '{comando.Nome}' neste tenant."));

        var resultado = Projeto.Criar(comando.BusinessId, comando.Nome, comando.Descricao, relogio.Agora());
        if (resultado.Falha) return resultado;

        await projetos.SalvarAsync(resultado.Valor, ct).ConfigureAwait(false);
        return resultado;
    }
}

public sealed record RenomearProjetoComando(string BusinessId, string ProjetoId, string? Nome = null, string? Descricao = null, bool AtualizarDescricao = false);

/// <summary>Renomear/editar descrição — não exige o toggle ligado para EDITAR um projeto que já
/// existe (só a CRIAÇÃO de projeto/tagging novo é barrada; um projeto que sobreviveu de quando o
/// toggle estava ligado continua editável mesmo se o dono desligar depois — histórico intacto).</summary>
public sealed class EditarProjetoUseCase(IProjetoRepository projetos)
{
    public async Task<Result<Projeto>> ExecutarAsync(RenomearProjetoComando comando, CancellationToken ct = default)
    {
        var projeto = await projetos.ObterPorIdAsync(comando.BusinessId, comando.ProjetoId, ct).ConfigureAwait(false);
        if (projeto is null) return Result.Falhar<Projeto>(ProjetoNaoEncontrado(comando.ProjetoId));

        if (comando.Nome is { } nome)
        {
            var existente = await projetos.BuscarPorNomeAsync(comando.BusinessId, nome, ct).ConfigureAwait(false);
            if (existente is not null && existente.Id != projeto.Id)
                return Result.Falhar<Projeto>(new Error("financeiro.projeto.nome_duplicado", $"Já existe um projeto chamado '{nome}' neste tenant."));

            var renomear = projeto.Renomear(nome);
            if (renomear.Falha) return Result.Falhar<Projeto>(renomear.Erro);
        }

        if (comando.AtualizarDescricao)
            projeto.AtualizarDescricao(comando.Descricao);

        await projetos.SalvarAsync(projeto, ct).ConfigureAwait(false);
        return Result.Ok(projeto);
    }

    internal static Error ProjetoNaoEncontrado(string projetoId) => new("financeiro.projeto.nao_encontrado", $"Projeto '{projetoId}' não encontrado.");
}

/// <summary>Arquivar/reativar — puramente FSM, NUNCA desvincula os fatos já tagueados (§3.1 do
/// design: histórico imutável, o projeto só some das listas default/selects).</summary>
public sealed class ArquivarReativarProjetoUseCase(IProjetoRepository projetos, IRelogio relogio)
{
    public async Task<Result<Projeto>> ArquivarAsync(string businessId, string projetoId, CancellationToken ct = default)
        => await MutarAsync(businessId, projetoId, p => p.Arquivar(relogio.Agora()), ct).ConfigureAwait(false);

    public async Task<Result<Projeto>> ReativarAsync(string businessId, string projetoId, CancellationToken ct = default)
        => await MutarAsync(businessId, projetoId, p => p.Reativar(), ct).ConfigureAwait(false);

    private async Task<Result<Projeto>> MutarAsync(string businessId, string projetoId, Func<Projeto, Result> acao, CancellationToken ct)
    {
        var projeto = await projetos.ObterPorIdAsync(businessId, projetoId, ct).ConfigureAwait(false);
        if (projeto is null) return Result.Falhar<Projeto>(EditarProjetoUseCase.ProjetoNaoEncontrado(projetoId));

        var resultado = acao(projeto);
        if (resultado.Falha) return Result.Falhar<Projeto>(resultado.Erro);

        await projetos.SalvarAsync(projeto, ct).ConfigureAwait(false);
        return Result.Ok(projeto);
    }
}
