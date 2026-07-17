using SistemaX.Modules.Financeiro.Domain.Comum;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Domain.Caixa;

/// <summary>
/// Uma linha dentro de <see cref="SessaoCaixa"/> (suprimento, sangria ou venda em espécie) — objeto
/// de valor com <see cref="Id"/> ESTÁVEL, mesmo racional de <c>PagamentoDeVenda</c> (Vendas): não é
/// chave de negócio cross-agregado, só endereçamento para a UI referenciar uma linha específica
/// (ex.: o extrato de sangrias do drill de sessão) sem depender de índice de lista.
///
/// <see cref="Motivo"/> é OBRIGATÓRIO em <see cref="TipoMovimentoCaixa.Suprimento"/>/
/// <see cref="TipoMovimentoCaixa.Sangria"/> (auditoria — "sangria de R$100 pra onde e por quê" é a
/// pergunta que o Super Consultor desta tela faz, ver ConsultorInsight do mockup) e OPCIONAL em
/// <see cref="TipoMovimentoCaixa.VendaEmEspecie"/> (a origem já é o próprio tipo).
/// </summary>
public sealed record MovimentoDeSessaoCaixa
{
    public string Id { get; }
    public TipoMovimentoCaixa Tipo { get; }
    public Money Valor { get; }
    public string? Motivo { get; }
    public DateTimeOffset RegistradoEm { get; }
    public string OperadorId { get; }
    public string OperadorNome { get; }

    private MovimentoDeSessaoCaixa(
        string id, TipoMovimentoCaixa tipo, Money valor, string? motivo,
        DateTimeOffset registradoEm, string operadorId, string operadorNome)
    {
        Id = id;
        Tipo = tipo;
        Valor = valor;
        Motivo = motivo;
        RegistradoEm = registradoEm;
        OperadorId = operadorId;
        OperadorNome = operadorNome;
    }

    /// <summary>REIDRATAÇÃO a partir do banco — não valida, mesmo padrão de <c>PagamentoDeVenda.Reconstituir</c>.</summary>
    public static MovimentoDeSessaoCaixa Reconstituir(
        string id, TipoMovimentoCaixa tipo, Money valor, string? motivo,
        DateTimeOffset registradoEm, string operadorId, string operadorNome)
        => new(id, tipo, valor, motivo, registradoEm, operadorId, operadorNome);

    public static Result<MovimentoDeSessaoCaixa> Registrar(
        TipoMovimentoCaixa tipo, Money valor, string? motivo, DateTimeOffset registradoEm,
        string operadorId, string operadorNome)
    {
        if (!valor.EhPositivo)
            return Result.Falhar<MovimentoDeSessaoCaixa>(new Error(
                "financeiro.sessao_caixa.movimento.valor_invalido", "Valor do movimento de caixa deve ser positivo."));

        if (string.IsNullOrWhiteSpace(operadorId))
            return Result.Falhar<MovimentoDeSessaoCaixa>(new Error(
                "financeiro.sessao_caixa.movimento.operador_invalido", "Operador do movimento é obrigatório."));

        if ((tipo is TipoMovimentoCaixa.Suprimento or TipoMovimentoCaixa.Sangria) && string.IsNullOrWhiteSpace(motivo))
            return Result.Falhar<MovimentoDeSessaoCaixa>(new Error(
                "financeiro.sessao_caixa.movimento.motivo_obrigatorio",
                $"Motivo é obrigatório para movimento do tipo '{tipo}'."));

        return Result.Ok(new MovimentoDeSessaoCaixa(
            IdGenerator.NovoId(), tipo, valor, string.IsNullOrWhiteSpace(motivo) ? null : motivo,
            registradoEm, operadorId, operadorNome));
    }
}
