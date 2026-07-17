using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Domain.Caixa;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Application.CasosDeUso;

/// <summary>
/// Abre o ritual de caixa físico numa conta-caixa (<see cref="TipoContaBancariaCaixa.CaixaFisico"/>)
/// — a invariante "não abrir 2 sessões simultâneas para o mesmo caixa" NÃO é checável dentro do
/// agregado <see cref="SessaoCaixa"/> (depende de consultar outras instâncias persistidas), então
/// vive aqui: consulta <see cref="ISessaoCaixaRepository.ObterAbertaPorContaAsync"/> ANTES de
/// chamar <see cref="SessaoCaixa.Abrir"/>.
/// </summary>
public sealed class AbrirSessaoCaixaUseCase(ISessaoCaixaRepository sessoes)
{
    public async Task<Result<SessaoCaixa>> ExecutarAsync(
        string businessId, string contaCaixaId, string operadorId, string operadorNome,
        Money saldoAbertura, DateTimeOffset abertaEm, CancellationToken ct = default)
    {
        var aberta = await sessoes.ObterAbertaPorContaAsync(businessId, contaCaixaId, ct).ConfigureAwait(false);
        if (aberta is not null)
            return Result.Falhar<SessaoCaixa>(new Error(
                "financeiro.sessao_caixa.ja_aberta",
                $"Já existe uma sessão de caixa aberta ('{aberta.Id}', desde {aberta.AbertaEm:g}) para esta conta-caixa — feche-a antes de abrir uma nova."));

        var sessao = SessaoCaixa.Abrir(businessId, contaCaixaId, operadorId, operadorNome, saldoAbertura, abertaEm);
        if (sessao.Falha) return sessao;

        await sessoes.SalvarAsync(sessao.Valor, ct).ConfigureAwait(false);
        return sessao;
    }
}

/// <summary>
/// Movimenta uma sessão já aberta (suprimento/sangria) — "busca → chama o método do agregado →
/// salva", mesmo molde de <c>MontarVendaUseCase</c>. Nenhum destes métodos publica evento de
/// integração: <see cref="SuprimentoRegistrado"/>/<see cref="SangriaRegistrada"/> ficam só como
/// fato de domínio (ver nota de design em SessaoCaixaDomainEvents.cs sobre por que não alimentam
/// <c>MovimentoFinanceiro</c> ainda) — nada pra publicar aqui.
/// </summary>
public sealed class MovimentarSessaoCaixaUseCase(ISessaoCaixaRepository sessoes)
{
    public Task<Result> RegistrarSuprimentoAsync(
        string businessId, string sessaoId, Money valor, string motivo, DateTimeOffset quando,
        string operadorId, string operadorNome, CancellationToken ct = default)
        => MutarAsync(businessId, sessaoId, sessao => sessao.RegistrarSuprimento(valor, motivo, quando, operadorId, operadorNome), ct);

    public Task<Result> RegistrarSangriaAsync(
        string businessId, string sessaoId, Money valor, string motivo, DateTimeOffset quando,
        string operadorId, string operadorNome, CancellationToken ct = default)
        => MutarAsync(businessId, sessaoId, sessao => sessao.RegistrarSangria(valor, motivo, quando, operadorId, operadorNome), ct);

    public Task<Result> RegistrarVendaEmEspecieAsync(
        string businessId, string sessaoId, Money valor, DateTimeOffset quando,
        string operadorId, string operadorNome, CancellationToken ct = default)
        => MutarAsync(businessId, sessaoId, sessao => sessao.RegistrarVendaEmEspecie(valor, quando, operadorId, operadorNome), ct);

    private async Task<Result> MutarAsync(string businessId, string sessaoId, Func<SessaoCaixa, Result> acao, CancellationToken ct)
    {
        var sessao = await sessoes.ObterPorIdAsync(businessId, sessaoId, ct).ConfigureAwait(false);
        if (sessao is null)
            return Result.Falhar(new Error("financeiro.sessao_caixa.nao_encontrada", $"Sessão de caixa '{sessaoId}' não encontrada."));

        var resultado = acao(sessao);
        if (resultado.Falha) return resultado;

        await sessoes.SalvarAsync(sessao, ct).ConfigureAwait(false);
        return Result.Ok();
    }
}

/// <summary>
/// Fecha a sessão com a contagem física (cega) — devolve a sessão já com
/// <see cref="SessaoCaixa.Diferenca"/> calculada, o dado que a rota HTTP devolve direto pra tela
/// (ModalFecharCaixa mostra "sobrou/faltou X" assim que o operador confirma a contagem).
/// </summary>
public sealed class FecharSessaoCaixaUseCase(ISessaoCaixaRepository sessoes)
{
    public async Task<Result<SessaoCaixa>> ExecutarAsync(
        string businessId, string sessaoId, Money saldoInformado, DateTimeOffset fechadaEm, CancellationToken ct = default)
    {
        var sessao = await sessoes.ObterPorIdAsync(businessId, sessaoId, ct).ConfigureAwait(false);
        if (sessao is null)
            return Result.Falhar<SessaoCaixa>(new Error("financeiro.sessao_caixa.nao_encontrada", $"Sessão de caixa '{sessaoId}' não encontrada."));

        var resultado = sessao.Fechar(saldoInformado, fechadaEm);
        if (resultado.Falha) return Result.Falhar<SessaoCaixa>(resultado.Erro);

        await sessoes.SalvarAsync(sessao, ct).ConfigureAwait(false);
        return Result.Ok(sessao);
    }
}
