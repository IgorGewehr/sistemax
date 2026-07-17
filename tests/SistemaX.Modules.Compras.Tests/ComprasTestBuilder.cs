using SistemaX.Modules.Compras.Application.CasosDeUso;
using SistemaX.Modules.Compras.Domain.Comum;
using SistemaX.Modules.Compras.Domain.Notas;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Compras.Tests;

/// <summary>Atalhos comuns aos testes de Compras — evita repetir a montagem de item/nota em toda
/// classe de teste (mesmo papel de <c>VendaTestBuilder</c> no módulo Vendas).</summary>
internal static class ComprasTestBuilder
{
    public const string TenantId = "tenant-1";
    public const string LojaId = "loja-1";
    public const string FornecedorId = "fornecedor-1";

    /// <summary>Item de domínio pronto para uso direto no agregado (bypassa o caso de uso de
    /// entrada) — útil para testar FSM/invariantes de <see cref="NotaDeCompra"/> isoladamente.</summary>
    public static ItemDeNotaDeCompra Item(
        int nItem, long vProdCentavos, string? produtoId = "produto-1", long qtdMilesimos = 1000,
        long? fatorMilesimos = 1000, MatchState matchState = MatchState.Manual,
        long vDescCentavos = 0, Money? vFreteItem = null, Money? vSegItem = null, Money? vOutroItem = null,
        long vIpiCentavos = 0, long vIcmsStCentavos = 0)
    {
        var resultado = ItemDeNotaDeCompra.Criar(
            nItem, $"CPROD-{nItem}", $"Item {nItem}", "1234.56.78", "UN", new Quantidade(qtdMilesimos),
            new Money(vProdCentavos), new Money(vDescCentavos), vFreteItem, vSegItem, vOutroItem,
            new Money(vIpiCentavos), new Money(vIcmsStCentavos), matchState, produtoId, fatorMilesimos);

        if (resultado.Falha)
            throw new InvalidOperationException($"Falha ao montar item de teste: {resultado.Erro.Mensagem}");

        return resultado.Valor;
    }

    /// <summary>Nota EmConferencia com 2 itens resolvidos (Manual) — pronta para
    /// <see cref="NotaDeCompra.ConfirmarRecebimento"/> sem passos extras.</summary>
    public static NotaDeCompra NotaEmConferencia(
        long vProd1Centavos = 10_000, long vProd2Centavos = 5_000, long vFreteCentavos = 0, long vDescontoCentavos = 0,
        string? fornecedorId = FornecedorId)
    {
        var vNf = vProd1Centavos + vProd2Centavos + vFreteCentavos - vDescontoCentavos;
        var totais = TotaisDaNota.Criar(
            vProd1Centavos + vProd2Centavos, vNf, vFreteCentavos, vDescontoCentavos: vDescontoCentavos).Valor;

        var itens = new[]
        {
            Item(1, vProd1Centavos, "produto-1"),
            Item(2, vProd2Centavos, "produto-2")
        };

        var nota = NotaDeCompra.Importar(
            TenantId, LojaId, OrigemNota.Manual, "1001", "1", DateTimeOffset.UtcNow, totais, itens, fornecedorId).Valor;

        nota.AbrirConferencia();
        return nota;
    }

    /// <summary>Input de <see cref="RegistrarEntradaDeNotaUseCase"/> — chave de acesso com 44
    /// dígitos determinística por <paramref name="numero"/> (suficiente para dedupe único por teste).</summary>
    public static EntradaDeNotaInput EntradaComItemConhecido(
        string numero = "1001", string? cProd = "CPROD-1", string? produtoIdConhecido = "produto-1",
        long vProdCentavos = 10_000, long vFreteCentavos = 0, string fornecedorId = FornecedorId)
    {
        var vNf = vProdCentavos + vFreteCentavos;
        return new EntradaDeNotaInput(
            TenantId, LojaId, OrigemNota.XmlUpload, numero, "1", DateTimeOffset.UtcNow, fornecedorId,
            ChaveDeAcesso(numero), vProdCentavos, vNf,
            [
                new ItemDeEntradaInput(
                    1, cProd, "Item de teste", "1234.56.78", "UN", 1000, vProdCentavos,
                    ProdutoIdConhecido: produtoIdConhecido, FatorConversaoConhecidoMilesimos: produtoIdConhecido is null ? null : 1000)
            ],
            VFreteCentavos: vFreteCentavos);
    }

    /// <summary>44 dígitos determinísticos a partir do número da nota — só para satisfazer o VO
    /// <see cref="Comum.ChaveDeAcesso"/>, sem pretensão de ser uma chave de NF-e real.</summary>
    public static string ChaveDeAcesso(string numero) => numero.PadLeft(44, '4');
}
