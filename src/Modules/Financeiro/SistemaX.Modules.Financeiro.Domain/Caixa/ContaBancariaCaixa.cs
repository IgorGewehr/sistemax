using SistemaX.Modules.Financeiro.Domain.Comum;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Domain.Caixa;

/// <summary>
/// Onde o dinheiro fisicamente está (conta corrente, caixa físico, carteira digital) — o LAR ÚNICO
/// do id que <see cref="MovimentoFinanceiro.ContaBancariaCaixaId"/> e
/// <see cref="ExtratoBancarioItem.ContaBancariaCaixaId"/> já referenciavam como string opaca desde
/// a F0, sem repositório próprio até esta reconciliação (docs/wiring/financeiro-telas-restantes.md
/// §3) — a tela Bancário rodava sobre UMA conta hardcoded
/// (<c>ClassificadorFormaPagamento.ContaCaixaPadraoId</c>). O SALDO ATUAL É DERIVADO —
/// <see cref="SaldoInicial"/> + soma de <c>MovimentoFinanceiro</c> — nunca um campo armazenado
/// aqui, exatamente para evitar drift entre saldo guardado e histórico real
/// (docs/financeiro-datamodel.md §2.2). Ver <c>IMovimentoFinanceiroRepository.CalcularSaldoAsync</c>
/// / <c>ContasBancariasService</c>.
/// </summary>
public sealed class ContaBancariaCaixa : Entity<string>
{
    public string BusinessId { get; private set; } = string.Empty;
    public string Nome { get; private set; } = string.Empty;
    public TipoContaBancariaCaixa Tipo { get; private set; }

    /// <summary>Saldo no momento em que a conta foi CADASTRADA no sistema — ponto de partida da
    /// soma de movimentos, nunca atualizado depois (o saldo atual é sempre derivado).</summary>
    public Money SaldoInicial { get; private set; }

    public bool Ativa { get; private set; }

    public DateTimeOffset CriadoEm { get; private set; }

    public DateTimeOffset AtualizadoEm { get; private set; }

    private ContaBancariaCaixa()
    {
    }

    /// <summary>
    /// <paramref name="id"/> é opcional — só existe para a semente idempotente
    /// (<c>FinanceiroBootstrapSeeder</c>) poder cadastrar a conta-caixa padrão com o MESMO id que
    /// <c>ClassificadorFormaPagamento.ContaCaixaPadraoId</c> já usava como referência hardcoded em
    /// <c>MovimentoFinanceiro</c> — sem isso, o saldo derivado da conta padrão nunca bateria com o
    /// ledger já escrito pelos handlers de <c>VendaConcluida</c>/<c>PedidoPago</c>. Qualquer conta
    /// nova cadastrada por um caso de uso normal não passa <paramref name="id"/> (ULID gerado).
    /// </summary>
    public static Result<ContaBancariaCaixa> Criar(
        string businessId, string nome, TipoContaBancariaCaixa tipo, Money? saldoInicial = null, string? id = null)
    {
        if (string.IsNullOrWhiteSpace(businessId))
            return Result.Falhar<ContaBancariaCaixa>(new Error("financeiro.conta_bancaria_caixa.business_invalido", "BusinessId é obrigatório."));

        if (string.IsNullOrWhiteSpace(nome))
            return Result.Falhar<ContaBancariaCaixa>(new Error("financeiro.conta_bancaria_caixa.nome_invalido", "Nome/apelido da conta é obrigatório."));

        var agora = DateTimeOffset.UtcNow;
        return Result.Ok(new ContaBancariaCaixa
        {
            Id = string.IsNullOrWhiteSpace(id) ? IdGenerator.NovoId() : id,
            BusinessId = businessId,
            Nome = nome,
            Tipo = tipo,
            SaldoInicial = saldoInicial ?? Money.Zero,
            Ativa = true,
            CriadoEm = agora,
            AtualizadoEm = agora
        });
    }

    /// <summary>REIDRATAÇÃO a partir do banco — não valida (mesmo padrão de
    /// <c>Fornecedor.Reconstituir</c>).</summary>
    public static ContaBancariaCaixa Reconstituir(
        string id, string businessId, string nome, TipoContaBancariaCaixa tipo, Money saldoInicial,
        bool ativa, DateTimeOffset criadoEm, DateTimeOffset atualizadoEm)
        => new()
        {
            Id = id,
            BusinessId = businessId,
            Nome = nome,
            Tipo = tipo,
            SaldoInicial = saldoInicial,
            Ativa = ativa,
            CriadoEm = criadoEm,
            AtualizadoEm = atualizadoEm
        };

    public Result Desativar()
    {
        if (!Ativa)
            return Result.Falhar(new Error("financeiro.conta_bancaria_caixa.ja_inativa", "Conta já está inativa."));

        Ativa = false;
        AtualizadoEm = DateTimeOffset.UtcNow;
        return Result.Ok();
    }

    public Result Reativar()
    {
        if (Ativa)
            return Result.Falhar(new Error("financeiro.conta_bancaria_caixa.ja_ativa", "Conta já está ativa."));

        Ativa = true;
        AtualizadoEm = DateTimeOffset.UtcNow;
        return Result.Ok();
    }
}
