using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Compras.Domain.Comum;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Compras.Domain.Fornecedores;

/// <summary>
/// Agregado raiz de fornecedor. <see cref="Documento"/> (CNPJ/CPF) é opcional — produtor rural e
/// fornecedor informal compram sem NF-e o tempo todo (nota <c>Manual</c>) — mas dedupe por
/// documento é responsabilidade do CASO DE USO (<c>CadastrarFornecedorUseCase</c>), nunca do
/// agregado: a lição real do gestao-raiz foi fundir fornecedores DISTINTOS porque os dois tinham
/// <c>documento == ""</c>. Aqui o agregado só valida a FORMA de um fornecedor individual; quem
/// decide "é o mesmo fornecedor de antes?" é a Application, e só quando o documento não é vazio.
/// </summary>
public sealed class Fornecedor : AggregateRoot<string>
{
    public string TenantId { get; private set; } = string.Empty;
    public string? Documento { get; private set; }
    public string RazaoSocial { get; private set; } = string.Empty;
    public string? NomeFantasia { get; private set; }
    public StatusFornecedor Status { get; private set; }

    private Fornecedor()
    {
    }

    public static Result<Fornecedor> Cadastrar(
        string tenantId, string razaoSocial, string? documento = null, string? nomeFantasia = null)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            return Result.Falhar<Fornecedor>(new Error("compras.fornecedor.tenant_invalido", "TenantId é obrigatório."));

        if (string.IsNullOrWhiteSpace(razaoSocial))
            return Result.Falhar<Fornecedor>(new Error("compras.fornecedor.razao_social_invalida", "Razão social é obrigatória."));

        return Result.Ok(new Fornecedor
        {
            Id = IdGenerator.NovoId(),
            TenantId = tenantId,
            Documento = string.IsNullOrWhiteSpace(documento) ? null : documento,
            RazaoSocial = razaoSocial,
            NomeFantasia = string.IsNullOrWhiteSpace(nomeFantasia) ? null : nomeFantasia,
            Status = StatusFornecedor.Ativo
        });
    }

    /// <summary>
    /// REIDRATAÇÃO a partir do banco — usada só pela camada de persistência (repositório). Não
    /// valida nem levanta evento: reconstrói o estado exato que foi persistido (mesmo padrão de
    /// <c>Assinatura.Reconstituir</c>, o molde da F0 para os demais agregados).
    /// </summary>
    public static Fornecedor Reconstituir(
        string id, string tenantId, string razaoSocial, string? documento, string? nomeFantasia, StatusFornecedor status)
        => new()
        {
            Id = id,
            TenantId = tenantId,
            RazaoSocial = razaoSocial,
            Documento = documento,
            NomeFantasia = nomeFantasia,
            Status = status
        };

    public Result Bloquear() => Transicionar(StatusFornecedor.Bloqueado);

    public Result Reativar() => Transicionar(StatusFornecedor.Ativo);

    public Result Inativar() => Transicionar(StatusFornecedor.Inativo);

    private Result Transicionar(StatusFornecedor destino)
    {
        var transicao = Fsm<StatusFornecedor>.ValidarTransicao(Status, destino, TransicoesPermitidas);
        if (transicao.Falha) return transicao;

        Status = destino;
        return Result.Ok();
    }

    private static readonly IReadOnlyDictionary<StatusFornecedor, StatusFornecedor[]> TransicoesPermitidas =
        new Dictionary<StatusFornecedor, StatusFornecedor[]>
        {
            [StatusFornecedor.Ativo] = [StatusFornecedor.Inativo, StatusFornecedor.Bloqueado],
            [StatusFornecedor.Inativo] = [StatusFornecedor.Ativo],
            [StatusFornecedor.Bloqueado] = [StatusFornecedor.Ativo]
        };
}
