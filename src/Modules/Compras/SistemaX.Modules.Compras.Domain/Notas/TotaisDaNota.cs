using SistemaX.SharedKernel;

namespace SistemaX.Modules.Compras.Domain.Notas;

/// <summary>
/// Totais extraídos do <c>ICMSTot</c> da NF-e (ou digitados numa nota <c>Manual</c>). Só
/// <see cref="VProd"/> e <see cref="VNf"/> são obrigatoriamente positivos — os demais são zero por
/// padrão (nem toda nota tem frete/seguro/ST). <see cref="VNf"/> é a verdade absoluta do rateio de
/// custo de entrada (<see cref="CustoDeEntrada"/>): nunca recalculado a partir dos outros campos —
/// é exatamente o valor que o fornecedor cobrou, e a reconciliação do rateio força a igualdade.
/// </summary>
public sealed record TotaisDaNota(
    Money VProd, Money VFrete, Money VSeguro, Money VOutro, Money VDesconto, Money VSt, Money VIpi, Money VNf)
{
    public static Result<TotaisDaNota> Criar(
        long vProdCentavos, long vNfCentavos, long vFreteCentavos = 0, long vSeguroCentavos = 0,
        long vOutroCentavos = 0, long vDescontoCentavos = 0, long vStCentavos = 0, long vIpiCentavos = 0)
    {
        if (vProdCentavos <= 0)
            return Result.Falhar<TotaisDaNota>(new Error("compras.totais.vprod_invalido", "Valor total dos produtos deve ser maior que zero."));

        if (vNfCentavos <= 0)
            return Result.Falhar<TotaisDaNota>(new Error("compras.totais.vnf_invalido", "Valor total da nota deve ser maior que zero."));

        return Result.Ok(new TotaisDaNota(
            new Money(vProdCentavos), new Money(vFreteCentavos), new Money(vSeguroCentavos), new Money(vOutroCentavos),
            new Money(vDescontoCentavos), new Money(vStCentavos), new Money(vIpiCentavos), new Money(vNfCentavos)));
    }
}
