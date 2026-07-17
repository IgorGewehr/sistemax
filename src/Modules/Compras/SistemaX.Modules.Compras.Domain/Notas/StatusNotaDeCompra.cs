namespace SistemaX.Modules.Compras.Domain.Notas;

/// <summary>
/// FSM de <see cref="NotaDeCompra"/> (plano §3.2):
///
/// <code>
///   Importada ──AbrirConferencia()──► EmConferencia ──ConfirmarRecebimento()──► Recebida
///       │                                    │                                     │
///       └──────────Descartar()───────────────┘                  Estornar()◄────────┘
///                                        (volta p/ EmConferencia)
/// </code>
///
/// <see cref="Descartada"/> é terminal (saída própria/devolução classificada errado, upload por
/// engano) — a chave de acesso permanece registrada para o dedupe continuar funcionando (nunca
/// reimporta a mesma nota descartada como se fosse nova). Ver
/// <see cref="NotaDeCompra.TransicoesPermitidas"/>.
/// </summary>
public enum StatusNotaDeCompra
{
    Importada,
    EmConferencia,
    Recebida,
    Descartada
}
