/**
 * DTO (.NET, `Money`/camelCase) → fatia do `VisaoGeralViewModel` (SDD em
 * `components/financial/visao-geral/types.ts`). Função pura, zero React — ver o padrão em
 * `docs/wiring/financeiro-api-contract.md` §9.1. Blocos com read-model real hoje: `disponivel`
 * (`QuantoSobrouDeVerdadeService`), `timeline` (`FluxoDeCaixaService`), `lucroDoMes`
 * (`DreGerencialService` + `ContasEmAbertoService`, ver docs/wiring/financeiro-telas-restantes.md)
 * e `proximosVencimentos` (reusa `GET /financeiro/extrato` — mesmo endpoint de Entradas & Saídas,
 * sem round-trip próprio).
 */
import type { DisponivelViewModel, LucroDoMesViewModel, ProximoVencimento, TimelineViewModel } from '@/components/financial/visao-geral/types';
import type { ContasEmAbertoDto, DisponivelParaRetiradaDto, DreDto, ExtratoLinhaDto, FluxoDeCaixaDto } from '@/lib/api/financeiro';

/** "Quanto sobrou de verdade" — hoje é 30 dias fixo de contas a pagar, sem imposto (o próprio
 * XML doc do serviço .NET admite: "fórmula do MVP, REDUZIDA"). O sublabel do mockup original
 * prometia "(15 dias + imposto)" — trocado aqui pra não afirmar o que o dado não confere (ver
 * docs/wiring/financeiro-api-contract.md §3, nota de rodapé). */
export function deDisponivelDto(dto: DisponivelParaRetiradaDto): DisponivelViewModel {
  return {
    livreDeVerdadeCentavos: dto.podeTirar.centavos,
    noBancoEGaveta: {
      label: 'No banco e na gaveta hoje',
      valorCentavos: dto.saldoEmCaixa.centavos,
      tone: 'pos',
      arrowLabel: 'Bancário →',
      drill: { rota: '/financeiro/bancario', mensagem: '→ Bancário — saldo por conta (banco + gaveta)' },
    },
    jaTemDono: {
      label: 'Já tem dono',
      sublabel: '(30 dias)',
      valorCentavos: -dto.jaTemDono.centavos,
      tone: 'crit',
      arrowLabel: 'E&S →',
      drill: {
        rota: '/financeiro/entradas-saidas',
        mensagem: '→ Entradas & Saídas — contas a pagar nos próximos 30 dias',
      },
    },
  };
}

/** Índice do último ponto REALIZADO (não projetado) — é "hoje" na timeline. */
function hojeIndexDe(pontos: FluxoDeCaixaDto['pontos']): number {
  let idx = 0;
  for (let i = 0; i < pontos.length; i++) {
    if (!pontos[i].projetado) idx = i;
  }
  return idx;
}

/**
 * `eventosPorDia` fica vazio de propósito: `PontoFluxoCaixa` (.NET) não carrega descrição de
 * origem por dia (precisaria juntar com `ContaAPagar.Descricao`/`ContaAReceber.Descricao` por
 * vencimento — read-model que ainda não existe, ver contrato §3). Preencher com os textos do mock
 * ("Aluguel", "Folha de pagamento"...) por cima de datas REAIS seria inventar dado — o tooltip cai
 * pro texto genérico "Sem vencimento grande" do componente em vez de mentir uma origem.
 */
export function deTimelineDto(dto: FluxoDeCaixaDto): TimelineViewModel {
  const valoresDiarios = dto.pontos.map((p) => p.saldoAcumulado.centavos);
  const datasISO = dto.pontos.map((p) => p.data);
  const hojeIndex = hojeIndexDe(dto.pontos);
  const mesLabel = (datasISO[hojeIndex] ?? datasISO[0] ?? '').split('-')[1] ?? '';

  return { valoresDiarios, hojeIndex, eventosPorDia: {}, mesLabel, datasISO };
}

