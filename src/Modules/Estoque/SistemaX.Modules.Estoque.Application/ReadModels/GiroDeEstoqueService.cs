using SistemaX.Modules.Estoque.Application.Ports;
using SistemaX.Modules.Estoque.Domain.Comum;
using SistemaX.Modules.Estoque.Domain.Razao;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Estoque.Application.ReadModels;

public sealed record LinhaGiro(
    string ProdutoId, string ProdutoNome, decimal GiroAnualizado, int? CoberturaDias, Money CmvNoPeriodo, Money ValorImobilizadoAtual);

/// <summary>
/// Giro = CMV do período ÷ valor imobilizado ATUAL, anualizado pela janela pedida. Cobertura em
/// dias = disponível atual ÷ consumo médio diário do período. NOTA DE ESCOPO: "estoque médio" do
/// período exigiria snapshot diário de saldo (histórico de posição) — fora do MVP; usar o valor
/// imobilizado ATUAL como aproximação é conservador o bastante para o ranking de "parados"
/// (giro baixo), que é o uso real deste read-model (R3 do plano).
/// </summary>
public sealed class GiroDeEstoqueService(IMovimentoRepository movimentos, ISaldoRepository saldos, IProdutoRepository produtos)
{
    public async Task<IReadOnlyList<LinhaGiro>> CalcularAsync(string tenantId, DateTimeOffset inicio, DateTimeOffset fim, CancellationToken ct = default)
    {
        var dias = Math.Max(1, (fim - inicio).Days);

        var saidasPorProduto = (await movimentos.ListarPorPeriodoAsync(tenantId, inicio, fim, ct))
            .Where(m => m.Tipo == TipoMovimento.Saida)
            .GroupBy(m => m.ProdutoId);

        var produtosPorId = (await produtos.ListarAsync(tenantId, ct)).ToDictionary(p => p.Id);
        var saldosPorProduto = (await saldos.ListarAsync(tenantId, ct)).ToDictionary(s => s.ProdutoId);

        var resultado = new List<LinhaGiro>();
        foreach (var grupo in saidasPorProduto)
        {
            if (!produtosPorId.TryGetValue(grupo.Key, out var produto)) continue;

            var quantidadeTotal = grupo.Aggregate(Quantidade.Zero, (acumulado, m) => acumulado + m.Quantidade);
            var cmvPeriodo = grupo.Aggregate(Money.Zero, (acumulado, m) => acumulado + CustoDoMovimento(m));

            var temSaldo = saldosPorProduto.TryGetValue(grupo.Key, out var saldo);
            var valorImobilizadoAtual = temSaldo ? saldo!.ValorTotal : Money.Zero;

            var giroAnualizado = valorImobilizadoAtual.EhPositivo
                ? Math.Round((decimal)cmvPeriodo.Centavos / valorImobilizadoAtual.Centavos * (365m / dias), 2)
                : 0m;

            var consumoMedioDiario = quantidadeTotal.EmDecimal / dias;
            var disponivelAtual = temSaldo ? saldo!.Disponivel.EmDecimal : 0m;
            int? coberturaDias = consumoMedioDiario > 0 ? (int)Math.Round(disponivelAtual / consumoMedioDiario) : null;

            resultado.Add(new LinhaGiro(produto.Id, produto.Nome, giroAnualizado, coberturaDias, cmvPeriodo, valorImobilizadoAtual));
        }

        return resultado.OrderBy(linha => linha.GiroAnualizado).ToList(); // parados primeiro — é o que o sócio quer ver
    }

    private static Money CustoDoMovimento(MovimentoDeEstoque movimento)
        => new((long)Math.Round((decimal)movimento.Quantidade.Milesimos * movimento.CustoUnitario.Centavos / 1000m, MidpointRounding.ToEven));
}
