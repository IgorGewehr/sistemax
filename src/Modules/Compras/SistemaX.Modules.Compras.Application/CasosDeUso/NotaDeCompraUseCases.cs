using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Compras.Application.Ports;
using SistemaX.Modules.Compras.Domain.Comum;
using SistemaX.Modules.Compras.Domain.Notas;
using SistemaX.Modules.Compras.Domain.Vinculos;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Compras.Application.CasosDeUso;

/// <summary>
/// Passos 1-7 do pipeline de importação (plano §4): recebe o fato já parseado
/// (<see cref="EntradaDeNotaInput"/>), dedupa por chave de acesso, roda o motor de match em
/// cascata sobre cada item e persiste a nota <see cref="StatusNotaDeCompra.EmConferencia"/>.
/// Não publica nenhum evento de integração — isso só acontece em
/// <see cref="ConfirmarRecebimentoUseCase"/> (passo 8/9), quando o recebimento é de fato confirmado.
/// </summary>
public sealed class RegistrarEntradaDeNotaUseCase(INotaDeCompraRepository notas, IVinculoProdutoFornecedorRepository vinculos)
{
    public async Task<Result<NotaDeCompra>> ExecutarAsync(EntradaDeNotaInput input, CancellationToken ct = default)
    {
        ChaveDeAcesso? chave = null;
        if (!string.IsNullOrWhiteSpace(input.ChaveDeAcessoBruta))
        {
            var chaveResultado = Domain.Comum.ChaveDeAcesso.Criar(input.ChaveDeAcessoBruta);
            if (chaveResultado.Falha) return Result.Falhar<NotaDeCompra>(chaveResultado.Erro);
            chave = chaveResultado.Valor;

            // Passo 3 do pipeline — dedupe estrutural: reimportar o mesmo XML abre a nota já
            // existente, nunca duplica e nunca erra pro usuário (plano §3.3 invariante 6).
            var existente = await notas.ObterPorChaveDeAcessoAsync(input.TenantId, chave.Value.Valor, ct);
            if (existente is not null) return Result.Ok(existente);
        }

        var totaisResultado = TotaisDaNota.Criar(
            input.VProdCentavos, input.VNfCentavos, input.VFreteCentavos, input.VSeguroCentavos,
            input.VOutroCentavos, input.VDescontoCentavos, input.VStCentavos, input.VIpiCentavos);
        if (totaisResultado.Falha) return Result.Falhar<NotaDeCompra>(totaisResultado.Erro);

        var itens = new List<ItemDeNotaDeCompra>(input.Itens.Count);
        foreach (var itemInput in input.Itens)
        {
            var (matchState, produtoId, fator) = await ResolverMatchCascataAsync(input.TenantId, input.FornecedorId, itemInput, vinculos, ct);

            var itemResultado = ItemDeNotaDeCompra.Criar(
                itemInput.NItem, itemInput.CProd, itemInput.DescricaoNf, itemInput.Ncm, itemInput.UnidadeNf,
                new Quantidade(itemInput.QuantidadeNfMilesimos), new Money(itemInput.VProdCentavos), new Money(itemInput.VDescCentavos),
                itemInput.VFreteItemCentavos is { } f ? new Money(f) : null,
                itemInput.VSegItemCentavos is { } s ? new Money(s) : null,
                itemInput.VOutroItemCentavos is { } o ? new Money(o) : null,
                new Money(itemInput.VIpiCentavos), new Money(itemInput.VIcmsStCentavos),
                matchState, produtoId, fator, itemInput.LoteFornecedor, itemInput.Validade);
            if (itemResultado.Falha) return Result.Falhar<NotaDeCompra>(itemResultado.Erro);

            itens.Add(itemResultado.Valor);
        }

        var notaResultado = NotaDeCompra.Importar(
            input.TenantId, input.LojaId, input.Origem, input.Numero, input.Serie, input.DataEmissao,
            totaisResultado.Valor, itens, input.FornecedorId, chave);
        if (notaResultado.Falha) return notaResultado;

        var nota = notaResultado.Valor;
        var abertura = nota.AbrirConferencia();
        if (abertura.Falha) return Result.Falhar<NotaDeCompra>(abertura.Erro); // não deveria falhar — Importada sempre pode abrir conferência

        await notas.SalvarAsync(nota, ct);
        return Result.Ok(nota);
    }

