using SistemaX.Modules.Fiscal.Application.Ports;
using SistemaX.Modules.Fiscal.Domain.Documentos;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Fiscal.Tests.Fakes;

/// <summary>Test double de <see cref="IGatewayEmissaoSefaz"/> — devolve um resultado
/// pré-configurado sem tocar rede, para testar como <c>EmitirDocumentoFiscalUseCase</c> reage a
/// cada desfecho possível do gateway (equivalente ao que <c>SefazApiGateway</c> devolveria depois
/// de interpretar a resposta HTTP real — 2xx autorizado, 422 rejeitado, etc — ver
/// docs/fiscal/emissao-mapping.md §7.1).</summary>
public sealed class FakeGatewayEmissaoSefaz : IGatewayEmissaoSefaz
{
    private readonly Result<ResultadoTransmissaoSefaz> _resultado;

    public FakeGatewayEmissaoSefaz(Result<ResultadoTransmissaoSefaz> resultado) => _resultado = resultado;

    public DocumentoFiscal? DocumentoRecebido { get; private set; }
    public int Chamadas { get; private set; }

    public static FakeGatewayEmissaoSefaz Autorizando(string chaveAcesso = "35260112345678000195650010000000091000000091", string? protocolo = "135260000000001") =>
        new(Result.Ok(ResultadoTransmissaoSefaz.Autorizado(chaveAcesso, protocolo, DateTimeOffset.UtcNow)));

    public static FakeGatewayEmissaoSefaz Rejeitando(string motivo = "Rejeição 225: falha no schema XML") =>
        new(Result.Ok(ResultadoTransmissaoSefaz.Rejeitado(motivo)));

    public static FakeGatewayEmissaoSefaz FalhandoInfra(string codigo = "fiscal.sefaz.503", string mensagem = "Serviço indisponível") =>
        new(Result.Falhar<ResultadoTransmissaoSefaz>(new Error(codigo, mensagem)));

    public Task<Result<ResultadoTransmissaoSefaz>> TransmitirAsync(DocumentoFiscal documento, CancellationToken ct = default)
    {
        Chamadas++;
        DocumentoRecebido = documento;
        return Task.FromResult(_resultado);
    }

    public Task<Result<ResultadoTransmissaoSefaz>> ConsultarAsync(DocumentoFiscal documento, CancellationToken ct = default)
        => Task.FromResult(_resultado);
}
