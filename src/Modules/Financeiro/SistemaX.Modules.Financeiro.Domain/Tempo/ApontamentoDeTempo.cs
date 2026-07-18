using SistemaX.Modules.Financeiro.Domain.Comum;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Domain.Tempo;

/// <summary>
/// APONTAMENTO DE TEMPO — registro leve/operacional de minutos gastos num atendimento (design-pai
/// §3.4). DECISÃO TRAVADA DO DONO para esta fatia (P4): SÓ MINUTOS — sem custo/hora. O campo
/// <see cref="CustoHoraCentavosSnapshot"/> existe PREPARADO no shape (mesmo grão do design, que
/// previa resolução global/por-técnico + snapshot congelado na criação — §5.2) mas nenhum caminho
/// de código desta fatia o resolve/preenche: fica sempre <c>null</c>. Valorização em R$ (custo/hora,
/// <c>ConfiguracaoFinanceiraTenant.CustoHoraPadraoCentavos</c>, override por técnico) é decisão
/// adiada para uma fatia futura — o painel e o resumo de gargalo desta fatia trabalham só com
/// MINUTOS.
///
/// Sem FSM, sem lançamento contábil, DELETE FÍSICO permitido (design §3.4): é registro
/// operacional/gerencial, não fato contábil — nunca toca <c>LancamentoContabil</c>, nunca entra no
/// DRE (R1 desta fatia: tempo não dobra com folha — <c>FolhaLancada</c> já lança o custo real do
/// técnico via <c>ContaAPagar</c>).
/// </summary>
public sealed class ApontamentoDeTempo : AggregateRoot<string>
{
    public string BusinessId { get; }
    public string? ProjetoId { get; }
    public string? ClienteId { get; }

    /// <summary>Audit-field (padrão de <c>OperadorNome</c>/<c>Assinatura.ClienteNome</c> — CLAUDE.md
    /// §6): denormalizado na criação (do request, ou derivado de <c>Assinatura.ClienteNome</c> quando
    /// vem via <see cref="AssinaturaId"/>) para o resumo de gargalo (§9.7) não precisar de lookup
    /// cross-módulo — Financeiro não tem <c>IClienteRepository</c>.</summary>
    public string? ClienteNome { get; }

    public string? AssinaturaId { get; }
    public string? OrdemServicoId { get; }
    public int Minutos { get; }
    public DateTimeOffset Data { get; }
    public string OperadorId { get; }
    public string OperadorNome { get; }
    public string? Descricao { get; }

    /// <summary>PREPARADO/NULO nesta fatia — ver o sumário da classe. Persistido para não exigir
    /// outra migração quando a valorização em R$ chegar.</summary>
    public long? CustoHoraCentavosSnapshot { get; }

    public DateTimeOffset CriadoEm { get; }

    private ApontamentoDeTempo(
        string id, string businessId, string? projetoId, string? clienteId, string? clienteNome, string? assinaturaId,
        string? ordemServicoId, int minutos, DateTimeOffset data, string operadorId, string operadorNome, string? descricao,
        long? custoHoraCentavosSnapshot, DateTimeOffset criadoEm)
    {
        Id = id;
        BusinessId = businessId;
        ProjetoId = projetoId;
        ClienteId = clienteId;
        ClienteNome = clienteNome;
        AssinaturaId = assinaturaId;
        OrdemServicoId = ordemServicoId;
        Minutos = minutos;
        Data = data;
        OperadorId = operadorId;
        OperadorNome = operadorNome;
        Descricao = descricao;
        CustoHoraCentavosSnapshot = custoHoraCentavosSnapshot;
        CriadoEm = criadoEm;
    }

    public static Result<ApontamentoDeTempo> Criar(
        string businessId, int minutos, DateTimeOffset data, string operadorId, string operadorNome, DateTimeOffset criadoEm,
        string? projetoId = null, string? clienteId = null, string? clienteNome = null, string? assinaturaId = null,
        string? ordemServicoId = null, string? descricao = null, long? custoHoraCentavosSnapshot = null)
    {
        if (string.IsNullOrWhiteSpace(businessId))
            return Result.Falhar<ApontamentoDeTempo>(new Error("financeiro.apontamento.business_obrigatorio", "BusinessId é obrigatório."));

        if (minutos <= 0)
            return Result.Falhar<ApontamentoDeTempo>(new Error("financeiro.apontamento.minutos_invalidos", "Minutos deve ser positivo."));

        if (string.IsNullOrWhiteSpace(operadorId) || string.IsNullOrWhiteSpace(operadorNome))
            return Result.Falhar<ApontamentoDeTempo>(new Error("financeiro.apontamento.operador_obrigatorio", "Operador é obrigatório."));

        if (projetoId is null && clienteId is null && assinaturaId is null && ordemServicoId is null)
            return Result.Falhar<ApontamentoDeTempo>(new Error(
                "financeiro.apontamento.sem_vinculo",
                "Apontamento precisa de ao menos um vínculo (projeto, cliente, assinatura ou ordem de serviço)."));

        var apontamento = new ApontamentoDeTempo(
            IdGenerator.NovoId(), businessId, projetoId, clienteId, clienteNome, assinaturaId, ordemServicoId, minutos, data,
            operadorId, operadorNome, descricao, custoHoraCentavosSnapshot, criadoEm);

        apontamento.Raise(new ApontamentoDeTempoRegistrado(apontamento.Id, businessId, projetoId, clienteId, minutos, data));
        return Result.Ok(apontamento);
    }

    /// <summary>REIDRATAÇÃO a partir do banco — não valida, não levanta evento.</summary>
    public static ApontamentoDeTempo Reconstituir(
        string id, string businessId, string? projetoId, string? clienteId, string? clienteNome, string? assinaturaId,
        string? ordemServicoId, int minutos, DateTimeOffset data, string operadorId, string operadorNome, string? descricao,
        long? custoHoraCentavosSnapshot, DateTimeOffset criadoEm)
        => new(id, businessId, projetoId, clienteId, clienteNome, assinaturaId, ordemServicoId, minutos, data, operadorId, operadorNome, descricao, custoHoraCentavosSnapshot, criadoEm);

    /// <summary>Custo derivado — <c>round_banker(minutos × snapshot / 60)</c> (design §3.4). Sempre
    /// <c>null</c> nesta fatia (snapshot nunca resolvido), mas a fórmula já nasce pronta para quando
    /// a valorização em R$ chegar.</summary>
    public long? CustoCentavos => CustoHoraCentavosSnapshot is { } taxa
        ? (long)Math.Round(Minutos * taxa / 60m, MidpointRounding.ToEven)
        : null;
}
