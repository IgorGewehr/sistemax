using SistemaX.Modules.Fiscal.Application.Ports;
using SistemaX.Modules.Fiscal.Domain.Comum;
using SistemaX.Modules.Fiscal.Domain.Documentos;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Fiscal.Application.CasosDeUso;

/// <summary>
/// Caso de uso MÍNIMO sobre o gateway existente (docs/fiscal/emissao-mapping.md §9/§11, gap #11):
/// registra uma Carta de Correção Eletrônica para um <see cref="DocumentoFiscal"/> já
/// <see cref="StatusDocumentoFiscal.Autorizado"/> — GUARDA DE FSM explícita aqui (não no agregado,
/// que não tem — nem precisa ter — o conceito de "correção", ver comentário de
/// <see cref="CartaCorrecaoFiscal"/>): só documento autorizado aceita CC-e, porque só ele tem
/// <see cref="DocumentoFiscal.ChaveDeAcesso"/> — sem chave não há o que corrigir na SEFAZ.
///
/// <see cref="Sequencia"/> é calculado aqui a partir do histórico já persistido (1, 2, 3...) — a
/// SEFAZ aceita no máximo 20 CC-e por chave de acesso (layout NFe), verificado antes de chamar o
/// gateway para nunca gastar uma chamada de rede numa correção que a SEFAZ rejeitaria de qualquer
/// forma.
/// </summary>
public sealed class EmitirCartaCorrecaoUseCase(
    IDocumentoFiscalRepository documentos,
    ICartaCorrecaoFiscalRepository cartas,
    IConfiguracaoFiscalTenantRepository configuracoes,
    IGatewayCartaCorrecaoSefaz gateway)
{
    private const int SequenciaMaxima = 20;
    private const int TextoMinimoCaracteres = 15;

    public async Task<Result<CartaCorrecaoFiscal>> ExecutarAsync(
        string documentoFiscalId, string correcao, DateTimeOffset agora, CancellationToken ct = default)
    {
        var documento = await documentos.ObterPorIdAsync(documentoFiscalId, ct);
        if (documento is null)
            return Result.Falhar<CartaCorrecaoFiscal>(new Error(
                "fiscal.documento.nao_encontrado", $"Documento fiscal '{documentoFiscalId}' não encontrado."));

        // Guarda de FSM (fora do agregado, ver doc da classe): só Autorizado tem chaveAcesso e é
        // o único estado em que uma correção pós-emissão faz sentido (Cancelado/Denegado/etc. não
        // têm o que "corrigir" — já não são mais o fato vigente).
        if (documento.Status != StatusDocumentoFiscal.Autorizado)
            return Result.Falhar<CartaCorrecaoFiscal>(new Error(
                "fiscal.cce.documento_nao_autorizado",
                $"Carta de Correção só pode ser emitida sobre documento Autorizado (status atual: '{documento.Status}')."));

        if (string.IsNullOrWhiteSpace(correcao) || correcao.Trim().Length < TextoMinimoCaracteres)
            return Result.Falhar<CartaCorrecaoFiscal>(new Error(
                "fiscal.cce.texto_curto", $"Texto da correção exige ao menos {TextoMinimoCaracteres} caracteres (layout SEFAZ)."));

        var configuracao = await configuracoes.ObterAsync(documento.TenantId, ct);
        if (configuracao is null)
            return Result.Falhar<CartaCorrecaoFiscal>(new Error(
                "fiscal.cce.configuracao_tenant_ausente", $"Tenant '{documento.TenantId}' sem ConfiguracaoFiscalTenant."));

        var existentes = await cartas.ListarPorDocumentoAsync(documentoFiscalId, ct);
        var proximaSequencia = existentes.Count + 1;
        if (proximaSequencia > SequenciaMaxima)
            return Result.Falhar<CartaCorrecaoFiscal>(new Error(
                "fiscal.cce.limite_excedido", $"Documento já tem {existentes.Count} cartas de correção — limite SEFAZ é {SequenciaMaxima}."));

        var registro = await gateway.RegistrarCorrecaoAsync(
            documento.TenantId, documento.ChaveDeAcesso!, correcao.Trim(), configuracao.UfOrigem, proximaSequencia, ct);
        if (registro.Falha)
            return Result.Falhar<CartaCorrecaoFiscal>(registro.Erro);

        var carta = new CartaCorrecaoFiscal(
            IdGenerator.NovoId(), documento.TenantId, documento.Id, documento.ChaveDeAcesso!, proximaSequencia, correcao.Trim(), agora);

        await cartas.SalvarAsync(carta, ct);
        return Result.Ok(carta);
    }
}
