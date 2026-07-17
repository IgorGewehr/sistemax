using SistemaX.Modules.Compras.Application.Ports;
using SistemaX.Modules.Compras.Domain.Fornecedores;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Compras.Application.CasosDeUso;

/// <summary>Cadastra (ou retorna o existente) — dedupe SÓ por documento não-vazio. Fornecedor sem
/// documento (produtor rural, informal) é SEMPRE novo: a lição real do gestao-raiz foi fundir
/// fornecedores distintos porque os dois tinham <c>documento == ""</c>.</summary>
public sealed class CadastrarFornecedorUseCase(IFornecedorRepository fornecedores)
{
    public async Task<Result<Fornecedor>> ExecutarAsync(
        string tenantId, string razaoSocial, string? documento = null, string? nomeFantasia = null, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(documento))
        {
            var existente = await fornecedores.ObterPorDocumentoAsync(tenantId, documento, ct);
            if (existente is not null) return Result.Ok(existente);
        }

        var resultado = Fornecedor.Cadastrar(tenantId, razaoSocial, documento, nomeFantasia);
        if (resultado.Falha) return resultado;

        await fornecedores.SalvarAsync(resultado.Valor, ct);
        return resultado;
    }
}

/// <summary>Transições de FSM do fornecedor — "busca → chama o método do agregado → salva", mesmo
/// padrão de <c>MontarVendaUseCase</c>.</summary>
public sealed class GerenciarFornecedorUseCase(IFornecedorRepository fornecedores)
{
    public Task<Result> BloquearAsync(string fornecedorId, CancellationToken ct = default)
        => MutarAsync(fornecedorId, f => f.Bloquear(), ct);

    public Task<Result> ReativarAsync(string fornecedorId, CancellationToken ct = default)
        => MutarAsync(fornecedorId, f => f.Reativar(), ct);

    public Task<Result> InativarAsync(string fornecedorId, CancellationToken ct = default)
        => MutarAsync(fornecedorId, f => f.Inativar(), ct);

    private async Task<Result> MutarAsync(string fornecedorId, Func<Fornecedor, Result> acao, CancellationToken ct)
    {
        var fornecedor = await fornecedores.ObterPorIdAsync(fornecedorId, ct);
        if (fornecedor is null)
            return Result.Falhar(new Error("compras.fornecedor.nao_encontrado", $"Fornecedor '{fornecedorId}' não encontrado."));

        var resultado = acao(fornecedor);
        if (resultado.Falha) return resultado;

        await fornecedores.SalvarAsync(fornecedor, ct);
        return Result.Ok();
    }
}
