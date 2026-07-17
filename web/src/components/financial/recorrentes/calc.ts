/**
 * Cálculos derivados da tela Recorrentes — puros e testáveis, espelham 1:1 a lógica
 * de `docs/ui/mockups/recorrentes.html` (funções `renderKpisFixas`, `overviewFixas`,
 * `drillFixas`, `anosDesde`, etc.). Nenhum destes valores é hardcoded no mock —
 * o mock só guarda o histórico bruto; tudo que é "derivado" nasce aqui.
 */
import type { Centavos } from '@/lib/money';

import type {
  AssinaturaServico,
  ContaFixa,
  ContaFixaDerivada,
  RetratoFixo,
} from './types';

/** Formatação de reais inteiros (sem centavos) — fonte única em `@/lib/money`, reexportada porque
 * os componentes desta tela importam daqui. Ver `docs/ui/financeiro-ui.md` §5. */
export { formatCentavosWhole, formatSignedCentavosWhole } from '@/lib/money';

/** Meses do histórico de contas fixas, Ago→Jul (mês mais recente por último). */
export const MESES_FIXAS = ['Ago', 'Set', 'Out', 'Nov', 'Dez', 'Jan', 'Fev', 'Mar', 'Abr', 'Mai', 'Jun', 'Jul'] as const;

/** Meses do gráfico Novos×Churn de assinaturas, Fev→Jul. */
export const MESES_ASSINATURAS = ['Fev', 'Mar', 'Abr', 'Mai', 'Jun', 'Jul'] as const;

/**
 * Limiar de "degrau fora do padrão" (mockup: `it.varPct >= 15`). Acima disso, a
 * conta ganha o chip "Atenção" e entra no discurso do Super Consultor.
 */
const LIMIAR_ALERTA_PCT = 15;

/** "+21,9%" / "−5,4%" — sinal explícito com vírgula pt-BR (mockup: `pctSigned`). */
export function formatPctSigned(value: number, digits = 1): string {
  const sign = value >= 0 ? '+' : '−';
  return `${sign}${Math.abs(value).toFixed(digits).replace('.', ',')}%`;
}

/** "12,3%" sem sinal (mockup: `pctPlain`) — usado na fatia de participação. */
export function formatPctPlain(value: number, digits = 1): string {
  return `${value.toFixed(digits).replace('.', ',')}%`;
}

/** Deriva atual/mesPassado/média/variação/YTD de uma conta fixa a partir do histórico bruto. */
export function derivarContaFixa(item: ContaFixa): ContaFixaDerivada {
  const atual = item.historico12m[11];
  const mesPassado = item.historico12m[10];
  const trailing6 = item.historico12m.slice(5, 11); // Jan..Jun
  const media6m = Math.round(trailing6.reduce((a, b) => a + b, 0) / 6);
  const variacaoPct = media6m > 0 ? ((atual - media6m) / media6m) * 100 : 0;
  const totalAnoCorrente = item.historico12m.slice(5, 12).reduce((a, b) => a + b, 0); // Jan..Jul
  return {
    ...item,
    atual,
    mesPassado,
    media6m,
    variacaoPct,
    emAlerta: variacaoPct >= LIMIAR_ALERTA_PCT,
    totalAnoCorrente,
  };
}

export interface FixasTotais {
  totalAtual: Centavos;
  totalMesPassado: Centavos;
  deltaAbs: Centavos;
  deltaPct: number;
  custoPorDia: Centavos;
  pesoReceitaPct: number;
}

export function calcularTotaisFixas(
  itens: ContaFixaDerivada[],
  receitaMediaReferencia: Centavos,
  diasUteisMes: number,
): FixasTotais {
  const totalAtual = itens.reduce((s, i) => s + i.atual, 0);
  const totalMesPassado = itens.reduce((s, i) => s + i.mesPassado, 0);
  const deltaAbs = totalAtual - totalMesPassado;
  const deltaPct = totalMesPassado > 0 ? (deltaAbs / totalMesPassado) * 100 : 0;
  return {
    totalAtual,
    totalMesPassado,
    deltaAbs,
    deltaPct,
    custoPorDia: Math.round(totalAtual / diasUteisMes),
    pesoReceitaPct: receitaMediaReferencia > 0 ? (totalAtual / receitaMediaReferencia) * 100 : 0,
  };
}

export function calcularRetratoFixo(itens: ContaFixaDerivada[]): RetratoFixo {
  const totalAtual = itens.reduce((s, i) => s + i.atual, 0);
  const totalHaSeisMeses = itens.reduce((s, i) => s + i.historico12m[5], 0); // valor de Jan
  return {
    projecaoAnual: totalAtual * 12,
    variacaoSeisMesesPct: totalHaSeisMeses > 0 ? ((totalAtual - totalHaSeisMeses) / totalHaSeisMeses) * 100 : 0,
    totalHaSeisMeses,
    totalAtual,
    compromissosAtivos: itens.length,
  };
}

