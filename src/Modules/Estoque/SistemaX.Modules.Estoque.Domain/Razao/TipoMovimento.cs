namespace SistemaX.Modules.Estoque.Domain.Razao;

/// <summary>
/// Tipo do movimento — define o EFEITO sobre físico/reservado (ver
/// <see cref="MovimentoDeEstoque.EfeitoFisico"/>/<see cref="MovimentoDeEstoque.EfeitoReservado"/>).
/// Só <see cref="Ajuste"/> carrega delta assinado; todos os outros guardam
/// <see cref="MovimentoDeEstoque.Quantidade"/> sempre positiva — o sentido vem do tipo, nunca do
/// sinal armazenado (isso é o que permite ordenar/filtrar o razão sem ambiguidade).
/// </summary>
public enum TipoMovimento
{
    /// <summary>+físico. Compra, estorno de venda, estorno de consumo de OS.</summary>
    Entrada,

    /// <summary>−físico. Venda, consumo de OS, transferência-saída.</summary>
    Saida,

    /// <summary>−físico. Quebra/validade/furto — motivo obrigatório.</summary>
    Perda,

    /// <summary>±físico (delta assinado — único tipo em que <c>Quantidade.Milesimos</c> pode ser
    /// negativo). Inventário/correção manual.</summary>
    Ajuste,

    /// <summary>+reservado. OS aprovada — físico intacto.</summary>
    Reserva,

    /// <summary>−reservado. Peça aplicada consome a reserva; sobra ou cancelamento libera.</summary>
    LiberacaoReserva
}
