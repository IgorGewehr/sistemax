using SistemaX.Modules.Fiscal.Application.Ports;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Fiscal.Tests.Fakes;

/// <summary>Test double de <see cref="IGatewayCartaCorrecaoSefaz"/> — devolve um resultado
/// pré-configurado sem tocar rede (mesmo papel de <see cref="FakeGatewayEmissaoSefaz"/>).</summary>
public sealed class FakeGatewayCartaCorrecaoSefaz : IGatewayCartaCorrecaoSefaz
{
    private readonly Result _resultado;

    private FakeGatewayCartaCorrecaoSefaz(Result resultado) => _resultado = resultado;

    public int Chamadas { get; private set; }
    public int? UltimaSequenciaRecebida { get; private set; }
    public string? UltimaChaveRecebida { get; private set; }
    public string? UltimaCorrecaoRecebida { get; private set; }

    public static FakeGatewayCartaCorrecaoSefaz Sucesso() => new(Result.Ok());

    public static FakeGatewayCartaCorrecaoSefaz FalhandoInfra(string codigo = "fiscal.sefaz.503", string mensagem = "Serviço indisponível") =>
        new(Result.Falhar(new Error(codigo, mensagem)));

    public Task<Result> RegistrarCorrecaoAsync(
        string tenantId, string chaveAcesso, string correcao, string ufEmitente, int sequencia, CancellationToken ct = default)
    {
        Chamadas++;
        UltimaSequenciaRecebida = sequencia;
        UltimaChaveRecebida = chaveAcesso;
        UltimaCorrecaoRecebida = correcao;
        return Task.FromResult(_resultado);
    }
}
