using SistemaX.Modules.Financeiro.Application.Quant;

namespace SistemaX.Modules.Financeiro.Application.Ports;

/// <summary>
/// Persistência do mapeamento CONFIGURÁVEL POR TENANT de corrente de receita → anexo do Simples
/// Nacional (P0-4, docs/financeiro/revisao-domain-fit-cnpj.md). <see cref="ObterAsync"/> retorna
/// <c>null</c> quando o tenant nunca personalizou nada — o chamador (<c>RadarDoSimplesService</c>)
/// cai para <see cref="MapeamentoCorrenteAnexoPadrao.Obter"/> nesse caso; <c>null</c> nunca é
/// confundido com "lista vazia" (que seria "nenhuma corrente tributada", um estado válido mas
/// bem diferente de "sem configuração").
/// </summary>
public interface IConfiguracaoRadarSimplesRepository
{
    Task<IReadOnlyList<MapeamentoCorrenteAnexo>?> ObterAsync(string businessId, CancellationToken ct = default);

    Task SalvarAsync(string businessId, IReadOnlyList<MapeamentoCorrenteAnexo> mapeamento, CancellationToken ct = default);
}
