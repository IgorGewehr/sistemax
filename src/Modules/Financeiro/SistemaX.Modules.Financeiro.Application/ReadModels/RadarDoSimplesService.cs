using SistemaX.Modules.Financeiro.Application.Categorias;
using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Application.Quant;
using SistemaX.Modules.Financeiro.Domain.Comum;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Application.ReadModels;

public sealed record RadarPorAnexoResultado(
    AnexoSimplesNacional Anexo, long ReceitaMesCentavos, int FaixaAtual, double AliquotaEfetiva, long ImpostoEstimadoCentavos);

public sealed record RadarDoSimplesResultado(
    long Rbt12Centavos,
    int FaixaAtual,
    double AliquotaEfetiva,
    double AliquotaNominalFaixaAtual,
    long DistanciaAoProximoDegrauCentavos,
    int? MesesProjetadosAteOProximoDegrau,
    double FatorR,
    IReadOnlyList<RadarPorAnexoResultado> PorAnexo,
    long ImpostoTotalEstimadoCentavos);

/// <summary>
/// Orquestra <see cref="RadarDoSimplesNacional"/> sobre <c>fato_receita_diaria</c> — catálogo #4 do
/// plano de inteligência do Financeiro (docs/financeiro/inteligencia-arquitetura.md/ADR-0005).
///
/// RBT12 (receita bruta dos últimos 12 meses) = soma de <c>fato_receita_diaria</c> nos
/// <see cref="MesesParaTendencia"/> MESES CALENDÁRIO FECHADOS anteriores ao mês corrente (P2-1,
/// docs/financeiro/revisao-domain-fit-cnpj.md — LC 123/2006 art. 3º §1º: "receita bruta acumulada
/// nos 12 meses ANTERIORES ao mês do período de apuração"), de TODAS as correntes
/// (Comercio/Servico/Recorrente) — P0-4: antes só via receita de venda/pedido/OS; agora
/// <c>CobrancaDeAssinaturaGerada</c> também folda ali, fechando o gap de assinatura nunca entrar
/// no RBT12. BUG ANTIGO (P2-1): somava 365 dias corridos TERMINANDO HOJE — incluía o mês corrente
/// (ainda não fechado) e fazia o RBT12 oscilar dia a dia dentro do próprio mês, e não bater com a
/// definição legal de "meses fechados". A janela agora é EXATAMENTE a mesma que a projeção de
/// cruzamento de faixa (P1-1) já rolava — <see cref="CarregarReceitaMensalRecenteAsync"/> é
/// reusada como fonte ÚNICA dos dois números, nunca duas definições de "12 meses" divergentes no
/// mesmo read-model.
///
/// P0-4 — MULTI-ANEXO: <see cref="AnexoSimplesNacional.I"/> (Comércio), <see cref="AnexoSimplesNacional.III"/>
/// e <see cref="AnexoSimplesNacional.V"/> (Serviço, decidido pelo Fator R — ver <see cref="FatorR"/>)
/// já têm tabela; <paramref name="anexo"/>, quando informado, é só a "lupa" sobre UM anexo
/// específico (mantém o contrato antigo do endpoint) — o mix real do tenant (repartição por
/// corrente, <c>PorAnexo</c>, DAS total) é SEMPRE calculado, independente do parâmetro.
/// </summary>
public sealed class RadarDoSimplesService(
    IFatoReceitaDiariaRepository fatoReceitaDiaria,
    IContaAPagarRepository contasAPagar,
    IConfiguracaoRadarSimplesRepository configuracaoRadar,
    IRelogio relogio)
{
    private const int MesesParaTendencia = 12;

    private static readonly HashSet<AnexoSimplesNacional> AnexosSuportados =
        [AnexoSimplesNacional.I, AnexoSimplesNacional.III, AnexoSimplesNacional.V];

    public async Task<Result<RadarDoSimplesResultado>> CalcularAsync(string businessId, AnexoSimplesNacional? anexo = null, CancellationToken ct = default)
    {
        if (anexo is { } solicitado && !AnexosSuportados.Contains(solicitado))
        {
            return Result.Falhar<RadarDoSimplesResultado>(new Error(
                "financeiro.radar_simples.anexo_nao_suportado",
                $"Anexo {solicitado} ainda não está implementado — só I, III e V (assistência técnica: comércio × serviço com Fator R)."));
        }

        var hoje = DateOnly.FromDateTime(relogio.Agora().UtcDateTime);
        var inicioDoMesCorrente = new DateOnly(hoje.Year, hoje.Month, 1);

        // RBT12 (P2-1) = soma dos MesesParaTendencia meses calendário FECHADOS — a mesma janela e
        // a mesma consulta que a projeção de cruzamento de faixa (P1-1) já carrega; somar aqui de
        // novo é a definição legal em si, não uma segunda fonte de verdade.
        var receitaMensalRecente = await CarregarReceitaMensalRecenteAsync(businessId, hoje, ct).ConfigureAwait(false);
        var rbt12 = receitaMensalRecente.Sum();

        var inicioJanelaFechada = new DateTimeOffset(inicioDoMesCorrente.AddMonths(-MesesParaTendencia).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var fimJanelaFechada = new DateTimeOffset(inicioDoMesCorrente.AddDays(-1).ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero);
        var folhaDozeMeses = await CarregarFolhaDozeMesesAsync(businessId, inicioJanelaFechada, fimJanelaFechada, ct).ConfigureAwait(false);
        var fatorR = FatorR.Calcular(folhaDozeMeses, rbt12);

        var receitaMesPorCorrente = await CarregarReceitaDoMesPorCorrenteAsync(businessId, hoje, ct).ConfigureAwait(false);

        var mapeamento = await configuracaoRadar.ObterAsync(businessId, ct).ConfigureAwait(false) ?? MapeamentoCorrenteAnexoPadrao.Obter();

        var mix = RadarDoSimplesNacional.CalcularMix(rbt12, receitaMensalRecente, fatorR, receitaMesPorCorrente, mapeamento);

        // Anexo "em foco": o solicitado explicitamente (contrato antigo do endpoint) ou, na falta
        // dele (Consultor — P0-4, nunca mais hardcoded), o anexo DOMINANTE do mix real (maior
        // receita do mês) — cai para o Anexo I só quando o tenant não tem receita nenhuma ainda.
        var anexoEmFoco = anexo ?? mix.PorAnexo
            .OrderByDescending(p => p.ReceitaMesCentavos)
            .Select(p => (AnexoSimplesNacional?)p.Anexo)
            .FirstOrDefault() ?? AnexoSimplesNacional.I;

        var tabelaEmFoco = RadarDoSimplesNacional.ObterTabela(anexoEmFoco);
        var faixaEmFoco = tabelaEmFoco[mix.FaixaAtual - 1];
        var aliquotaEmFoco = RadarDoSimplesNacional.CalcularAliquotaEfetiva(rbt12, faixaEmFoco);

        var porAnexo = mix.PorAnexo
            .Select(p => new RadarPorAnexoResultado(p.Anexo, p.ReceitaMesCentavos, p.FaixaNumero, p.AliquotaEfetiva, p.ImpostoEstimadoCentavos))
            .ToList();

        return Result.Ok(new RadarDoSimplesResultado(
            mix.Rbt12Centavos,
            mix.FaixaAtual,
            aliquotaEmFoco,
            faixaEmFoco.AliquotaNominal,
            mix.DistanciaAoProximoDegrauCentavos,
            mix.MesesProjetadosAteOProximoDegrau,
            mix.FatorR,
            porAnexo,
            mix.ImpostoTotalEstimadoCentavos));
    }

    /// <summary>Totais mensais (ano-mês → soma de receita, de TODAS as correntes) dos últimos
    /// <see cref="MesesParaTendencia"/> meses FECHADOS (não inclui o mês corrente parcial — misturar
    /// um mês incompleto com meses inteiros distorceria a tendência de crescimento). 12 meses, não
    /// 6 (P1-1) — é a mesma janela real do RBT12 que a projeção de cruzamento de faixa rola.</summary>
    private async Task<IReadOnlyList<long>> CarregarReceitaMensalRecenteAsync(string businessId, DateOnly hoje, CancellationToken ct)
    {
        var inicioDoMesCorrente = new DateOnly(hoje.Year, hoje.Month, 1);
        var inicioDaJanela = inicioDoMesCorrente.AddMonths(-MesesParaTendencia);
        var fimDaJanela = inicioDoMesCorrente.AddDays(-1); // último dia do mês fechado anterior

        var fatos = await fatoReceitaDiaria.ListarAsync(businessId, inicioDaJanela, fimDaJanela, ct).ConfigureAwait(false);

        return fatos
            .GroupBy(f => new DateOnly(f.Dia.Year, f.Dia.Month, 1))
            .OrderBy(g => g.Key)
            .Select(g => g.Sum(f => f.ReceitaCentavos))
            .ToList();
    }

    /// <summary>Receita do mês CORRENTE (em andamento, dia 1 até hoje) por corrente — a base de
    /// apuração do DAS estimado (P0-4): repartir por anexo é repartir a receita do MÊS que está
    /// sendo tributado, não o RBT12 acumulado (que só decide a FAIXA/alíquota efetiva).</summary>
    private async Task<IReadOnlyDictionary<CorrenteDeReceita, long>> CarregarReceitaDoMesPorCorrenteAsync(string businessId, DateOnly hoje, CancellationToken ct)
    {
        var inicioDoMes = new DateOnly(hoje.Year, hoje.Month, 1);
        var fatos = await fatoReceitaDiaria.ListarAsync(businessId, inicioDoMes, hoje, ct).ConfigureAwait(false);

        return fatos.GroupBy(f => f.Corrente).ToDictionary(g => g.Key, g => g.Sum(f => f.ReceitaCentavos));
    }

    /// <summary>Folha de salários (categoria <see cref="CategoriaFinanceiraPadrao.DespesaComPessoal"/>)
    /// na MESMA janela de 12 meses do RBT12 — Fator R exige os dois numeradores na mesma janela
    /// (LC 123/2006 art. 18 §5º-J). A folha nasce de <c>FolhaLancada</c> via <c>FolhaLancadaHandler</c>,
    /// já uma <c>ContaAPagar</c> no ledger — nenhuma fact table nova precisa nascer para isso.</summary>
    private async Task<long> CarregarFolhaDozeMesesAsync(string businessId, DateTimeOffset inicio, DateTimeOffset fim, CancellationToken ct)
    {
        var contas = await contasAPagar.ListarPorCompetenciaAsync(businessId, inicio, fim, ct).ConfigureAwait(false);
        return contas.Where(c => c.CategoriaId == CategoriaFinanceiraPadrao.DespesaComPessoal).Sum(c => c.ValorTotal.Centavos);
    }
}
