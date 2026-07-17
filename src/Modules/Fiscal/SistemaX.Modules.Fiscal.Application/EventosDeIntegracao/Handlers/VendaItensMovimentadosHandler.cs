using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Fiscal.Application.CasosDeUso;
using SistemaX.Modules.Fiscal.Application.Ports;
using SistemaX.Modules.Fiscal.Domain.Comum;
using SistemaX.Modules.Fiscal.Domain.Documentos;
using SistemaX.Modules.Fiscal.Domain.Operacoes;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Fiscal.Application.EventosDeIntegracao.Handlers;

/// <summary>
/// <c>venda.itens</c> → abre um rascunho de <see cref="DocumentoFiscal"/> (NFC-e — venda de
/// balcão) e tenta resolver a tributação de cada item, delegando tudo para
/// <see cref="EmitirDocumentoFiscalUseCase"/> (idempotente por <c>SourceRef("vendas", VendaId)</c>
/// — reprocessar o mesmo evento é NO-OP, ver docs/fiscal/arquitetura.md §6).
///
/// NCM/CEST/natureza de cada produto vêm de <see cref="IDadosFiscaisProdutoCacheRepository"/> (a
/// cópia local alimentada por <c>ProdutoFiscalAtualizado</c>) — Fiscal NUNCA faz chamada síncrona
/// ao Estoque. Produto sem NCM cadastrado ainda não trava a venda: o item simplesmente falha a
/// resolução e o documento nasce <c>BloqueadoPorConfiguracaoFiscal</c>, nunca com um NCM/CFOP
/// inventado.
///
/// GAP DOCUMENTADO (mesmo padrão do resto do catálogo): sem <see cref="IConfiguracaoFiscalTenantRepository"/>
/// preenchida para o tenant (regime + UF de origem), este handler não tem como decidir qual
/// <c>RegimeTributario</c> usar — nesse caso é NO-OP silencioso (nenhum documento é aberto) até
/// que a configuração exista; fechar isso com um bloqueio explícito é trabalho de UI/Settings→Fiscal,
/// fora do escopo desta fase.
/// </summary>
public sealed class VendaItensMovimentadosHandler(
    IDocumentoFiscalRepository documentos,
    IConfiguracaoFiscalTenantRepository configuracoes,
    IDadosFiscaisProdutoCacheRepository cacheDeProdutos,
    EmitirDocumentoFiscalUseCase emitir) : IIntegrationEventHandler<VendaItensMovimentados>
{
    public async Task HandleAsync(VendaItensMovimentados evento, CancellationToken ct = default)
    {
        var origem = new SourceRef("vendas", evento.VendaId);
        if (await documentos.ObterPorOrigemAsync(evento.TenantId, origem.Chave, ct) is not null)
            return; // replay do mesmo evento — idempotência

        var configuracao = await configuracoes.ObterAsync(evento.TenantId, ct);
        if (configuracao is null)
            return; // sem regime/UF configurados para o tenant — nada a fazer ainda

        var operacao = new OperacaoFiscal(
            Tipo: TipoOperacaoFiscal.VendaMercadoria,
            UfOrigem: configuracao.UfOrigem,
            UfDestino: configuracao.UfOrigem,
            DestinatarioConsumidorFinal: true,
            DestinatarioContribuinteIcms: false,
            OperacaoPresencial: true);

        var itens = new List<ItemParaEmitir>(evento.Itens.Count);
        foreach (var linha in evento.Itens)
        {
            var cache = await cacheDeProdutos.ObterAsync(evento.TenantId, linha.ProdutoId, ct);
            itens.Add(new ItemParaEmitir(
                linha.ProdutoId, linha.Descricao, Ncm: cache?.Ncm ?? string.Empty,
                new Quantidade(linha.QuantidadeMilesimos), new Money(linha.PrecoUnitarioCentavos),
                new Money(linha.DescontoCentavos)));
        }

        await emitir.ExecutarAsync(
            evento.TenantId, TipoDocumentoFiscal.NFCe, origem, configuracao.Regime, operacao,
            modelo: "65", serie: configuracao.SerieNfce, itens, ct);
    }
}
