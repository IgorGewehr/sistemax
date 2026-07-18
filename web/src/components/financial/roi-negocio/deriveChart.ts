import type { RoiDoNegocioDto } from '@/lib/api/financeiro';

/**
 * Matemática pura do gráfico "Investido × recuperado" — porta o `<script>` SVG do mockup
 * (`docs/ui/mockups/roi-negocio.html`) pra dado REAL, calculado (nunca uma curva de exemplo
 * hardcoded). A chave: `recuperado(t) − investido(t) ≡ acumulado(t)` (o aporte entra nos dois
 * lados e se cancela — mesma observação do design-imobilizado-roi.md §7, ecoada no card do
 * Consultor do mockup). Então:
 *
 *   recuperado(t) = investidoTotal + serie[t].acumuladoCentavos
 *
 * é EXATAMENTE consistente com `RoiRecuperacao` (aportes + fluxo operacional acumulado) e cruza
 * `investidoTotal` no MESMO mês que `RoiPayback` — sem recalcular payback duas vezes.
 *
 * Segmento futuro (linha tracejada, quando o payback ainda não foi realizado): a série do backend
 * só cobre `m0..mesAtual` (nunca projeta ponto a ponto no wire) — a reta até o cruzamento
 * (`hoje → hoje+projetadoMeses`, onde `recuperado = investidoTotal` por definição) é uma
 * aproximação honesta de uma trajetória que o próprio backend computa como quase-linear
 * (`margemCaixaMensal` constante menos parcelas agendadas) — nunca um valor mensal inventado.
 */

export interface RoiChartPoint {
  competencia: string;
  recuperadoCentavos: number;
}

export interface RoiChartCrossPoint {
  x: number;
  y: number;
  label: string;
  realizado: boolean;
}

export interface RoiChartLayout {
  viewBox: string;
  investedY: number;
  investedTotalCentavos: number;
  solidPath: string;
  dashedPath: string | null;
  gapPath: string | null;
  todayPoint: { x: number; y: number };
  crossPoint: RoiChartCrossPoint | null;
  axisLabels: { x: number; label: string; anchor: 'start' | 'middle' | 'end' }[];
  axisY: number;
}

const W = 900;
const H = 250;
const PAD_L = 16;
const PAD_R = 16;
const PAD_TOP = 46;
const PAD_BOTTOM = 30;

const MESES = ['jan', 'fev', 'mar', 'abr', 'mai', 'jun', 'jul', 'ago', 'set', 'out', 'nov', 'dez'];

export function addMonthsIso(competenciaIso: string, months: number): string {
  const [y, m] = competenciaIso.slice(0, 7).split('-').map(Number);
  const total = y * 12 + (m - 1) + months;
  const ny = Math.floor(total / 12);
  const nm = (total % 12) + 1;
  return `${ny}-${String(nm).padStart(2, '0')}-01`;
}

export function mesLabel(competenciaIso: string): string {
  const [y, m] = competenciaIso.slice(0, 7).split('-').map(Number);
  return `${MESES[(m - 1 + 12) % 12]}/${String(y).slice(2)}`;
}

export function computeRoiChartLayout(roi: RoiDoNegocioDto): RoiChartLayout {
  const investidoTotal = roi.investimento.totalCentavos;
  const historico: RoiChartPoint[] = roi.serie.map((s) => ({
    competencia: s.competencia,
    recuperadoCentavos: investidoTotal + s.acumuladoCentavos,
  }));

  if (historico.length === 0) {
    return {
      viewBox: `0 0 ${W} ${H}`,
      investedY: PAD_TOP,
      investedTotalCentavos: investidoTotal,
      solidPath: '',
      dashedPath: null,
      gapPath: null,
      todayPoint: { x: PAD_L, y: PAD_TOP },
      crossPoint: null,
      axisLabels: [],
      axisY: H - PAD_BOTTOM + 16,
    };
  }

  const hoje = historico[historico.length - 1];
  const jaRealizado = roi.payback.simplesRealizadoEm !== null;
  const projetadoMeses = roi.payback.projetadoMeses;

  const futuro: RoiChartPoint | null =
    !jaRealizado && projetadoMeses !== null && projetadoMeses > 0
      ? { competencia: addMonthsIso(hoje.competencia, projetadoMeses), recuperadoCentavos: investidoTotal }
      : null;

  const pontos = futuro ? [...historico, futuro] : historico;
  const n = pontos.length;

  const maxV = Math.max(investidoTotal, ...pontos.map((p) => p.recuperadoCentavos), 1) * 1.08;
  const minV = Math.min(0, ...pontos.map((p) => p.recuperadoCentavos));

  const plotW = W - PAD_L - PAD_R;
  const plotH = H - PAD_TOP - PAD_BOTTOM;
  const xFor = (i: number) => PAD_L + (n <= 1 ? 0 : (i * plotW) / (n - 1));
  const yFor = (v: number) => PAD_TOP + ((maxV - v) / (maxV - minV || 1)) * plotH;

  const pathFor = (idxs: number[]) =>
    idxs.map((i, j) => `${j === 0 ? 'M' : 'L'}${xFor(i).toFixed(1)},${yFor(pontos[i].recuperadoCentavos).toFixed(1)}`).join(' ');

  const solidPath = pathFor(historico.map((_, i) => i));
  const dashedPath = futuro ? pathFor([historico.length - 1, historico.length]) : null;
  const investedY = yFor(investidoTotal);

  let gapPath: string | null = null;
  if (futuro) {
    const xHoje = xFor(historico.length - 1);
    const yHoje = yFor(hoje.recuperadoCentavos);
    const xCross = xFor(historico.length);
    gapPath = `M ${xHoje.toFixed(1)},${investedY.toFixed(1)} L ${xCross.toFixed(1)},${investedY.toFixed(1)} L ${xCross.toFixed(1)},${yFor(investidoTotal).toFixed(1)} L ${xHoje.toFixed(1)},${yHoje.toFixed(1)} Z`;
  }

  let crossPoint: RoiChartCrossPoint | null = null;
  if (jaRealizado) {
    const idx = historico.findIndex((p) => p.recuperadoCentavos >= investidoTotal);
    if (idx >= 0) {
      crossPoint = {
        x: xFor(idx),
        y: yFor(historico[idx].recuperadoCentavos),
        label: `${mesLabel(historico[idx].competencia)} · ROI completo (realizado)`,
        realizado: true,
      };
    }
  } else if (futuro) {
    crossPoint = {
      x: xFor(n - 1),
      y: investedY,
      label: `${mesLabel(futuro.competencia)} · ROI completo (projetado)`,
      realizado: false,
    };
  }

  const axisLabels: RoiChartLayout['axisLabels'] = [{ x: xFor(0), label: mesLabel(historico[0].competencia), anchor: 'start' }];
  if (historico.length > 1) axisLabels.push({ x: xFor(historico.length - 1), label: `hoje · ${mesLabel(hoje.competencia)}`, anchor: 'middle' });
  if (futuro) axisLabels.push({ x: xFor(n - 1), label: mesLabel(futuro.competencia), anchor: 'end' });

  return {
    viewBox: `0 0 ${W} ${H}`,
    investedY,
    investedTotalCentavos: investidoTotal,
    solidPath,
    dashedPath,
    gapPath,
    todayPoint: { x: xFor(historico.length - 1), y: yFor(hoje.recuperadoCentavos) },
    crossPoint,
    axisLabels,
    axisY: H - PAD_BOTTOM + 16,
  };
}