/**
 * Soma o histórico mês a mês de todas as contas fixas — alimenta o sparkline do
 * KPI "Custo de existir" (mockup: `sparklineFixas`).
 */
export function calcularSerieMensalFixas(itens: ContaFixaDerivada[]): Centavos[] {
  return MESES_FIXAS.map((_, m) => itens.reduce((s, i) => s + i.historico12m[m], 0));
}

/**
 * A "próxima grande" conta a vencer no mês de referência (mockup: `maiorPendente`,
 * filtra `prox.slice(3) === '07'` — mês de referência fixo, Julho/2026, mesmo
 * período do header). Entre as pendentes do mês, pega a de maior valor atual.
 */
export function calcularProximaGrande(itens: ContaFixaDerivada[], sufixoMesReferencia = '07'): ContaFixaDerivada | null {
  const pendentes = itens.filter((i) => i.proximaLabel.slice(3) === sufixoMesReferencia);
  if (pendentes.length === 0) return null;
  return pendentes.reduce((maior, atual) => (atual.atual > maior.atual ? atual : maior), pendentes[0]);
}

/**
 * Período de referência fixo do mock — Julho/2026, o mesmo "Julho 2026" do
 * period-pill do header. `anosDesde` usa isto para calcular tempo de casa.
 */
const MES_INDEX: Record<string, number> = { jan: 0, fev: 1, mar: 2, abr: 3, mai: 4, jun: 5, jul: 6, ago: 7, set: 8, out: 9, nov: 10, dez: 11 };
const REFERENCIA_MESES_TOTAIS = MES_INDEX.jul + 2026 * 12;

/** "5 anos e 6 meses de casa" — tempo decorrido desde `ativaDesde` (ex. "jan/2021") até a referência fixa. */
export function anosDesde(ativaDesde: string): string {
  const [mesStr, anoStr] = ativaDesde.split('/');
  const inicioMesesTotais = (MES_INDEX[mesStr] ?? 0) + (2000 + Number(anoStr.slice(-2))) * 12;
  const diff = REFERENCIA_MESES_TOTAIS - inicioMesesTotais;
  const anos = Math.floor(diff / 12);
  const meses = diff % 12;
  if (anos <= 0) return `${meses} ${meses === 1 ? 'mês' : 'meses'} de casa`;
  if (meses === 0) return `${anos} ${anos === 1 ? 'ano' : 'anos'} de casa`;
  return `${anos} ${anos === 1 ? 'ano e' : 'anos e'} ${meses} ${meses === 1 ? 'mês' : 'meses'} de casa`;
}

// ───────────────────────── Assinaturas (resumo) ─────────────────────────

export function calcularMrrTotal(servicos: AssinaturaServico[]): Centavos {
  return servicos.reduce((s, p) => s + p.mrr, 0);
}

export function calcularPctMrr(servico: AssinaturaServico, mrrTotal: Centavos): number {
  return mrrTotal > 0 ? (servico.mrr / mrrTotal) * 100 : 0;
}

/** Soma o último mês (Jul) de cada série mensal — usado tanto p/ novos quanto p/ churn. */
function somarUltimoMes(series: Centavos[][]): Centavos {
  return series.reduce((s, serie) => s + serie[serie.length - 1], 0);
}

export function calcularChurnMesTotal(servicos: AssinaturaServico[]): Centavos {
  return somarUltimoMes(servicos.map((p) => p.churn6m));
}

export function calcularNovosMaisExpansaoMes(servicos: AssinaturaServico[]): Centavos {
  return somarUltimoMes(servicos.map((p) => p.novos6m));
}

export function calcularChurnClientesMesTotal(servicos: AssinaturaServico[]): number {
  return servicos.reduce((s, p) => s + p.churnClientesMes, 0);
}

export function calcularNovosClientesMesTotal(servicos: AssinaturaServico[]): number {
  return servicos.filter((p) => p.novos6m[p.novos6m.length - 1] > 0).length;
}

/** % do churn deste mês sobre a base de MRR ANTES do churn (mockup: 16,9% = 1239 / (6077+1239)). */
export function calcularChurnPctBase(churnMes: Centavos, mrrAtual: Centavos): number {
  const baseAntes = mrrAtual + churnMes;
  return baseAntes > 0 ? (churnMes / baseAntes) * 100 : 0;
}

export function calcularArrEstimado(mrrAtual: Centavos): Centavos {
  return mrrAtual * 12;
}

export function calcularTicketMedio(mrrAtual: Centavos, assinaturasAtivasCount: number): Centavos {
  return assinaturasAtivasCount > 0 ? Math.round(mrrAtual / assinaturasAtivasCount) : 0;
}

/** Variação % genérica entre um valor atual e um valor de referência (ex.: MRR vs mês anterior). */
export function calcularDeltaPct(atual: Centavos, referencia: Centavos): number {
  return referencia > 0 ? ((atual - referencia) / referencia) * 100 : 0;
}
