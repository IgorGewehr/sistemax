using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SistemaX.Modules.Fiscal.Application.Ports;
using SistemaX.Modules.Fiscal.Domain.Documentos;
using SistemaX.Modules.Fiscal.Infrastructure.Sefaz.Mapeamento;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Fiscal.Infrastructure.Sefaz;

/// <summary>
/// Único ponto de I/O de rede do módulo Fiscal — implementação HTTP de
/// <see cref="IGatewayEmissaoSefaz"/>/<see cref="IGatewayCancelamentoSefaz"/>/
/// <see cref="IGatewayInutilizacaoSefaz"/> contra o MESMO gateway (`emissao.tensorroot.com` ou
/// compatível) que <c>saas-erp/lib/services/sefaz-gateway.ts</c> já fala em produção
/// (docs/fiscal/emissao-mapping.md §2). <c>MotorDeCalculoTributario</c>/<c>Fiscal.Domain</c> nunca
/// importam isto (regra de fronteira: Domain não faz I/O).
///
/// Retry/backoff/detecção de erro transiente replica <c>sefazRequest()</c> do TS linha a linha
/// (mesmos códigos HTTP, mesmo backoff 1s/2s/4s, mesmo timeout) — evita que o saas-erp e o
/// SistemaX tenham comportamentos diferentes contra o mesmo gateway sob carga (§2).
/// </summary>
public sealed class SefazApiGateway(
    HttpClient http,
    IOptions<SefazGatewayOptions> options,
    IConfiguracaoFiscalTenantRepository configuracoes,
    ICadastroFiscalEmitenteRepository emitentes,
    ICertificadoDigitalRepository certificados,
    IDestinatarioDocumentoFiscalRepository destinatarios,
    IFormaPagamentoDocumentoFiscalRepository formasPagamento,
    IDadosFiscaisProdutoCacheRepository dadosProduto,
    IReferenciaDevolucaoDocumentoFiscalRepository referenciasDevolucao,
    ILogger<SefazApiGateway> logger)
    : IGatewayEmissaoSefaz, IGatewayCancelamentoSefaz, IGatewayInutilizacaoSefaz
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public async Task<Result<ResultadoTransmissaoSefaz>> TransmitirAsync(DocumentoFiscal documento, CancellationToken ct = default)
    {
        var opts = options.Value;

        // Modo MOCK nunca monta payload de verdade nem faz I/O — mesmo comportamento de
        // isMockMode() no sefaz-gateway.ts (buildMockResponse retorna ANTES de qualquer fetch).
        if (opts.ModoMock)
            return Result.Ok(MontarResultadoMock(opts));

        var insumosResult = await ResolverInsumosAsync(documento, opts, ct);
        if (insumosResult.Falha) return Result.Falhar<ResultadoTransmissaoSefaz>(insumosResult.Erro);

        var payloadResult = MontarPayload(documento, insumosResult.Valor);
        if (payloadResult.Falha) return Result.Falhar<ResultadoTransmissaoSefaz>(payloadResult.Erro);

        var endpoint = documento.Tipo switch
        {
            TipoDocumentoFiscal.NFe => "/nfe/emitir",
            TipoDocumentoFiscal.NFCe => "/nfe/nfce/emitir",
            TipoDocumentoFiscal.NFSe => "/nfse/emitir",
            _ => null,
        };
        if (endpoint is null)
            return Result.Falhar<ResultadoTransmissaoSefaz>(new Error(
                "fiscal.emissao.tipo_nao_suportado", $"Tipo '{documento.Tipo}' sem endpoint de emissão mapeado."));

        var respostaResult = await PostComRetryAsync(endpoint, payloadResult.Valor, "Transmitir" + documento.Tipo, ct);
        if (respostaResult.Falha) return Result.Falhar<ResultadoTransmissaoSefaz>(respostaResult.Erro);

        return InterpretarResposta(respostaResult.Valor);
    }

    public async Task<Result<ResultadoTransmissaoSefaz>> ConsultarAsync(DocumentoFiscal documento, CancellationToken ct = default)
    {
        var opts = options.Value;

        if (opts.ModoMock)
        {
            return Result.Ok(documento.ChaveDeAcesso is not null
                ? ResultadoTransmissaoSefaz.Autorizado(documento.ChaveDeAcesso, protocolo: null, DateTimeOffset.UtcNow)
                : ResultadoTransmissaoSefaz.Processando());
        }

        if (documento.ChaveDeAcesso is null)
            return Result.Falhar<ResultadoTransmissaoSefaz>(new Error(
                "fiscal.emissao.consulta_sem_chave", "Documento sem chaveAcesso ainda — nada a consultar antes da primeira transmissão."));

        var insumosResult = await ResolverInsumosAsync(documento, opts, ct);
        if (insumosResult.Falha) return Result.Falhar<ResultadoTransmissaoSefaz>(insumosResult.Erro);

        var payload = new
        {
            chaveAcesso = documento.ChaveDeAcesso,
            ufEmitente = insumosResult.Valor.UfOrigem,
            ambiente = insumosResult.Valor.AmbienteSefaz,
            certificado = new { pfxBase64 = insumosResult.Valor.Certificado!.PfxBase64, password = insumosResult.Valor.Certificado!.Senha },
        };

        var respostaResult = await PostComRetryAsync("/nfe/consultar", payload, "ConsultarNFe", ct);
        return respostaResult.Falha
            ? Result.Falhar<ResultadoTransmissaoSefaz>(respostaResult.Erro)
            : InterpretarResposta(respostaResult.Valor);
    }

    public async Task<Result<ResultadoCancelamentoSefaz>> CancelarAsync(DocumentoFiscal documento, string justificativa, CancellationToken ct = default)
    {
        var opts = options.Value;

        if (opts.ModoMock)
            return Result.Ok(new ResultadoCancelamentoSefaz(GerarProtocoloMock(), DateTimeOffset.UtcNow));

        if (documento.ChaveDeAcesso is null)
            return Result.Falhar<ResultadoCancelamentoSefaz>(new Error(
                "fiscal.cancelamento.sem_chave", "Documento sem chaveAcesso não pode ser cancelado."));

        var insumosResult = await ResolverInsumosAsync(documento, opts, ct);
        if (insumosResult.Falha) return Result.Falhar<ResultadoCancelamentoSefaz>(insumosResult.Erro);

        var payload = new
        {
            chaveAcesso = documento.ChaveDeAcesso,
            // Protocolo de autorização devolvido pela SEFAZ na transmissão original, persistido em
            // DocumentoFiscal.Protocolo por RegistrarAutorizacao — nunca string vazia quando o
            // gateway o forneceu (alguns UFs/gateways exigem o protocolo original no payload de
            // cancelamento).
            protocolo = documento.Protocolo ?? string.Empty,
            justificativa,
            ufEmitente = insumosResult.Valor.UfOrigem,
            ambiente = insumosResult.Valor.AmbienteSefaz,
            certificado = new { pfxBase64 = insumosResult.Valor.Certificado!.PfxBase64, password = insumosResult.Valor.Certificado!.Senha },
        };

        var respostaResult = await PostComRetryAsync("/nfe/cancelar", payload, "CancelarNFe", ct);
        if (respostaResult.Falha) return Result.Falhar<ResultadoCancelamentoSefaz>(respostaResult.Erro);

        var resposta = respostaResult.Valor;
        if (resposta.Status != "cancelado")
            return Result.Falhar<ResultadoCancelamentoSefaz>(new Error(
                "fiscal.cancelamento.nao_confirmado", resposta.MotivoStatus ?? "SEFAZ não confirmou o cancelamento."));

        return Result.Ok(new ResultadoCancelamentoSefaz(resposta.Protocolo ?? string.Empty, DateTimeOffset.UtcNow));
    }

    public async Task<Result> InutilizarAsync(
        string tenantId, string cnpj, string modelo, string serie, long numeroInicial, long numeroFinal,
        string justificativa, string ufEmitente, CancellationToken ct = default)
    {
        var opts = options.Value;
        if (opts.ModoMock) return Result.Ok();

        var certificado = await certificados.ObterAsync(tenantId, ct);
        if (certificado is null)
            return Result.Falhar(new Error(
                "fiscal.inutilizacao.certificado_ausente", $"Tenant '{tenantId}' sem certificado digital configurado."));

        var payload = new
        {
            cnpj,
            modelo,
            serie,
            numeroInicial,
            numeroFinal,
            justificativa,
            ano = DateTimeOffset.UtcNow.ToString("yyyy"),
            ufEmitente,
            ambiente = opts.AmbienteSefaz,
            certificado = new { pfxBase64 = certificado.PfxBase64, password = certificado.Senha },
        };

        var respostaResult = await PostComRetryAsync("/nfe/inutilizar", payload, "InutilizarNFe", ct);
        return respostaResult.Falha ? Result.Falhar(respostaResult.Erro) : Result.Ok();
    }

    // ------------------------------------------------------------------ insumos / payload

    private async Task<Result<InsumosMapeamento>> ResolverInsumosAsync(DocumentoFiscal documento, SefazGatewayOptions opts, CancellationToken ct)
    {
        var configuracao = await configuracoes.ObterAsync(documento.TenantId, ct);
        if (configuracao is null)
            return Result.Falhar<InsumosMapeamento>(new Error(
                "fiscal.emissao.configuracao_tenant_ausente",
                $"Tenant '{documento.TenantId}' sem ConfiguracaoFiscalTenant — não é possível resolver regime/UF de origem."));

        var emitente = await emitentes.ObterAsync(documento.TenantId, ct);
        if (emitente is null)
            return Result.Falhar<InsumosMapeamento>(new Error(
                "fiscal.emissao.cadastro_emitente_ausente",
                $"Tenant '{documento.TenantId}' sem CadastroFiscalEmitente — cadastre CNPJ/Razão Social/endereço antes de emitir (gap #4, emissao-mapping.md §3)."));

        var certificado = await certificados.ObterAsync(documento.TenantId, ct);
        if (certificado is null)
            return Result.Falhar<InsumosMapeamento>(new Error(
                "fiscal.emissao.certificado_ausente",
                $"Tenant '{documento.TenantId}' sem certificado digital configurado (gap #2, emissao-mapping.md §4.6)."));

        var destinatario = await destinatarios.ObterPorDocumentoAsync(documento.Id, ct);
        var pagamentosDoDocumento = await formasPagamento.ObterPorDocumentoAsync(documento.Id, ct);

        // Gap #5 (emissao-mapping.md §4.6/§11) — só relevante quando o documento é uma devolução
        // vinculada a uma NF-e original; ausência (null) é o caso comum e o mapper omite o bloco
        // `referencias` (§1: nunca inferido, só projetado a partir do que foi vinculado).
        var refNFeDevolucao = await referenciasDevolucao.ObterRefNFeAsync(documento.Id, ct);

        // Gap #6 (emissao-mapping.md §4.3/§11) — GTIN/unidade comercial por produto, resolvido
        // aqui (Infrastructure, é I/O) e passado como projeção pronta pro mapper — nunca lido de
        // PerfilFiscalNCM/TributacaoProduto (§1), só da cópia cadastral local do produto.
        var dadosProdutoPorId = new Dictionary<string, DadosFiscaisProdutoCache>(documento.Itens.Count);
        foreach (var item in documento.Itens)
        {
            if (dadosProdutoPorId.ContainsKey(item.ProdutoId)) continue;
            var cache = await dadosProduto.ObterAsync(documento.TenantId, item.ProdutoId, ct);
            if (cache is not null) dadosProdutoPorId[item.ProdutoId] = cache;
        }

        return Result.Ok(new InsumosMapeamento(
            Emitente: emitente,
            Regime: configuracao.Regime,
            UfOrigem: configuracao.UfOrigem,
            AmbienteSefaz: opts.AmbienteSefaz,
            Destinatario: destinatario,
            Pagamentos: pagamentosDoDocumento,
            Certificado: certificado,
            RefNFeDevolucao: refNFeDevolucao,
            DadosProdutoPorId: dadosProdutoPorId));
    }

    private static Result<object> MontarPayload(DocumentoFiscal documento, InsumosMapeamento insumos) => documento.Tipo switch
    {
        TipoDocumentoFiscal.NFe => ComoObjeto(DocumentoFiscalPayloadMapper.MontarNFe(documento, insumos)),
        TipoDocumentoFiscal.NFCe => ComoObjeto(DocumentoFiscalPayloadMapper.MontarNFCe(documento, insumos)),
        TipoDocumentoFiscal.NFSe => ComoObjeto(DocumentoFiscalPayloadMapper.MontarNFSe(documento, insumos)),
        _ => Result.Falhar<object>(new Error(
            "fiscal.emissao.tipo_nao_suportado", $"Tipo '{documento.Tipo}' sem mapeamento de payload.")),
    };

    private static Result<object> ComoObjeto<T>(Result<T> resultado) where T : notnull =>
        resultado.Sucesso ? Result.Ok<object>(resultado.Valor) : Result.Falhar<object>(resultado.Erro);

    private static Result<ResultadoTransmissaoSefaz> InterpretarResposta(SefazHttpResponse resposta) => resposta.Status switch
    {
        "autorizado" => resposta.ChaveAcesso is null
            ? Result.Falhar<ResultadoTransmissaoSefaz>(new Error("fiscal.emissao.autorizado_sem_chave", "SEFAZ retornou 'autorizado' sem chaveAcesso."))
            : Result.Ok(ResultadoTransmissaoSefaz.Autorizado(resposta.ChaveAcesso, resposta.Protocolo, ParseData(resposta.DataRecebimento) ?? DateTimeOffset.UtcNow)),
        "rejeitado" => Result.Ok(ResultadoTransmissaoSefaz.Rejeitado(resposta.MotivoStatus ?? "Rejeitado pela SEFAZ (motivo não informado).")),
        "denegado" => Result.Ok(ResultadoTransmissaoSefaz.Denegado(resposta.MotivoStatus ?? "Denegado pela SEFAZ (motivo não informado).")),
        "processando" => Result.Ok(ResultadoTransmissaoSefaz.Processando()),
        _ => Result.Falhar<ResultadoTransmissaoSefaz>(new Error(
            "fiscal.emissao.status_inesperado", $"Gateway devolveu status '{resposta.Status}' fora do esperado.")),
    };

    private static DateTimeOffset? ParseData(string? valor) =>
        !string.IsNullOrWhiteSpace(valor) && DateTimeOffset.TryParse(valor, out var data) ? data : null;

    // ------------------------------------------------------------------ HTTP + retry (§2, mesmo algoritmo do sefazRequest())

    private async Task<Result<SefazHttpResponse>> PostComRetryAsync(string endpoint, object payload, string operacao, CancellationToken ct)
    {
        var opts = options.Value;
        Error? ultimoErro = null;

        for (var tentativa = 1; tentativa <= opts.MaxRetries; tentativa++)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(opts.TimeoutSeconds));

            try
            {
                logger.LogInformation("[SEFAZ] {Operacao} tentativa {Tentativa}/{Max} -> {Endpoint}", operacao, tentativa, opts.MaxRetries, endpoint);

                using var response = await http.PostAsJsonAsync(endpoint, payload, JsonOpts, cts.Token).ConfigureAwait(false);
                var rawBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

                SefazHttpResponse? corpo = null;
                try
                {
                    corpo = string.IsNullOrWhiteSpace(rawBody) ? null : JsonSerializer.Deserialize<SefazHttpResponse>(rawBody, JsonOpts);
                }
                catch (JsonException)
                {
                    // corpo não é JSON — tratado abaixo conforme o status code
                }

                var erroDoCorpo = corpo?.Error ?? corpo?.Message;

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                    return Result.Falhar<SefazHttpResponse>(new Error(
                        "fiscal.sefaz.401", $"Não autenticado: {erroDoCorpo ?? "Authorization header ausente ou vazio"}"));

                if (response.StatusCode == HttpStatusCode.Forbidden)
                    return Result.Falhar<SefazHttpResponse>(new Error(
                        "fiscal.sefaz.403", $"Acesso negado: {erroDoCorpo ?? "verifique SEFAZ_API_KEY e CNPJ do certificado vs emitente"}"));

                // 422 — rejeição de NEGÓCIO (nota rejeitada). Devolve o corpo AS-IS — nunca
                // Result.Falhar (contrato explícito do IGatewayEmissaoSefaz: 422 não é infra).
                if ((int)response.StatusCode == 422)
                {
                    logger.LogWarning("[SEFAZ] {Operacao} -> 422 rejeicao: {Body}", operacao, rawBody);
                    if (corpo is not null) return Result.Ok(corpo);
                    return Result.Falhar<SefazHttpResponse>(new Error("fiscal.sefaz.422_sem_corpo", "422 Rejeição sem corpo válido."));
                }

                if (response.StatusCode == HttpStatusCode.BadRequest)
                    return Result.Falhar<SefazHttpResponse>(new Error(
                        "fiscal.sefaz.400", $"Requisição inválida: {erroDoCorpo ?? response.ReasonPhrase}"));

                if ((int)response.StatusCode == 429)
                {
                    var retryAfter = response.Headers.RetryAfter?.ToString();
                    return Result.Falhar<SefazHttpResponse>(new Error(
                        "fiscal.sefaz.429",
                        $"Rate limit excedido{(retryAfter is not null ? $" (retry-after: {retryAfter})" : string.Empty)}: {erroDoCorpo ?? "aguarde antes de reenviar"}"));
                }

                if ((int)response.StatusCode == 503)
                {
                    ultimoErro = new Error("fiscal.sefaz.503", $"Serviço indisponível: {erroDoCorpo ?? response.ReasonPhrase}");
                }
                else if ((int)response.StatusCode >= 500)
                {
                    ultimoErro = new Error("fiscal.sefaz.5xx", $"Erro do servidor ({(int)response.StatusCode}): {erroDoCorpo ?? response.ReasonPhrase}");
                }
                else if (!response.IsSuccessStatusCode)
                {
                    return Result.Falhar<SefazHttpResponse>(new Error(
                        "fiscal.sefaz.inesperado", $"Resposta inesperada ({(int)response.StatusCode}): {erroDoCorpo ?? response.ReasonPhrase}"));
                }
                else
                {
                    if (corpo is null)
                        return Result.Falhar<SefazHttpResponse>(new Error(
                            "fiscal.sefaz.corpo_invalido", $"Resposta 2xx com body inválido ou vazio: {Truncar(rawBody)}"));
                    return Result.Ok(corpo);
                }
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                ultimoErro = new Error("fiscal.sefaz.timeout", $"Timeout após {opts.TimeoutSeconds}s na operação {operacao}.");
            }
            catch (HttpRequestException ex)
            {
                ultimoErro = new Error("fiscal.sefaz.transporte", $"Falha de transporte: {ex.Message}");
            }

            if (tentativa < opts.MaxRetries)
            {
                var backoff = TimeSpan.FromSeconds(Math.Pow(2, tentativa - 1));
                logger.LogInformation("[SEFAZ] {Operacao} - tentando de novo em {Backoff}s...", operacao, backoff.TotalSeconds);
                await Task.Delay(backoff, ct).ConfigureAwait(false);
            }
        }

        return Result.Falhar<SefazHttpResponse>(ultimoErro ?? new Error(
            "fiscal.sefaz.falhou", $"{operacao} falhou após {opts.MaxRetries} tentativas."));
    }

    private static string Truncar(string valor) => valor.Length > 200 ? valor[..200] : valor;

    // ------------------------------------------------------------------ MOCK mode (dev/CI sem certificado)

    /// <summary>Desfecho default ("autorizado") preserva o comportamento histórico do modo mock.
    /// "rejeitado"/"denegado" via <see cref="SefazGatewayOptions.MockDesfecho"/> existem só para
    /// QA/testes provarem os dois desfechos SEM I/O real (item 1 das pendências —
    /// <c>SefazApiGatewayTests.TransmitirAsync_ModoMock_ComDesfechoRejeitadoForcado_DevolveRejeitadoSemIO</c>).</summary>
    private static ResultadoTransmissaoSefaz MontarResultadoMock(SefazGatewayOptions opts) => opts.MockDesfecho.ToLowerInvariant() switch
    {
        "rejeitado" => ResultadoTransmissaoSefaz.Rejeitado(opts.MockMotivoDesfecho),
        "denegado" => ResultadoTransmissaoSefaz.Denegado(opts.MockMotivoDesfecho),
        _ => ResultadoTransmissaoSefaz.Autorizado(GerarChaveMock(), GerarProtocoloMock(), DateTimeOffset.UtcNow),
    };

    /// <summary>Mesma estrutura de 44 dígitos + DV mod-11 de <c>generateMockAccessKey()</c> em
    /// sefaz-gateway.ts — só para preencher o campo em MOCK, nunca usado para validação real.</summary>
    private static string GerarChaveMock()
    {
        const string uf = "35";
        var agora = DateTimeOffset.UtcNow;
        var aamm = agora.ToString("yy") + agora.ToString("MM");
        const string cnpj = "00000000000191";
        const string modelo = "65";
        const string serie = "001";
        var numero = Random.Shared.Next(0, 999_999_999).ToString().PadLeft(9, '0');
        const string tpEmis = "1";
        var cNf = Random.Shared.Next(0, 99_999_999).ToString().PadLeft(8, '0');
        var parcial = $"{uf}{aamm}{cnpj}{modelo}{serie}{numero}{tpEmis}{cNf}";

        var soma = 0;
        var peso = 2;
        for (var i = parcial.Length - 1; i >= 0; i--)
        {
            soma += (parcial[i] - '0') * peso;
            peso = peso == 9 ? 2 : peso + 1;
        }
        var resto = soma % 11;
        var dv = resto < 2 ? 0 : 11 - resto;
        return $"{parcial}{dv}";
    }

    private static string GerarProtocoloMock()
    {
        var ano = DateTimeOffset.UtcNow.ToString("yy");
        var seq = Random.Shared.NextInt64(0, 9_999_999_999).ToString().PadLeft(10, '0');
        return $"135{ano}{seq}";
    }
}
