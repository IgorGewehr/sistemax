using SistemaX.SharedKernel;

namespace SistemaX.Modules.Compras.Domain.Comum;

/// <summary>
/// Chave de acesso da NF-e/NFC-e/CT-e — 44 dígitos. É a base do DEDUPE estrutural do módulo (§3.3
/// invariante 6 do plano): 1 nota por chave por tenant; reimportar o mesmo XML nunca duplica, só
/// abre a nota já existente. Nota <c>Manual</c> (compra sem NF-e — pequeno varejo/produtor rural)
/// não tem chave; é a ÚNICA origem que dispensa este VO (ver <c>NotaDeCompra.Importar</c>).
/// </summary>
public readonly record struct ChaveDeAcesso
{
    public string Valor { get; }

    private ChaveDeAcesso(string valor) => Valor = valor;

    public static Result<ChaveDeAcesso> Criar(string valorBruto)
    {
        if (string.IsNullOrWhiteSpace(valorBruto))
            return Result.Falhar<ChaveDeAcesso>(new Error("compras.chave_acesso.vazia", "Chave de acesso não pode ser vazia."));

        var somenteDigitos = new string(valorBruto.Where(char.IsDigit).ToArray());
        if (somenteDigitos.Length != 44)
            return Result.Falhar<ChaveDeAcesso>(new Error(
                "compras.chave_acesso.invalida",
                $"Chave de acesso deve ter 44 dígitos (recebido {somenteDigitos.Length})."));

        return Result.Ok(new ChaveDeAcesso(somenteDigitos));
    }

    public override string ToString() => Valor;
}
