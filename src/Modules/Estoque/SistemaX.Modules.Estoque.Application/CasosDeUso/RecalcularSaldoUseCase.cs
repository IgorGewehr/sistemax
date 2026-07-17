using SistemaX.Modules.Estoque.Application.Ports;
using SistemaX.Modules.Estoque.Domain.Saldos;

namespace SistemaX.Modules.Estoque.Application.CasosDeUso;

/// <summary>
/// Manutenção: refaz <see cref="SaldoDeItem"/> do zero, por REPLAY completo do razão — nunca
/// confia no cache. Usado como botão de conserto e verificação de consistência (rodar e comparar
/// com o que está persistido: se diferir, o cache tinha bug). Ordem de replay é o próprio ULID do
/// movimento (ordenável por criação), não <c>OcorridoEm</c> — dois movimentos com o mesmo
/// <c>OcorridoEm</c> (replay de origem) ainda têm ordem total estável entre si.
/// </summary>
public sealed class RecalcularSaldoUseCase(IMovimentoRepository movimentos, ISaldoRepository saldos)
{
    public async Task<SaldoDeItem> ExecutarAsync(string tenantId, string produtoId, string depositoId, CancellationToken ct = default)
    {
        var razaoCompleto = await movimentos.ListarPorProdutoAsync(tenantId, produtoId, depositoId, ct);

        var saldo = SaldoDeItem.Vazio(tenantId, produtoId, depositoId);
        foreach (var movimento in razaoCompleto.OrderBy(m => m.Id, StringComparer.Ordinal))
            saldo.AplicarMovimento(movimento);

        await saldos.SalvarAsync(saldo, ct);
        return saldo;
    }
}
