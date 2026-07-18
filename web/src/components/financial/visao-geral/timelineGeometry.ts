import type { Centavos } from '@/lib/money';

/**
 * Geometria pura do gráfico "Caixa · próximos 30 dias" (bloco ①b, único gráfico da tela v3) —
 * separada do componente pra ficar testável sem DOM/SVG. Área sob a curva inteira até a linha de
 * zero (`areaPath`, "o rio nunca toca o fundo = tudo bem", ver `docs/ui/mockups/visao-geral-v3.html`)
 * + o realce defensivo de quando o saldo projetado cruza abaixo de zero (nem sempre acontece — o
 * mockup assume positivo o tempo todo, mas a projeção REAL pode cruzar).
 */

const WIDTH = 880;
const HEIGHT = 226;
const PAD_L = 14;
const PAD_R = 14;
const PAD_TOP = 30;
const PAD_BOTTOM = 28;
const PLOT_W = WIDTH - PAD_L - PAD_R;
const PLOT_H = HEIGHT - PAD_TOP - PAD_BOTTOM;

export interface TimelinePoint {
  index: number;
  x: number;
  y: number;
}

export interface TimelineGeometry {
  width: number;
  height: number;
  padTop: number;
  padBottom: number;
  points: TimelinePoint[];
  zeroY: number;
  /** Área lavada sob a curva inteira, até a linha de zero — o "rio" do mockup. */
  areaPath: string;
  solidPath: string;
  dashedPath: string;
  /** Área abaixo de zero, a partir do cruzamento real (não do início do gráfico) — só quando a
   * projeção realmente cruza. */
  negativeAreaPath: string | null;
  negativeDashedPath: string | null;
  crossIndex: number | null;
  /** Largura de cada "fatia" clicável por dia. */
  slotWidth: number;
}

function xFor(i: number, n: number): number {
  return PAD_L + i * (PLOT_W / (n - 1 || 1));
}

function pathFrom(points: TimelinePoint[], idxs: number[]): string {
  return idxs.map((i, k) => `${k === 0 ? 'M' : 'L'}${points[i].x.toFixed(1)},${points[i].y.toFixed(1)}`).join(' ');
}

export function computeTimelineGeometry(valoresDiarios: Centavos[], hojeIndex: number): TimelineGeometry {
  const n = valoresDiarios.length;
  const min = Math.min(...valoresDiarios, 0);
  const max = Math.max(...valoresDiarios, 1);
  const range = max - min || 1;
  const yFor = (v: number) => PAD_TOP + ((max - v) / range) * PLOT_H;

  const points: TimelinePoint[] = valoresDiarios.map((v, i) => ({ index: i, x: xFor(i, n), y: yFor(v) }));
  const zeroY = yFor(0);

  if (n === 0) {
    return {
      width: WIDTH,
      height: HEIGHT,
      padTop: PAD_TOP,
      padBottom: PAD_BOTTOM,
      points,
      zeroY,
      areaPath: '',
      solidPath: '',
      dashedPath: '',
      negativeAreaPath: null,
      negativeDashedPath: null,
      crossIndex: null,
      slotWidth: PLOT_W,
    };
  }

  let crossIndex: number | null = null;
  for (let i = hojeIndex; i < n - 1; i++) {
    if (valoresDiarios[i] >= 0 && valoresDiarios[i + 1] < 0) {
      crossIndex = i + 1;
      break;
    }
  }

  const allIdxs = Array.from({ length: n }, (_, i) => i);
  const solidPath = pathFrom(points, allIdxs.slice(0, hojeIndex + 1));
  const dashedPath = pathFrom(points, allIdxs.slice(hojeIndex));
  const areaPath = `${pathFrom(points, allIdxs)} L ${points[n - 1].x.toFixed(1)},${zeroY.toFixed(1)} L ${points[0].x.toFixed(1)},${zeroY.toFixed(1)} Z`;

  let negativeAreaPath: string | null = null;
  let negativeDashedPath: string | null = null;
  if (crossIndex !== null) {
    const prev = points[crossIndex - 1];
    const curr = points[crossIndex];
    const frac = (0 - valoresDiarios[crossIndex - 1]) / (valoresDiarios[crossIndex] - valoresDiarios[crossIndex - 1]);
    const crossX = prev.x + frac * (curr.x - prev.x);

    const tailIdxs = allIdxs.slice(crossIndex);
    const tail = tailIdxs.map((i) => `L ${points[i].x.toFixed(1)},${points[i].y.toFixed(1)}`).join(' ');

    negativeAreaPath = `M ${crossX.toFixed(1)},${zeroY.toFixed(1)} ${tail} L ${points[n - 1].x.toFixed(1)},${zeroY.toFixed(1)} Z`;
    negativeDashedPath = `M ${crossX.toFixed(1)},${zeroY.toFixed(1)} ${tail}`;
  }

  return {
    width: WIDTH,
    height: HEIGHT,
    padTop: PAD_TOP,
    padBottom: PAD_BOTTOM,
    points,
    zeroY,
    areaPath,
    solidPath,
    dashedPath,
    negativeAreaPath,
    negativeDashedPath,
    crossIndex,
    slotWidth: PLOT_W / n,
  };
}
