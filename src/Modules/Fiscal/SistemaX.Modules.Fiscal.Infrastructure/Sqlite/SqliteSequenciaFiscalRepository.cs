using SistemaX.Infrastructure.Local;
using SistemaX.Infrastructure.Local.Sequences;
using SistemaX.Infrastructure.Local.UnitOfWork;
using SistemaX.Modules.Fiscal.Application.Ports;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Fiscal.Infrastructure.Sqlite;

/// <summary>
/// Alocação atômica REAL — reusa <see cref="ILocalSequenceAllocator"/> (a mesma peça de
/// infraestrutura que o resto do repo já usa para <c>sale_number</c>) contra o nome de sequência
/// <c>fiscal:{tenantId}:{modelo}:{serie}</c>, o que dá a cada (tenant, modelo, série) sua própria
/// linha em <c>local_sequences</c> — exatamente a chave natural de <c>SequenciaFiscal</c> descrita
/// em docs/fiscal/arquitetura.md §5, sem precisar de uma tabela dedicada.
///
/// Quando chamado DENTRO de uma <see cref="ILocalSessao"/> ativa, a alocação participa da MESMA
/// transação que grava o <c>DocumentoFiscal</c> em <c>NumeroAlocado</c> (a exigência dura do
/// design: nunca dois passos separados) — é o caminho que <c>EmitirDocumentoFiscalUseCase</c>
/// sempre usa hoje, via <c>IUnidadeDeTrabalhoFiscal.IniciarAsync</c> chamado antes desta alocação.
/// Sem sessão ambiente (chamador direto do port fora desse caso de uso — ex.: uma ferramenta
/// administrativa ou um teste), abre e commita sua própria transação curta — ainda atômica quanto
/// à PRÓPRIA alocação isoladamente.
/// </summary>
public sealed class SqliteSequenciaFiscalRepository(
    ILocalSqliteConnectionFactory connectionFactory, ILocalSessao sessao, ILocalSequenceAllocator allocador)
    : ISequenciaFiscalRepository
{
    public async Task<Result<long>> AlocarProximoAsync(string tenantId, string modelo, string serie, CancellationToken ct = default)
    {
        var nomeSequencia = $"fiscal:{tenantId}:{modelo}:{serie}";

        if (sessao.Atual is { } uow)
        {
            var numeroNaSessao = await allocador.NextAsync(uow.Connection, uow.Transaction, nomeSequencia, ct).ConfigureAwait(false);
            return Result.Ok(numeroNaSessao);
        }

        await using var connection = await connectionFactory.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var transaction = (Microsoft.Data.Sqlite.SqliteTransaction)await connection.BeginTransactionAsync(ct).ConfigureAwait(false);
        var numero = await allocador.NextAsync(connection, transaction, nomeSequencia, ct).ConfigureAwait(false);
        await transaction.CommitAsync(ct).ConfigureAwait(false);
        return Result.Ok(numero);
    }
}
