using SistemaX.Modules.Fiscal.Domain.Operacoes;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Fiscal.Application.Ports;

/// <summary>
/// Resolve o CFOP efetivo de um item pela cadeia decidida por Igor (ADR-0002):
/// <c>emissão &gt; produto &gt; padrão-config</c>. Nunca hardcode em nenhum dos 3 níveis:
/// "emissão" é o parâmetro explícito passado pelo caller (UI/caso de uso de emissão); "produto" é
/// <see cref="IDadosFiscaisProdutoCacheRepository"/> (cadastro do produto no Estoque, propagado
/// via evento); "padrão-config" é <see cref="IRegraCfopRepository"/> (dado seedável, Settings→Fiscal).
///
/// É um Port (nome do design doc, docs/fiscal/arquitetura.md §6) mas a ÚNICA implementação
/// (<c>ResolvedorDeCfop</c>) vive em Application, não em Infrastructure: a função em si não faz
/// I/O direto — só orquestra os DOIS outros ports acima, que já são o boundary de infraestrutura.
/// Não há adapter InMemory/Sqlite separado a duplicar aqui (mesmo racional de não modelar
/// <c>SequenciaFiscal</c> como AggregateRoot — evitar indireção sem benefício).
/// </summary>
public interface IResolvedorDeCfop
{
    Task<Result<string>> ResolverAsync(
        string tenantId, OperacaoFiscal operacao, string produtoId, string? cfopDaEmissao, CancellationToken ct = default);
}
