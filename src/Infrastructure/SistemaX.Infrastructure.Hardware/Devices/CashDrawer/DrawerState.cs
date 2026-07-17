namespace SistemaX.Infrastructure.Hardware.Devices.CashDrawer;

/// <summary>
/// A maioria das gavetas de dinheiro NÃO reporta seu estado físico real — só recebem o pulso de
/// abertura, sem sensor. O Supermarket-OS inferia "aberta/fechada" a partir do último comando
/// enviado (docs/robustez §5, fraqueza 4), o que é enganoso: a gaveta pode estar fisicamente
/// aberta manualmente por um operador sem nenhum comando ter sido enviado. Este enum é honesto
/// sobre o NÍVEL DE CONFIANÇA da informação — nunca finge certeza que o hardware não garante.
/// </summary>
public enum DrawerState
{
    /// <summary>Nenhum comando enviado ainda nesta sessão — não há base pra inferir nada.</summary>
    Desconhecido,

    /// <summary>Inferido a partir do último comando enviado — NÃO é leitura de sensor real.</summary>
    InferidoAberta,

    /// <summary>Confirmado por sensor físico (gaveta com porta DK) — alta confiança.</summary>
    ConfirmadoPorSensorAberta,

    ConfirmadoPorSensorFechada
}
