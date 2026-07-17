namespace SistemaX.Modules.Vendas.Domain;

/// <summary>
/// Métodos de pagamento aceitos numa <see cref="PagamentoDeVenda"/>. Lista aberta o suficiente
/// para cobrir o MVP do PDV (dinheiro, débito, crédito, PIX) e casos de loja (voucher, crédito de
/// loja/gift card) sem forçar um "outro" genérico demais — ver plano de arquitetura do PDV,
/// §8.3. TEF integrado (NSU/bandeira/parcelas) e PIX dinâmico (QR/webhook) são portas de
/// Infrastructure (<c>ITefAdapter</c>/<c>IPixProvider</c>) que não vazam pra este enum.
/// </summary>
public enum MetodoPagamento
{
    Dinheiro,
    Debito,
    Credito,
    Pix,
    Voucher,
    CreditoLoja,
    Outro
}
