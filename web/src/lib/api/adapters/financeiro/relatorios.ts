/**
 * DTO (.NET, `Money`/camelCase) → fatias REAIS do view-model de Relatórios
 * (`components/financial/relatorios/types.ts`), ver docs/wiring/financeiro-telas-restantes.md §5
 * (task #33). Funções puras, zero React.
 */
import type { AbertoViewModel, AgingBucket, DreRegimeBlock, MrrViewModel } from '@/components/financial/relatorios/types';
import type { ContasEmAbertoDto, DreDto, ReceitaRecorrenteDto } from '@/lib/api/financeiro';

/** Cores decrescentes de severidade — mesma escala do mockup (0–15d mais claro, +30d = `crit`).
 * Aplicada por ORDEM (não por `id`): o backend devolve sempre 3 baldes nessa sequência
 * (`ContasEmAbertoService`), mas o `id` real (`"30+"`) difere do `id` ilustrativo do mockup (`"+30"`). */
const AGING_CORES = ['hsl(var(--warn) / 0.55)', 'hsl(var(--warn))', 'hsl(var(--crit))'];

export function deContasEmAbertoDto(dto: ContasEmAbertoDto): AbertoViewModel {
  const agingBuckets: AgingBucket[] = dto.agingBuckets.map((b, i) => ({
    id: b.id,
    label: b.label,
    amountCentavos: b.valor.centavos,
    colorVar: AGING_CORES[i] ?? AGING_CORES[AGING_CORES.length - 1],
  }));

  return {
    docLabel: 'Contas em aberto',
    receberEmAberto: dto.receberEmAberto.centavos,
    receberAtrasado: dto.receberAtrasado.centavos,
    pagarEmAberto: dto.pagarEmAberto.centavos,
    agingBuckets,
  };
}

export function deReceitaRecorrenteParaMrr(dto: ReceitaRecorrenteDto): MrrViewModel {
  return {
    docLabel: 'Relatório MRR',
    condicaoLabel: 'Visível porque vende serviço recorrente',
    mrr: dto.mrr.centavos,
    churnMes: dto.mrrChurnNoMes.centavos,
    arrEstimado: dto.arr.centavos,
  };
}

/** DRE gerencial REAL, regime de COMPETÊNCIA — `GET /financeiro/relatorios/dre`
 * (`DreGerencialService`). Taxonomia diverge do mockup original: o serviço não separa "Impostos" de
 * "Despesas e custos" (só `custoDireto` = CMV real + comissões, e `despesaOperacional` = o resto) —
 * por isso as 2 linhas de dedução usam os nomes REAIS do agrupamento, não os do mockup (ver
 * docs/wiring/financeiro-telas-restantes.md §5, "atenção à taxonomia"). Regime de caixa não existe
 * no backend ainda — sem bridge note fabricada, só a explicação do regime. */
export function deDreCompetenciaDto(atual: DreDto, anteriorResultadoCentavos: number, mesAnteriorLabel: string): DreRegimeBlock {
  const resultadoCentavos = atual.resultadoOperacional.centavos;
  const deltaAbsCentavos = resultadoCentavos - anteriorResultadoCentavos;
  const deltaPct = anteriorResultadoCentavos !== 0 ? Math.round((deltaAbsCentavos / Math.abs(anteriorResultadoCentavos)) * 100) : 0;
  const subiu = deltaPct >= 0;

  return {
    regimeLabel: 'competência',
    topLine: { label: 'Receita bruta', valueCentavos: atual.receitaBruta.centavos },
    deductionLines: [
      { label: '(–) Custo direto (CMV + comissões)', valueCentavos: atual.custoDireto.centavos },
      { label: '(–) Despesas operacionais', valueCentavos: atual.despesaOperacional.centavos },
    ],
    totalLine: { label: 'Resultado do mês', valueCentavos: resultadoCentavos },
    delta: {
      direction: subiu ? 'up' : 'down',
      label: `${subiu ? '▲' : '▼'} ${Math.abs(deltaPct)}% vs ${mesAnteriorLabel}`,
    },
    bridgeNote: [
      { text: 'Regime de competência: conta o que foi vendido/gasto no mês, mesmo que o dinheiro ainda não tenha mudado de mão — ' },
      { text: 'veja "Como fecha o mês" em Entradas & Saídas', bold: true },
      { text: ' para a projeção de caixa do período.' },
    ],
  };
}