    /// <summary>Cascata de match (plano §5): (1) de-para aprendido do fornecedor → Auto, zero-touch;
    /// (2) quem chamou já informou o produto (nota Manual, UI) → Manual; senão → SemMatch, bloqueia
    /// até resolução humana em <see cref="ResolverMatchDeItemUseCase"/>.</summary>
    private static async Task<(MatchState Estado, string? ProdutoId, long? FatorMilesimos)> ResolverMatchCascataAsync(
        string tenantId, string? fornecedorId, ItemDeEntradaInput item, IVinculoProdutoFornecedorRepository vinculos, CancellationToken ct)
    {
        if (fornecedorId is not null && item.CProd is not null)
        {
            var vinculo = await vinculos.ObterAsync(tenantId, fornecedorId, item.CProd, ct);
            if (vinculo is not null)
                return (MatchState.Auto, vinculo.ProdutoId, vinculo.FatorConversaoMilesimos);
        }

        if (item.ProdutoIdConhecido is not null)
            return (MatchState.Manual, item.ProdutoIdConhecido, item.FatorConversaoConhecidoMilesimos ?? 1000L);

        return (MatchState.SemMatch, null, null);
    }
}

/// <summary>
/// Resolução humana de um item <c>Sugerido</c>/<c>SemMatch</c> — grava/atualiza o
/// <see cref="VinculoProdutoFornecedor"/> do fornecedor (plano §5 "Aprendizado"): a PRÓXIMA nota do
/// mesmo fornecedor com o mesmo <c>cProd</c> cai direto na estratégia 1 (Auto). Preserva o Id do
/// vínculo existente quando já havia um (reaprendizado nunca duplica registro).
/// </summary>
public sealed class ResolverMatchDeItemUseCase(INotaDeCompraRepository notas, IVinculoProdutoFornecedorRepository vinculos)
{
    public async Task<Result> ExecutarAsync(
        string notaId, int nItem, string produtoId, long fatorConversaoMilesimos, CancellationToken ct = default)
    {
        var nota = await notas.ObterPorIdAsync(notaId, ct);
        if (nota is null)
            return Result.Falhar(new Error("compras.nota.nao_encontrada", $"Nota '{notaId}' não encontrada."));

        var resultado = nota.ResolverMatch(nItem, produtoId, fatorConversaoMilesimos);
        if (resultado.Falha) return resultado;

        await notas.SalvarAsync(nota, ct);
        await AprenderVinculoAsync(nota.TenantId, nota.FornecedorId, nota.Itens.First(i => i.NItem == nItem).CProd, produtoId, fatorConversaoMilesimos, nota.Id, ct);

        return Result.Ok();
    }

    private async Task AprenderVinculoAsync(
        string tenantId, string? fornecedorId, string? cProd, string produtoId, long fatorConversaoMilesimos, string notaId, CancellationToken ct)
    {
        if (fornecedorId is null || cProd is null) return; // sem cProd não há chave de de-para pra aprender

        var existente = await vinculos.ObterAsync(tenantId, fornecedorId, cProd, ct);
        if (existente is not null)
        {
            existente.AtualizarMatch(produtoId, fatorConversaoMilesimos, notaId, DateTimeOffset.UtcNow);
            await vinculos.SalvarAsync(existente, ct);
            return;
        }

        var novoResultado = VinculoProdutoFornecedor.Criar(tenantId, fornecedorId, cProd, produtoId, fatorConversaoMilesimos, notaId);
        if (novoResultado.Sucesso)
            await vinculos.SalvarAsync(novoResultado.Valor, ct);
    }
}

