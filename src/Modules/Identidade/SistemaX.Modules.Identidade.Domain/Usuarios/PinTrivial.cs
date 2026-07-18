namespace SistemaX.Modules.Identidade.Domain.Usuarios;

/// <summary>
/// Rejeita PINs fáceis de adivinhar — dígitos todos iguais (0000, 1111…) ou sequência de 1 em 1,
/// crescente ou decrescente (1234, 4321, 0123…). Esta é a FONTE DA VERDADE da regra: o wizard no
/// web também valida, mas o cliente é bypassável, então o servidor precisa recusar por conta
/// própria (ver <c>TrocarPinUseCase</c>). NÃO se aplica ao PIN provisório "1234" do founder
/// semeado — esse é criado direto (sem passar por troca), e é exatamente dele que o wizard de
/// 1º-boot obriga o operador a sair.
/// </summary>
public static class PinTrivial
{
    public static bool EhTrivial(string pin)
    {
        if (string.IsNullOrWhiteSpace(pin) || pin.Length < 4)
        {
            return true;
        }

        if (pin.All(digito => digito == pin[0]))
        {
            return true;
        }

        var crescente = true;
        var decrescente = true;
        for (var i = 1; i < pin.Length; i++)
        {
            if (pin[i] - pin[i - 1] != 1)
            {
                crescente = false;
            }

            if (pin[i] - pin[i - 1] != -1)
            {
                decrescente = false;
            }
        }

        return crescente || decrescente;
    }
}
