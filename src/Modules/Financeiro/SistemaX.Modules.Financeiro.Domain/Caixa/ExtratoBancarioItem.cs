using SistemaX.Modules.Financeiro.Domain.Comum;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Domain.Caixa;

/// <summary>Linha importada de OFX/CSV do banco — candidata a conciliar contra um <c>MovimentoFinanceiro</c>.</summary>
public sealed class ExtratoBancarioItem : Entity<string>
{
    public string BusinessId { get; }
    public string ContaBancariaCaixaId { get; }
    public DateTimeOffset Data { get; }
    public Money Valor { get; }
    public string Descricao { get; }

    /// <summary>Identificador único do item no extrato de origem — dedupe de reimportação do mesmo OFX.</summary>
    public string IdentificadorExterno { get; }

    private ExtratoBancarioItem(string id, string businessId, string contaBancariaCaixaId, DateTimeOffset data, Money valor, string descricao, string identificadorExterno)
    {
        Id = id;
        BusinessId = businessId;
        ContaBancariaCaixaId = contaBancariaCaixaId;
        Data = data;
        Valor = valor;
        Descricao = descricao;
        IdentificadorExterno = identificadorExterno;
    }

    public static ExtratoBancarioItem Importar(string businessId, string contaBancariaCaixaId, DateTimeOffset data, Money valor, string descricao, string identificadorExterno)
        => new(IdGenerator.NovoId(), businessId, contaBancariaCaixaId, data, valor, descricao, identificadorExterno);

    /// <summary>REIDRATAÇÃO a partir do banco — não valida, não levanta evento.</summary>
    public static ExtratoBancarioItem Reconstituir(
        string id, string businessId, string contaBancariaCaixaId, DateTimeOffset data, Money valor,
        string descricao, string identificadorExterno)
        => new(id, businessId, contaBancariaCaixaId, data, valor, descricao, identificadorExterno);
}
