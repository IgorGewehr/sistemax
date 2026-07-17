using SistemaX.Modules.Financeiro.Application.Caixa;
using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Domain.Caixa;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Application.ReadModels;

/// <summary>Amostra fixa (ilustrativa, não paginada) do balde "Bateu certinho".</summary>
public sealed record ItemBatidoAmostraResumo(DateTimeOffset Data, string Descricao);

/// <summary>Um item pendente num dos dois baldes de sobra. <see cref="Id"/> é o id do lado que
/// SOBROU (o <c>ExtratoBancarioItem</c> em "sobrou no banco", o <c>MovimentoFinanceiro</c> em
/// "sobrou no sistema"); <see cref="IdSugerido"/> é o id do MELHOR candidato do lado oposto —
/// já pronto pra virar o par de <c>POST /financeiro/conciliacao</c> — ou <c>null</c> quando a
/// heurística não encontrou nenhum candidato plausível.</summary>
public sealed record ItemConciliacaoPendenteResumo(string Id, DateTimeOffset Data, string Descricao, Money Valor, string? Sugestao, string? IdSugerido);

public sealed record ConciliacaoBancariaResumo(
    int BateuCertinhoTotal,
    IReadOnlyList<ItemBatidoAmostraResumo> BateuCertinhoAmostra,
    IReadOnlyList<ItemConciliacaoPendenteResumo> SobrouNoBanco,
    IReadOnlyList<ItemConciliacaoPendenteResumo> SobrouNoSistema);

