using SistemaX.Modules.Financeiro.Domain.Caixa;

namespace SistemaX.Modules.Financeiro.Application.Ports;

/// <summary>Persistência de <see cref="SessaoCaixa"/> — o ritual de caixa físico (docs/wiring/
/// financeiro-telas-restantes.md §4). Toda leitura filtra por <c>businessId</c> (R1 — businessId é
/// sagrado), mesmo quando o <c>id</c> já identifica a sessão sozinho.</summary>
public interface ISessaoCaixaRepository
{
    Task<SessaoCaixa?> ObterPorIdAsync(string businessId, string id, CancellationToken ct = default);

    /// <summary>A sessão ABERTA da conta-caixa informada, se houver — usada tanto por
    /// <c>GET /financeiro/caixa/atual</c> quanto pela invariante "não abrir 2 sessões simultâneas
    /// para o mesmo caixa" (checada em <c>AbrirSessaoCaixaUseCase</c>, não no agregado — depende de
    /// consultar outras instâncias persistidas). <c>null</c> se a conta-caixa não tem sessão aberta
    /// agora.</summary>
    Task<SessaoCaixa?> ObterAbertaPorContaAsync(string businessId, string contaCaixaId, CancellationToken ct = default);

    /// <summary>Histórico de sessões da conta-caixa (abertas e fechadas), mais recente primeiro —
    /// alimenta a <c>SessoesTable</c> do mockup. <paramref name="de"/>/<paramref name="ate"/>
    /// filtram por <see cref="SessaoCaixa.AbertaEm"/>; <c>null</c> em ambos lista tudo.</summary>
    Task<IReadOnlyList<SessaoCaixa>> ListarAsync(
        string businessId, string contaCaixaId, DateTimeOffset? de = null, DateTimeOffset? ate = null, CancellationToken ct = default);

    Task SalvarAsync(SessaoCaixa sessao, CancellationToken ct = default);
}
