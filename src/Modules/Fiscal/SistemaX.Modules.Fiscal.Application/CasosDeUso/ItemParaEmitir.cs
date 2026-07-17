using SistemaX.Modules.Fiscal.Domain.Comum;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Fiscal.Application.CasosDeUso;

/// <summary>Linha de entrada para <see cref="EmitirDocumentoFiscalUseCase"/> — dado ainda cru
/// (preço/quantidade/desconto), a tributação em si é resolvida por
/// <c>ResolvedorDeItemFiscalService</c>/<c>MotorDeCalculoTributario</c>, nunca calculada aqui.
/// <see cref="CfopDaEmissao"/> é o nível mais alto da cadeia de resolução de CFOP (emissão &gt;
/// produto &gt; padrão-config, decisão de Igor) — normalmente nulo (deixa a cadeia decidir);
/// preenchido só quando o operador força um CFOP específico nesta emissão.</summary>
public sealed record ItemParaEmitir(
    string ProdutoId,
    string Descricao,
    string Ncm,
    Quantidade Quantidade,
    Money PrecoUnitario,
    Money Desconto,
    string? CfopDaEmissao = null);
