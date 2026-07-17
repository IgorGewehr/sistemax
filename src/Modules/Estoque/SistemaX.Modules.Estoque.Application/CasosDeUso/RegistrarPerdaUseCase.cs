using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Estoque.Application.Comum;
using SistemaX.Modules.Estoque.Application.Ports;
using SistemaX.Modules.Estoque.Domain.Comum;
using SistemaX.Modules.Estoque.Domain.Razao;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Estoque.Application.CasosDeUso;

/// <summary>Perda manual (quebra/validade/furto/outro) — motivo é obrigatório por regra de
/// domínio (ver <c>MovimentoDeEstoque.Registrar</c>). Publica <c>PerdaRegistrada</c> com o custo
/// total pelo custo médio vigente, para o Financeiro lançar no DRE.</summary>
public sealed class RegistrarPerdaUseCase(IProdutoRepository produtos, IMovimentoRepository movimentos, ISaldoRepository saldos, IIntegrationEventBus bus)
{
    public async Task<Result<MovimentoDeEstoque>> ExecutarAsync(
        string tenantId, string produtoId, Quantidade quantidade, string motivo,
        string operadorId, string operadorNome, DateTimeOffset ocorridoEm, CancellationToken ct = default)
    {
        var produto = await produtos.ObterPorIdAsync(produtoId, ct);
        if (produto is null)
            return Result.Falhar<MovimentoDeEstoque>(new Error("estoque.produto.nao_encontrado", $"Produto '{produtoId}' não encontrado."));

        var movimentoId = IdGenerator.NovoId();
        var saldo = await saldos.ObterOuCriarAsync(tenantId, produtoId, EstoqueConstantes.DepositoPadrao, ct);

        var movimentoResultado = MovimentoDeEstoque.Registrar(
            tenantId, EstoqueConstantes.DepositoPadrao, produtoId, TipoMovimento.Perda, quantidade, saldo.CustoMedio,
            new SourceRef("manual", movimentoId), $"manual:{movimentoId}", motivo, operadorId, operadorNome, ocorridoEm);

        if (movimentoResultado.Falha) return movimentoResultado;

        var movimento = movimentoResultado.Valor;
        saldo.AplicarMovimento(movimento);

        await movimentos.SalvarAsync(movimento, ct);
        await saldos.SalvarAsync(saldo, ct);

        var custoTotalCentavos = (long)Math.Round((decimal)quantidade.Milesimos * saldo.CustoMedio.Centavos / 1000m, MidpointRounding.ToEven);
        await bus.PublishAsync(new PerdaRegistrada(movimento.Id, tenantId, produtoId, quantidade.Milesimos, custoTotalCentavos, motivo, ocorridoEm), ct);

        return movimentoResultado;
    }
}
