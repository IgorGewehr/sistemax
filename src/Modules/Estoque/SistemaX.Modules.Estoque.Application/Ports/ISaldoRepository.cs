using SistemaX.Modules.Estoque.Domain.Saldos;

namespace SistemaX.Modules.Estoque.Application.Ports;

/// <summary>Port do read-model persistido de saldo (produto × depósito).</summary>
public interface ISaldoRepository
{
    Task<SaldoDeItem?> ObterAsync(string tenantId, string produtoId, string depositoId, CancellationToken ct = default);

    /// <summary>Atalho usado por todo handler: primeiro movimento de um produto ainda não tem
    /// <see cref="SaldoDeItem"/> — retorna <see cref="SaldoDeItem.Vazio"/> nesse caso, nunca null.</summary>
    Task<SaldoDeItem> ObterOuCriarAsync(string tenantId, string produtoId, string depositoId, CancellationToken ct = default);

    Task SalvarAsync(SaldoDeItem saldo, CancellationToken ct = default);

    Task<IReadOnlyList<SaldoDeItem>> ListarAsync(string tenantId, CancellationToken ct = default);
}
