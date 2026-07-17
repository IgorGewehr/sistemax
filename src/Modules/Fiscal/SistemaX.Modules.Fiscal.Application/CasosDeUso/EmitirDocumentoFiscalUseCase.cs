using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Fiscal.Application.Motor;
using SistemaX.Modules.Fiscal.Application.Ports;
using SistemaX.Modules.Fiscal.Domain.Comum;
using SistemaX.Modules.Fiscal.Domain.Documentos;
using SistemaX.Modules.Fiscal.Domain.Operacoes;
using SistemaX.Modules.Fiscal.Domain.Regimes;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Fiscal.Application.CasosDeUso;

/// <summary>
/// Abre um <see cref="DocumentoFiscal"/>, resolve tributação item a item, aloca o número e, na
/// sequência (fora da transação de numeração — I/O de rede não compartilha atomicidade com ela,
/// docs/fiscal/emissao-mapping.md §7.3), chama <see cref="IGatewayEmissaoSefaz.TransmitirAsync"/>
/// para transmitir de verdade: <c>NumeroAlocado → Autorizado/Rejeitado/Denegado</c> conforme o
/// gateway responder. Falha de INFRAESTRUTURA do gateway (timeout, SEFAZ indisponível) NUNCA falha
/// este caso de uso — o documento permanece <c>NumeroAlocado</c> (número já é fato comprometido) e
/// fica para um job de retransmissão pegar depois; só rejeição/denegação/autorização (a SEFAZ
/// respondeu) avança a FSM aqui.
///
/// Idempotente por <see cref="SourceRef.Chave"/> — reprocessar a mesma origem (ex.: replay de
/// <c>VendaItensMovimentados</c>) encontra o documento já existente e retorna-o sem duplicar
/// (R3 do CLAUDE.md).
/// </summary>
public sealed class EmitirDocumentoFiscalUseCase(
    IDocumentoFiscalRepository documentos,
    ISequenciaFiscalRepository sequencias,
    ResolvedorDeItemFiscalService resolvedor,
    IIntegrationEventBus bus,
    IUnidadeDeTrabalhoFiscal unidadeDeTrabalho,
    TransmitirDocumentoFiscalUseCase transmissor)
{
    public async Task<Result<DocumentoFiscal>> ExecutarAsync(
        string tenantId, TipoDocumentoFiscal tipo, SourceRef origem, RegimeTributario regime,
        OperacaoFiscal operacao, string modelo, string serie, IReadOnlyList<ItemParaEmitir> itens,
        CancellationToken ct = default)
    {
        var existente = await documentos.ObterPorOrigemAsync(tenantId, origem.Chave, ct);
        if (existente is not null)
            return Result.Ok(existente);

        if (itens.Count == 0)
            return Result.Falhar<DocumentoFiscal>(new Error("fiscal.documento.sem_itens", "Emissão sem itens."));

        var documento = DocumentoFiscal.Abrir(tenantId, tipo, origem);

        foreach (var item in itens)
        {
            var resolvido = await resolvedor.ResolverAsync(
                tenantId, regime, operacao, item.ProdutoId, item.Descricao, item.Ncm,
                item.Quantidade, item.PrecoUnitario, item.Desconto, item.CfopDaEmissao, ct);

            if (resolvido.Falha)
            {
                var bloqueio = documento.Bloquear(resolvido.Erro.Mensagem);
                if (bloqueio.Falha) return Result.Falhar<DocumentoFiscal>(bloqueio.Erro);

                await documentos.SalvarAsync(documento, ct);
                return Result.Ok(documento); // bloqueado — venda não é impedida, nota fica pendente de configuração
            }

            var adicionado = documento.AdicionarItemResolvido(resolvido.Valor);
            if (adicionado.Falha) return Result.Falhar<DocumentoFiscal>(adicionado.Erro);
        }

        // Alocação do número + persistência do documento em NumeroAlocado NA MESMA transação
        // local (docs/fiscal/arquitetura.md §5/§7): um crash entre as duas chamadas nunca pode
        // deixar "número consumido, documento não gravado". IUnidadeDeTrabalhoFiscal abre a
        // sessão ambiente que ISequenciaFiscalRepository/IDocumentoFiscalRepository (Infrastructure)
        // já sabem consultar para participar da mesma transação em vez de abrir a própria.
        await unidadeDeTrabalho.IniciarAsync(ct);
        try
        {
            var numeroResult = await sequencias.AlocarProximoAsync(tenantId, modelo, serie, ct);
            if (numeroResult.Falha)
            {
                await unidadeDeTrabalho.RollbackAsync(ct);
                return Result.Falhar<DocumentoFiscal>(numeroResult.Erro);
            }

            var alocado = documento.AlocarNumero(serie, numeroResult.Valor);
            if (alocado.Falha)
            {
                await unidadeDeTrabalho.RollbackAsync(ct);
                return Result.Falhar<DocumentoFiscal>(alocado.Erro);
            }

            await documentos.SalvarAsync(documento, ct);
            await unidadeDeTrabalho.CommitAsync(ct); // número e documento comprometidos juntos
        }
        catch
        {
            await unidadeDeTrabalho.RollbackAsync(ct);
            throw;
        }

        foreach (var evento in documento.DomainEvents.OfType<NumeroFiscalAlocadoDomainEvent>())
            await bus.PublishAsync(evento.ParaEventoDeIntegracao(), ct);

        documento.ClearDomainEvents();

        // Chamada JÁ FORA da transação de numeração (§7.3) — I/O de rede não compartilha
        // atomicidade com ela. TransmitirDocumentoFiscalUseCase registra o desfecho na FSM; o
        // MESMO caso de uso é reusado pelo job de retry (RetransmitirDocumentosPendentesUseCase)
        // para documentos que ficam presos aqui por falha de infraestrutura.
        return await transmissor.ExecutarAsync(documento, ct);
    }
}