/**
 * "Lucro do mês" (bloco ①b) — resultado de competência vem do MESMO `DreGerencialService` que
 * Entradas & Saídas/Relatórios já consomem (`atual`); o delta vs mês passado precisa de uma 2ª
 * chamada com o período anterior (`anteriorResultadoCentavos`, calculado no hook — nunca
 * duplicado aqui). `margemPorRealCentavos` ("de cada R$1 vendido, sobram R$X") é derivado de
 * `resultadoOperacional ÷ receitaBruta`, não um número separado no DTO. `aReceberCentavos` (a
 * ponte "lucro > disponível porque ainda falta receber") reusa `ContasEmAbertoService` — mesmo
 * dado que alimenta o card "Contas em aberto" de Relatórios, sem endpoint próprio.
 */
export function deLucroDoMesDto(atual: DreDto, anteriorResultadoCentavos: number, contasEmAberto: ContasEmAbertoDto): LucroDoMesViewModel {
  const lucroCentavos = atual.resultadoOperacional.centavos;
  const deltaAbsCentavos = lucroCentavos - anteriorResultadoCentavos;
  const deltaPercentual =
    anteriorResultadoCentavos !== 0 ? Math.round((deltaAbsCentavos / Math.abs(anteriorResultadoCentavos)) * 100) : 0;

  const receitaBrutaCentavos = atual.receitaBruta.centavos;
  const margemPorRealCentavos = receitaBrutaCentavos > 0 ? Math.round((lucroCentavos / receitaBrutaCentavos) * 100) : 0;

  return {
    lucroCentavos,
    deltaPercentual: Math.abs(deltaPercentual),
    deltaDirecao: deltaPercentual >= 0 ? 'up' : 'down',
    margemPorRealCentavos,
    aReceberCentavos: contasEmAberto.receberEmAberto.centavos,
    verDeOndeVeio: { rota: '/financeiro/entradas-saidas', mensagem: '→ Entradas & Saídas — composição do resultado do mês' },
  };
}

const DIAS_SEMANA_PT = ['dom', 'seg', 'ter', 'qua', 'qui', 'sex', 'sáb'];

/** "2026-07-18T00:00:00-03:00" ou "2026-07-18" → "sáb 18/07". Nunca `new Date(iso)` da string
 * inteira — extrai ano/mês/dia por componentes (determinístico, sem ambiguidade de timezone),
 * mesma diretriz de `adapters/financeiro/bancario.ts`/`entradasSaidas.ts`. */
function dataLabelDeIso(iso: string): string {
  const [ano, mes, dia] = iso
    .split('T')[0]
    .split('-')
    .map((p) => Number(p));
  if (!ano || !mes || !dia) return iso;
  const data = new Date(ano, mes - 1, dia);
  return `${DIAS_SEMANA_PT[data.getDay()]} ${String(dia).padStart(2, '0')}/${String(mes).padStart(2, '0')}`;
}

/**
 * "Próximos 7 dias" (bloco ④) — SEM endpoint próprio: reusa `GET /financeiro/extrato` (o mesmo
 * read-model de Entradas & Saídas), filtrando as linhas ainda não pagas (previsto/atrasado) dentro
 * da janela pedida pelo hook e ordenando por data. Itemizado e 100% real — nenhum campo do mock
 * fica sem fonte.
 */
export function deProximosVencimentosDeExtrato(dtos: ExtratoLinhaDto[]): ProximoVencimento[] {
  return [...dtos]
    .filter((l) => l.status !== 'pago')
    .sort((a, b) => a.data.localeCompare(b.data))
    .map((l) => {
      const tone: 'pos' | 'crit' = l.tipo === 'entrada' ? 'pos' : 'crit';
      const sinal = l.tipo === 'entrada' ? 1 : -1;
      const dataLabel = dataLabelDeIso(l.data);
      return {
        dataLabel,
        valorCentavos: sinal * l.valor.centavos,
        tone,
        descricao: l.descricao,
        drill: {
          rota: '/financeiro/entradas-saidas',
          mensagem: `→ Entradas & Saídas — ${l.descricao}, vence ${dataLabel.split(' ')[1] ?? dataLabel}`,
        },
      };
    });
}