/// <summary>
/// Os "3 baldes" da tela Bancário (docs/wiring/financeiro-telas-restantes.md §3): cruza o extrato
/// importado (<see cref="ExtratoBancarioItem"/>) com o ledger interno (<see cref="MovimentoFinanceiro"/>)
/// via <see cref="Conciliacao"/> — o que já foi confirmado (auto ou manual) é "bateu certinho"; o
/// que sobra de cada lado (sem confirmação nem descarte explícito) vira "sobrou no banco"/"sobrou
/// no sistema", cada um com uma SUGESTÃO heurística (mesmo valor absoluto, data mais próxima do
/// lado oposto) — é matching determinístico, não o Super Consultor (Lei 2: quem confirma/ignora é
/// o usuário, ver <c>ConciliarMovimentoUseCase</c>).
/// </summary>
public sealed class ConciliacaoBancariaService(
    IContaBancariaCaixaRepository contas,
    IExtratoBancarioItemRepository itensExtrato,
    IConciliacaoRepository conciliacoes,
    IMovimentoFinanceiroRepository movimentos,
    ResolvedorDeDescricaoDeMovimento resolvedorDescricao)
{
    private const int TamanhoAmostra = 3;

    public async Task<ConciliacaoBancariaResumo> CalcularAsync(
        string businessId, DateTimeOffset inicio, DateTimeOffset fim, CancellationToken ct = default)
    {
        var todasAsContas = await contas.ListarAsync(businessId, ct: ct).ConfigureAwait(false);
        var itensDoPeriodo = new List<ExtratoBancarioItem>();
        foreach (var conta in todasAsContas)
        {
            var doConta = await itensExtrato.ListarNaoConciliadosAsync(businessId, conta.Id, ct).ConfigureAwait(false);
            itensDoPeriodo.AddRange(doConta.Where(i => i.Data >= inicio && i.Data <= fim));
        }

        var movimentosDoPeriodo = (await movimentos.ListarPorPeriodoAsync(businessId, inicio, fim, ct).ConfigureAwait(false))
            .Where(m => !m.EhEstorno)
            .ToList();

        var todasConciliacoes = await conciliacoes.ListarPorBusinessIdAsync(businessId, ct).ConfigureAwait(false);
        var extratoConfirmadoIds = ConjuntoDe(todasConciliacoes, StatusConciliacao.ConciliadoAuto, StatusConciliacao.ConciliadoManual, c => c.ExtratoBancarioItemId);
        var extratoIgnoradoIds = ConjuntoDe(todasConciliacoes, StatusConciliacao.Ignorado, StatusConciliacao.Ignorado, c => c.ExtratoBancarioItemId);
        var movimentoConfirmadoIds = ConjuntoDe(todasConciliacoes, StatusConciliacao.ConciliadoAuto, StatusConciliacao.ConciliadoManual, c => c.MovimentoFinanceiroId);
        var movimentoIgnoradoIds = ConjuntoDe(todasConciliacoes, StatusConciliacao.Ignorado, StatusConciliacao.Ignorado, c => c.MovimentoFinanceiroId);

        var bateuCertinho = itensDoPeriodo.Where(i => extratoConfirmadoIds.Contains(i.Id)).OrderByDescending(i => i.Data).ToList();
        var amostra = bateuCertinho.Take(TamanhoAmostra).Select(i => new ItemBatidoAmostraResumo(i.Data, i.Descricao)).ToList();

        var candidatosMovimento = movimentosDoPeriodo.Where(m => !movimentoConfirmadoIds.Contains(m.Id)).ToList();
        var sobrouNoBanco = new List<ItemConciliacaoPendenteResumo>();
        foreach (var item in itensDoPeriodo.Where(i => !extratoConfirmadoIds.Contains(i.Id) && !extratoIgnoradoIds.Contains(i.Id)).OrderByDescending(i => i.Data))
        {
            var candidato = MelhorCandidato(item.Valor.Centavos, item.Data, candidatosMovimento, m => m.Valor.Centavos * SinalDe(m.Tipo), m => m.DataMovimento);
            var sugestao = candidato is null
                ? null
                : $"Pode ser \"{await resolvedorDescricao.ResolverAsync(candidato, ct).ConfigureAwait(false)}\" ({candidato.DataMovimento:dd/MM}, mesmo valor).";
            sobrouNoBanco.Add(new ItemConciliacaoPendenteResumo(item.Id, item.Data, item.Descricao, item.Valor, sugestao, candidato?.Id));
        }

        var candidatosExtrato = itensDoPeriodo.Where(i => !extratoConfirmadoIds.Contains(i.Id)).ToList();
        var sobrouNoSistema = new List<ItemConciliacaoPendenteResumo>();
        foreach (var movimento in movimentosDoPeriodo.Where(m => !movimentoConfirmadoIds.Contains(m.Id) && !movimentoIgnoradoIds.Contains(m.Id)).OrderByDescending(m => m.DataMovimento))
        {
            var valorComSinal = movimento.Valor.Centavos * SinalDe(movimento.Tipo);
            var candidato = MelhorCandidato(valorComSinal, movimento.DataMovimento, candidatosExtrato, i => i.Valor.Centavos, i => i.Data);
            var descricao = await resolvedorDescricao.ResolverAsync(movimento, ct).ConfigureAwait(false);
            var sugestao = candidato is null
                ? null
                : $"Pode ser \"{candidato.Descricao}\" no extrato ({candidato.Data:dd/MM}, mesmo valor).";
            var valor = movimento.Tipo == TipoMovimentoFinanceiro.Entrada ? movimento.Valor : -movimento.Valor;
            sobrouNoSistema.Add(new ItemConciliacaoPendenteResumo(movimento.Id, movimento.DataMovimento, descricao, valor, sugestao, candidato?.Id));
        }

        return new ConciliacaoBancariaResumo(bateuCertinho.Count, amostra, sobrouNoBanco, sobrouNoSistema);
    }

    private static int SinalDe(TipoMovimentoFinanceiro tipo) => tipo == TipoMovimentoFinanceiro.Entrada ? 1 : -1;

    private static HashSet<string> ConjuntoDe(
        IReadOnlyList<Conciliacao> conciliacoes, StatusConciliacao statusA, StatusConciliacao statusB, Func<Conciliacao, string> seletor)
        => conciliacoes.Where(c => c.Status == statusA || c.Status == statusB).Select(seletor).ToHashSet();

    /// <summary>Heurística de match: mesmo valor absoluto (com sinal já normalizado pelo chamador),
    /// desempatado pela data mais próxima. Nenhuma IA envolvida — é comparação determinística de
    /// dois números (Lei 2).</summary>
    private static TCandidato? MelhorCandidato<TCandidato>(
        long valorComSinal, DateTimeOffset data, IReadOnlyList<TCandidato> candidatos, Func<TCandidato, long> valorDoCandidato, Func<TCandidato, DateTimeOffset> dataDoCandidato)
        where TCandidato : class
        => candidatos
            .Where(c => valorDoCandidato(c) == valorComSinal)
            .OrderBy(c => Math.Abs((dataDoCandidato(c) - data).Ticks))
            .FirstOrDefault();
}