public sealed class IgnorarItemDaNotaUseCase(INotaDeCompraRepository notas)
{
    public async Task<Result> ExecutarAsync(string notaId, int nItem, string motivo, CancellationToken ct = default)
    {
        var nota = await notas.ObterPorIdAsync(notaId, ct);
        if (nota is null)
            return Result.Falhar(new Error("compras.nota.nao_encontrada", $"Nota '{notaId}' não encontrada."));

        var resultado = nota.IgnorarItem(nItem, motivo);
        if (resultado.Falha) return resultado;

        await notas.SalvarAsync(nota, ct);
        return Result.Ok();
    }
}

/// <summary>
/// Confirma o recebimento e publica os eventos de integração — a ÚNICA sequência que importa está
/// na ORDEM das linhas abaixo (regra dura R3 do projeto): primeiro persiste o commit local, SÓ
/// DEPOIS publica no barramento. Um fato (<c>NotaDeCompraRecebidaDomainEvent</c>), DOIS eventos de
/// integração lado a lado — <c>CompraRecebida</c> (Financeiro já assina) e <c>CompraItensRecebidos</c>
/// (Estoque já assina) — mesmo desenho de <c>ConcluirVendaUseCase</c> em Vendas.Application.
/// </summary>
public sealed class ConfirmarRecebimentoUseCase(INotaDeCompraRepository notas, IIntegrationEventBus barramentoDeEventos)
{
    public async Task<Result<NotaDeCompra>> ExecutarAsync(
        string notaId, string usuarioId, string usuarioNome, DateTimeOffset agora, CancellationToken ct = default)
    {
        var nota = await notas.ObterPorIdAsync(notaId, ct);
        if (nota is null)
            return Result.Falhar<NotaDeCompra>(new Error("compras.nota.nao_encontrada", $"Nota '{notaId}' não encontrada."));

        var resultado = nota.ConfirmarRecebimento(usuarioId, usuarioNome, agora);
        if (resultado.Falha) return Result.Falhar<NotaDeCompra>(resultado.Erro);

        await notas.SalvarAsync(nota, ct); // commit local confirmado

        foreach (var evento in nota.DomainEvents.OfType<NotaDeCompraRecebidaDomainEvent>())
        {
            await barramentoDeEventos.PublishAsync(evento.ParaCompraRecebida(), ct);
            await barramentoDeEventos.PublishAsync(evento.ParaCompraItensRecebidos(), ct);
        }

        nota.ClearDomainEvents();
        return Result.Ok(nota);
    }
}

/// <summary>Estorna e publica <c>CompraEstornada</c> — mesma ordem commit-depois-publica de
/// <see cref="ConfirmarRecebimentoUseCase"/>.</summary>
public sealed class EstornarRecebimentoUseCase(INotaDeCompraRepository notas, IIntegrationEventBus barramentoDeEventos)
{
    public async Task<Result<NotaDeCompra>> ExecutarAsync(
        string notaId, string usuarioId, string usuarioNome, DateTimeOffset agora, CancellationToken ct = default)
    {
        var nota = await notas.ObterPorIdAsync(notaId, ct);
        if (nota is null)
            return Result.Falhar<NotaDeCompra>(new Error("compras.nota.nao_encontrada", $"Nota '{notaId}' não encontrada."));

        var resultado = nota.Estornar(usuarioId, usuarioNome, agora);
        if (resultado.Falha) return Result.Falhar<NotaDeCompra>(resultado.Erro);

        await notas.SalvarAsync(nota, ct);

        foreach (var evento in nota.DomainEvents.OfType<NotaDeCompraEstornadaDomainEvent>())
            await barramentoDeEventos.PublishAsync(evento.ParaEventoDeIntegracao(), ct);

        nota.ClearDomainEvents();
        return Result.Ok(nota);
    }
}

public sealed class DescartarNotaUseCase(INotaDeCompraRepository notas)
{
    public async Task<Result> ExecutarAsync(string notaId, string motivo, CancellationToken ct = default)
    {
        var nota = await notas.ObterPorIdAsync(notaId, ct);
        if (nota is null)
            return Result.Falhar(new Error("compras.nota.nao_encontrada", $"Nota '{notaId}' não encontrada."));

        var resultado = nota.Descartar(motivo);
        if (resultado.Falha) return resultado;

        await notas.SalvarAsync(nota, ct);
        return Result.Ok();
    }
}
