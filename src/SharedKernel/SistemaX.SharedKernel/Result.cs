namespace SistemaX.SharedKernel;

/// <summary>Erro de domínio/aplicação com código estável (para i18n e logs) e mensagem.</summary>
public sealed record Error(string Codigo, string Mensagem)
{
    public static readonly Error Nenhum = new(string.Empty, string.Empty);
}

/// <summary>
/// Resultado explícito de uma operação. Preferimos Result a lançar exceção para
/// regras de negócio esperadas (saldo insuficiente, transição inválida). Exceção fica
/// para o inesperado (bug, falha de infra).
/// </summary>
public class Result
{
    public bool Sucesso { get; }
    public bool Falha => !Sucesso;
    public Error Erro { get; }

    protected Result(bool sucesso, Error erro) => (Sucesso, Erro) = (sucesso, erro);

    public static Result Ok() => new(true, Error.Nenhum);
    public static Result Falhar(Error erro) => new(false, erro);
    public static Result<T> Ok<T>(T valor) => new(valor, true, Error.Nenhum);
    public static Result<T> Falhar<T>(Error erro) => new(default!, false, erro);
}

public sealed class Result<T> : Result
{
    private readonly T _valor;

    internal Result(T valor, bool sucesso, Error erro) : base(sucesso, erro) => _valor = valor;

    public T Valor => Sucesso
        ? _valor
        : throw new InvalidOperationException("Não há valor num Result de falha. Cheque .Sucesso antes.");
}
