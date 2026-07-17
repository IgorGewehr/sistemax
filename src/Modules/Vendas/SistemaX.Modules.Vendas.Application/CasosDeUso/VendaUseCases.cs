using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Vendas.Application.Ports;
using SistemaX.Modules.Vendas.Domain;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Vendas.Application.CasosDeUso;

/// <summary>Abre o carrinho no terminal. Primeira escrita da venda no repositório — a partir daqui
/// ela sobrevive a um crash/refresh do terminal (ver nota de crash-safety em <c>IVendaRepository</c>).</summary>
public sealed class IniciarVendaUseCase(IVendaRepository vendas)
{
    public async Task<Result<Venda>> ExecutarAsync(string tenantId, CancellationToken ct = default)
    {
        Venda venda;
        try
        {
            venda = Venda.Abrir(tenantId);
        }
        catch (ArgumentException ex)
        {
            return Result.Falhar<Venda>(new Error("venda.tenant_invalido", ex.Message));
        }

        await vendas.SalvarAsync(venda, ct);
        return Result.Ok(venda);
    }
}

/// <summary>
/// Monta o carrinho: itens, quantidade, desconto de item/venda e registro de pagamento — tudo
/// "busca → chama o método do agregado → salva", o mesmo padrão de
/// <c>CancelarAssinaturaUseCase</c>/<c>PausarReativarAssinaturaUseCase</c> do Financeiro. Nenhum
/// destes métodos levanta evento de integração (só <see cref="ConcluirVendaUseCase"/> e
/// <see cref="EstornarVendaUseCase"/> o fazem) — não há nada pra publicar aqui.
/// </summary>
public sealed class MontarVendaUseCase(IVendaRepository vendas)
{
    public Task<Result> AdicionarItemAsync(
        string vendaId, string produtoId, string descricao, int quantidade, Money precoUnitario, CancellationToken ct = default)
        => MutarAsync(vendaId, venda => venda.AdicionarItem(produtoId, descricao, quantidade, precoUnitario), ct);

    public Task<Result> RemoverItemAsync(string vendaId, string itemId, CancellationToken ct = default)
        => MutarAsync(vendaId, venda => venda.RemoverItem(itemId), ct);

    public Task<Result> AlterarQuantidadeItemAsync(string vendaId, string itemId, int novaQuantidade, CancellationToken ct = default)
        => MutarAsync(vendaId, venda => venda.AlterarQuantidadeItem(itemId, novaQuantidade), ct);

    public Task<Result> AplicarDescontoItemAsync(string vendaId, string itemId, Money desconto, CancellationToken ct = default)
        => MutarAsync(vendaId, venda => venda.AplicarDescontoItem(itemId, desconto), ct);

    public Task<Result> AplicarDescontoVendaAsync(string vendaId, Money desconto, string? motivo = null, CancellationToken ct = default)
        => MutarAsync(vendaId, venda => venda.AplicarDescontoVenda(desconto, motivo), ct);

    /// <summary>Vincula (ou remove, com <c>clienteId: null</c>) o cliente do carrinho — companion
    /// dimensional da F0 do plano de inteligência do Financeiro (ver <see cref="Venda.ClienteId"/>).</summary>
    public Task<Result> DefinirClienteAsync(string vendaId, string? clienteId, CancellationToken ct = default)
        => MutarAsync(vendaId, venda => venda.DefinirCliente(clienteId), ct);

    public Task<Result> RegistrarPagamentoAsync(
        string vendaId, MetodoPagamento metodo, Money valor, Money? valorRecebido, DateTimeOffset registradoEm, CancellationToken ct = default)
        => MutarAsync(vendaId, venda => venda.RegistrarPagamento(metodo, valor, valorRecebido, registradoEm), ct);

    public Task<Result> RemoverPagamentoAsync(string vendaId, string pagamentoId, CancellationToken ct = default)
        => MutarAsync(vendaId, venda => venda.RemoverPagamento(pagamentoId), ct);

    private async Task<Result> MutarAsync(string vendaId, Func<Venda, Result> acao, CancellationToken ct)
    {
        var venda = await vendas.ObterPorIdAsync(vendaId, ct);
        if (venda is null)
            return Result.Falhar(new Error("venda.nao_encontrada", $"Venda '{vendaId}' não encontrada."));

        var resultado = acao(venda);
        if (resultado.Falha) return resultado;

        await vendas.SalvarAsync(venda, ct);
        return Result.Ok();
    }
}

/// <summary>
/// Finaliza a venda e publica <c>VendaConcluida</c> + <c>VendaItensMovimentados</c> (companion com
/// o detalhe por item — fecha o gap documentado em VendaDomainEvents.cs: Estoque e Fiscal já têm
/// handler pronto e testado para este evento, só faltava Vendas publicá-lo) — a ÚNICA sequência que
/// importa está na ORDEM das linhas abaixo (regra dura R3/docs/arquitetura): primeiro persiste o
/// commit local, SÓ DEPOIS publica no barramento de integração, os dois eventos lado a lado a
/// partir do MESMO fato. Nunca o inverso — ver VendaDomainEvents.cs e
/// docs/arquitetura/COMO-CRIAR-UM-MODULO.md passo 5.
/// </summary>
public sealed class ConcluirVendaUseCase(IVendaRepository vendas, IIntegrationEventBus barramentoDeEventos)
{
    public async Task<Result<Venda>> ExecutarAsync(string vendaId, CancellationToken ct = default)
    {
        var venda = await vendas.ObterPorIdAsync(vendaId, ct);
        if (venda is null)
            return Result.Falhar<Venda>(new Error("venda.nao_encontrada", $"Venda '{vendaId}' não encontrada."));

        var resultado = venda.Concluir();
        if (resultado.Falha)
            return Result.Falhar<Venda>(resultado.Erro);

        await vendas.SalvarAsync(venda, ct); // commit local confirmado

        foreach (var evento in venda.DomainEvents.OfType<VendaConcluidaDomainEvent>())
        {
            await barramentoDeEventos.PublishAsync(evento.ParaEventoDeIntegracao(), ct);
            await barramentoDeEventos.PublishAsync(evento.ParaVendaItensMovimentados(), ct);
        }

        venda.ClearDomainEvents();
        return Result.Ok(venda);
    }
}

/// <summary>Estorna e publica <c>VendaEstornada</c> — mesma ordem commit-depois-publica de
/// <see cref="ConcluirVendaUseCase"/>. O cancelamento fiscal (NFC-e, prazo de 30min) e a decisão
/// de reservar/devolver estoque são responsabilidade de outros assinantes deste mesmo evento —
/// Vendas só constata o fato.</summary>
public sealed class EstornarVendaUseCase(IVendaRepository vendas, IIntegrationEventBus barramentoDeEventos)
{
    public async Task<Result<Venda>> ExecutarAsync(string vendaId, CancellationToken ct = default)
    {
        var venda = await vendas.ObterPorIdAsync(vendaId, ct);
        if (venda is null)
            return Result.Falhar<Venda>(new Error("venda.nao_encontrada", $"Venda '{vendaId}' não encontrada."));

        var resultado = venda.Estornar();
        if (resultado.Falha)
            return Result.Falhar<Venda>(resultado.Erro);

        await vendas.SalvarAsync(venda, ct);

        foreach (var evento in venda.DomainEvents.OfType<VendaEstornadaDomainEvent>())
            await barramentoDeEventos.PublishAsync(evento.ParaEventoDeIntegracao(), ct);

        venda.ClearDomainEvents();
        return Result.Ok(venda);
    }
}
