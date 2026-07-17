using SistemaX.SharedKernel;

namespace SistemaX.Modules.Vendas.Domain;

/// <summary>
/// Uma linha de pagamento dentro de uma <see cref="Venda"/> — objeto de valor com <see cref="Id"/>
/// ESTÁVEL (não é chave de negócio cross-agregado, é só endereçamento para a UI poder remover uma
/// linha específica de um split sem depender de índice de lista, lição documentada no PDV do
/// gestao-raiz onde o índice como key de UI causava bugs de re-render).
///
/// <see cref="Valor"/> é sempre a parcela do TOTAL da venda que este pagamento cobre — nunca
/// inclui o troco. Em dinheiro, o operador pode informar <see cref="ValorRecebido"/> maior que
/// <see cref="Valor"/> (cédula de valor redondo); a diferença vira <see cref="Troco"/>, calculada,
/// nunca armazenada. Em qualquer outro método, receber mais do que o valor do pagamento não faz
/// sentido de negócio (não existe "troco de PIX") — ver <see cref="Registrar"/>.
/// </summary>
public sealed record PagamentoDeVenda
{
    public string Id { get; }
    public MetodoPagamento Metodo { get; }
    public Money Valor { get; }
    public Money? ValorRecebido { get; }
    public DateTimeOffset RegistradoEm { get; }

    public Money Troco => Metodo == MetodoPagamento.Dinheiro && ValorRecebido is { } recebido
        ? recebido - Valor
        : Money.Zero;

    private PagamentoDeVenda(string id, MetodoPagamento metodo, Money valor, Money? valorRecebido, DateTimeOffset registradoEm)
    {
        Id = id;
        Metodo = metodo;
        Valor = valor;
        ValorRecebido = valorRecebido;
        RegistradoEm = registradoEm;
    }

    public static PagamentoDeVenda Reconstituir(string id, MetodoPagamento metodo, Money valor, Money? valorRecebido, DateTimeOffset registradoEm)
        => new(id, metodo, valor, valorRecebido, registradoEm);

    public static Result<PagamentoDeVenda> Registrar(
        MetodoPagamento metodo, Money valor, Money? valorRecebido, DateTimeOffset registradoEm)
    {
        if (!valor.EhPositivo)
            return Result.Falhar<PagamentoDeVenda>(new Error(
                "venda.pagamento.valor_invalido", "Valor do pagamento deve ser positivo."));

        if (valorRecebido is { } recebido)
        {
            if (metodo != MetodoPagamento.Dinheiro)
                return Result.Falhar<PagamentoDeVenda>(new Error(
                    "venda.pagamento.troco_apenas_dinheiro",
                    "Só é possível informar valor recebido (com troco) em pagamentos em dinheiro."));

            if (recebido.Centavos < valor.Centavos)
                return Result.Falhar<PagamentoDeVenda>(new Error(
                    "venda.pagamento.recebido_insuficiente",
                    "Valor recebido não pode ser menor que o valor do pagamento."));
        }

        return Result.Ok(new PagamentoDeVenda(Ulid.NewUlid().ToString(), metodo, valor, valorRecebido, registradoEm));
    }
}
