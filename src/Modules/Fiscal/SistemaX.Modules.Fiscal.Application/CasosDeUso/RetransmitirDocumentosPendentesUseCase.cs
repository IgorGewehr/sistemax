using SistemaX.Modules.Fiscal.Application.Ports;
using SistemaX.Modules.Fiscal.Domain.Documentos;

namespace SistemaX.Modules.Fiscal.Application.CasosDeUso;

/// <summary>
/// O "job de retransmissão"/"job de consulta periódica" que docs/fiscal/emissao-mapping.md §7.2/§9
/// e os comentários de <see cref="TransmitirDocumentoFiscalUseCase"/> citavam como necessário e
/// ainda não existia: reavalia documentos presos em <see cref="StatusDocumentoFiscal.NumeroAlocado"/>
/// há mais de <c>antiguidadeMinima</c> (falha de infraestrutura do gateway numa tentativa anterior,
/// nunca rejeição/denegação — essas já avançaram a FSM) e tenta de novo. <c>antiguidadeMinima</c> É
/// o backoff: um documento recém-alocado ainda não teve chance de a rede/SEFAZ se estabilizar, e
/// não é retentado antes desse prazo — o próprio intervalo do cron (mais espaçado que uma retentativa
/// imediata) é o segundo nível de backoff.
///
/// LIMITE DE TENTATIVAS — <paramref name="idadeMaximaAntesDeDesistir"/> (opcional; ausente = nunca
/// desiste sozinho, comportamento anterior preservado): documento que CONTINUA <c>NumeroAlocado</c>
/// além desse teto de idade é roteado para <see cref="DesistirDeNumeroUseCase"/> — a transição de
/// FSM que a doc de <c>IDocumentoFiscalRepository.ListarNumeroAlocadoAntesDeAsync</c> já nomeava
/// como destino ("insumo do job periódico que roteia para DesistirDeNumeroUseCase"). Sem contador de
/// tentativas persistido — a IDADE do documento é o proxy: cada rodada do cron que ainda o encontra
/// <c>NumeroAlocado</c> representa uma tentativa acumulada, e o cron já não retenta o que é jovem
/// demais (<c>antiguidadeMinima</c>).
///
/// Mesmo molde de <c>AvaliarParcelasVencidasUseCase</c> do Financeiro: um caso de uso Application
/// puro, chamado com um <c>tenantId</c> explícito, acionado periodicamente por
/// <c>RetransmissaoFiscalBackgroundService</c> (Fiscal.Infrastructure).
///
/// Idempotente por natureza (não por execução): reavalia o estado ATUAL a cada chamada. Documentos
/// que sem <see cref="DocumentoFiscal.ChaveDeAcesso"/> nunca tiveram uma transmissão aceita pela
/// SEFAZ — o único caminho seguro é retransmitir o XML de novo
/// (<see cref="TransmitirDocumentoFiscalUseCase.ExecutarAsync"/>). Um documento com
/// <see cref="DocumentoFiscal.ChaveDeAcesso"/> já preenchido mas ainda <c>NumeroAlocado</c> seria
/// o caso "SEFAZ recebeu, ainda não decidiu" — consultado via
/// <see cref="TransmitirDocumentoFiscalUseCase.ConsultarAsync"/> em vez de reenviar o XML (não
/// alcançável hoje: nenhum caminho do agregado preenche ChaveDeAcesso sem também sair de
/// NumeroAlocado — mantido aqui pronto para quando o gateway passar a devolver um recibo
/// provisório em <see cref="ResultadoTransmissaoSefaz.Processando"/>).
/// </summary>
public sealed class RetransmitirDocumentosPendentesUseCase(
    IDocumentoFiscalRepository documentos,
    TransmitirDocumentoFiscalUseCase transmissor,
    DesistirDeNumeroUseCase desistir)
{
    /// <returns>Quantidade de documentos que saíram de <c>NumeroAlocado</c> nesta rodada por
    /// RETRANSMISSÃO (autorizados, rejeitados ou denegados) — não conta as desistências (essas
    /// também saem de <c>NumeroAlocado</c>, mas por limite de tentativas, não por resposta da
    /// SEFAZ). Quem ainda falhar por infra ou continuar "ainda processando" sem exceder
    /// <paramref name="idadeMaximaAntesDeDesistir"/> simplesmente não conta e é retentado na
    /// próxima rodada.</returns>
    public async Task<int> ExecutarAsync(
        string tenantId, TimeSpan antiguidadeMinima, TimeSpan? idadeMaximaAntesDeDesistir = null, CancellationToken ct = default)
    {
        var limite = DateTimeOffset.UtcNow - antiguidadeMinima;
        var pendentes = await documentos.ListarNumeroAlocadoAntesDeAsync(tenantId, limite, ct);

        var resolvidos = 0;
        foreach (var documento in pendentes)
        {
            var resultado = documento.ChaveDeAcesso is null
                ? await transmissor.ExecutarAsync(documento, ct)
                : await transmissor.ConsultarAsync(documento, ct);

            if (resultado.Sucesso && resultado.Valor.Status != StatusDocumentoFiscal.NumeroAlocado)
            {
                resolvidos++;
                continue;
            }

            // Ainda preso após a retransmissão — limite de tentativas: idade excedeu o teto
            // configurado, desiste formalmente (Inutilizado) em vez de retentar pra sempre.
            if (idadeMaximaAntesDeDesistir is { } tetoIdade && DateTimeOffset.UtcNow - documento.CriadoEm >= tetoIdade)
            {
                await desistir.ExecutarAsync(documento.Id,
                    $"Retransmissão automática excedeu o limite de tentativas ({tetoIdade}) — número liberado.", ct);
            }
        }

        return resolvidos;
    }
}
