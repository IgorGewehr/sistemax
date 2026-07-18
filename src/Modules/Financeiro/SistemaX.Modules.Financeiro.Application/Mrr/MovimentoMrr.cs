using SistemaX.Modules.Financeiro.Domain.Comum;

namespace SistemaX.Modules.Financeiro.Application.Mrr;

/// <summary>
/// P1-4 (docs/financeiro/revisao-domain-fit-cnpj.md) — os cinco tipos de movimento que decompõem a
/// variação do MRR mês a mês. Valores PINADOS (persistidos como INTEGER — nunca reordenar, mesma
/// regra de <c>CorrenteDeReceita</c>).
/// </summary>
public enum TipoMovimentoMrr
{
    /// <summary>Assinatura nasceu — <c>AssinaturaCriada</c>.</summary>
    Novo = 0,

    /// <summary>Troca de plano/valor PRA CIMA — <c>AssinaturaAlterada</c> com delta positivo.</summary>
    Expansao = 1,

    /// <summary>Troca de plano/valor PRA BAIXO, ou pausa (o MRR retirado por inteiro) —
    /// <c>AssinaturaAlterada</c> com delta negativo, ou <c>AssinaturaPausada</c>.</summary>
    Contracao = 2,

    /// <summary>Cancelamento — <c>AssinaturaCancelada</c>. Magnitude 0 (nenhum movimento
    /// registrado) se a assinatura já estava Pausada, para não duplo-descontar um MRR que já
    /// tinha saído via Contração.</summary>
    Churn = 3,

    /// <summary>Assinatura pausada volta a contar — <c>AssinaturaReativada</c>.</summary>
    Reativacao = 4
}

/// <summary>
/// Um MOVIMENTO de MRR — um lançamento no ledger derivado dos eventos de domínio de
/// <c>Assinatura</c> (P1-4). É o "event-sourcing" do MRR em si: a soma de TODOS os movimentos de
/// um tenant, em qualquer ponto do tempo, é POR CONSTRUÇÃO igual ao MRR corrente real (soma de
/// <c>Assinatura.Mrr</c> das assinaturas Ativas/Inadimplentes) — a invariante testada
/// <c>MRR_fim = MRR_início + Novo + Expansão − Contração − Churn + Reativação</c> não é mais que
/// esta soma cumulativa fatiada por mês. <see cref="ValorCentavos"/> é sempre uma MAGNITUDE
/// positiva — o sinal de cada tipo é aplicado pelo LEITOR (<c>PainelDeMovimentosMrrService</c>),
/// nunca guardado aqui, para o dado bruto nunca mentir sobre "quanto" só porque o tipo já diz "pra
/// que lado".
/// </summary>
public sealed record MovimentoMrr(
    string Id, string BusinessId, string AssinaturaId, string ServicoId,
    TipoMovimentoMrr Tipo, long ValorCentavos, DateOnly Competencia, DateTimeOffset OcorridoEm)
{
    // Competencia é normalizada para o dia 1 do mês de OcorridoEm — granularidade de MÊS (mesma
    // convenção de Assinatura.ProximaCompetenciaDevida/CronogramaLinear), nunca o dia exato: dois
    // movimentos no mesmo mês em dias diferentes precisam cair no MESMO bucket para
    // PainelDeMovimentosMrrService agrupar certo.
    public static MovimentoMrr Novo(string businessId, string assinaturaId, string servicoId, long valorCentavos, DateTimeOffset ocorridoEm)
        => Criar(TipoMovimentoMrr.Novo, businessId, assinaturaId, servicoId, valorCentavos, ocorridoEm);

    public static MovimentoMrr Expansao(string businessId, string assinaturaId, string servicoId, long valorCentavos, DateTimeOffset ocorridoEm)
        => Criar(TipoMovimentoMrr.Expansao, businessId, assinaturaId, servicoId, valorCentavos, ocorridoEm);

    public static MovimentoMrr Contracao(string businessId, string assinaturaId, string servicoId, long valorCentavos, DateTimeOffset ocorridoEm)
        => Criar(TipoMovimentoMrr.Contracao, businessId, assinaturaId, servicoId, valorCentavos, ocorridoEm);

    public static MovimentoMrr Churn(string businessId, string assinaturaId, string servicoId, long valorCentavos, DateTimeOffset ocorridoEm)
        => Criar(TipoMovimentoMrr.Churn, businessId, assinaturaId, servicoId, valorCentavos, ocorridoEm);

    public static MovimentoMrr Reativacao(string businessId, string assinaturaId, string servicoId, long valorCentavos, DateTimeOffset ocorridoEm)
        => Criar(TipoMovimentoMrr.Reativacao, businessId, assinaturaId, servicoId, valorCentavos, ocorridoEm);

    private static MovimentoMrr Criar(TipoMovimentoMrr tipo, string businessId, string assinaturaId, string servicoId, long valorCentavos, DateTimeOffset ocorridoEm)
        => new(
            IdGenerator.NovoId(), businessId, assinaturaId, servicoId, tipo,
            Math.Abs(valorCentavos), new DateOnly(ocorridoEm.Year, ocorridoEm.Month, 1), ocorridoEm);
}
