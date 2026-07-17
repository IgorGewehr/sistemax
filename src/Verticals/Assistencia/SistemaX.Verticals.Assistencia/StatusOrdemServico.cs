namespace SistemaX.Verticals.Assistencia;

/// <summary>
/// FSM de <see cref="OrdemDeServico"/> — fluxo completo de uma assistência técnica:
/// equipamento → defeito → diagnóstico → orçamento (peças + mão-de-obra) → aprovação →
/// execução → pronta → entrega (fatura e entrega são o MESMO ato — o cliente paga na retirada).
///
/// <code>
///   Aberta ──RegistrarDiagnostico()──► EmDiagnostico ──EnviarOrcamento()──► AguardandoAprovacao ⟲ (reenvio substitui)
///                                                                              │            │
///                                                       RegistrarAprovacao()───┤            └──RegistrarReprovacao()
///                                                                              ▼                        ▼
///                                                                          Aprovada                 Reprovada
///                                                        [reserva peças]       │                        │
///                                                                   IniciarExecucao()          DevolverSemReparo()
///                                                                              ▼               [taxa? → OsFaturada]
///                             AplicarPeca()/AdicionarPecaExtra()* ◄────── EmExecucao                    ▼
///                                          [baixa peça a peça]              │                 DevolvidaSemReparo (T)
///                                                                 ConcluirExecucao()
///                                                       [libera reservas não usadas]
///                                                                              ▼
///                                                                          Pronta
///                                                            Entregar(pagamento, desconto)
///                                                          [fatura + entrega no mesmo commit]
///                                                                              ▼
///                                                                          Entregue (T)
///
///   Cancelar(motivo) — de Aberta/EmDiagnostico/AguardandoAprovacao/Aprovada/EmExecucao → Cancelada (T)
///                       (libera reservas restantes; estorna baixas se cancelada em execução)
/// </code>
///
/// Terminais (T): <see cref="Entregue"/>, <see cref="DevolvidaSemReparo"/>, <see cref="Cancelada"/>.
/// Atraso NÃO é estado — é derivado de <c>PrevisaoEntrega</c> (ver <c>OrdemDeServico.EstaAtrasada</c>),
/// mesma decisão de tratar urgência como cálculo, não status.
/// </summary>
public enum StatusOrdemServico
{
    /// <summary>Equipamento recebido, defeito relatado pelo cliente registrado.</summary>
    Aberta,

    /// <summary>Técnico investigando — <c>Diagnostico</c> preenchido, técnico já atribuído.</summary>
    EmDiagnostico,

    /// <summary>Orçamento (peças previstas + mão de obra) enviado, aguardando decisão do cliente.
    /// Reenviar orçamento neste mesmo estado é permitido (substitui o anterior).</summary>
    AguardandoAprovacao,

    /// <summary>Cliente aprovou — dispara reserva de peças previstas com produto de catálogo.</summary>
    Aprovada,

    /// <summary>Cliente recusou o orçamento. NÃO é terminal — o equipamento ainda está na loja
    /// até <see cref="DevolvidaSemReparo"/>.</summary>
    Reprovada,

    /// <summary>Peças sendo aplicadas (baixa peça a peça), mão de obra final em ajuste.</summary>
    EmExecucao,

    /// <summary>Execução concluída — aguardando o cliente retirar. Reservas não usadas liberadas.</summary>
    Pronta,

    /// <summary>Entregue ao cliente — pagamento recebido no mesmo ato (ver <c>Entregar</c>), que
    /// levanta o evento de domínio traduzido para <c>OsFaturada</c>. Terminal.</summary>
    Entregue,

    /// <summary>Equipamento devolvido sem reparo após reprovação (pode ter gerado taxa de
    /// diagnóstico). Terminal.</summary>
    DevolvidaSemReparo,

    /// <summary>Cancelada em qualquer ponto anterior à entrega. Terminal.</summary>
    Cancelada
}
