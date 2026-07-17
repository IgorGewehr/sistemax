import type { Centavos } from '@/lib/money';

/**
 * Geometria pura do gráfico "O caixa nos próximos 30 dias" (bloco ②) — separada do componente
 * pra ficar testável sem DOM/SVG. Espelha o cálculo do mockup: acha o único cruzamento de zero a
 * partir de hoje (nunca antes), pra desenhar a área/realce vermelho e o balão "aqui fica
 * negativo" no ponto certo, sem precisar hardcodar em qual dia isso acontece.
 */

const WIDTH = 900;
const HEIGHT = 232;
const PAD_L = 16;
const PAD_R = 16;
const PAD_TOP = 42;
const PAD_BOTTOM = 30;
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
  solidPath: string;
  dashedPath: string;
  /** Área sombreada abaixo de zero, a partir do cruzamento real (não do início do gráfico). */
  negativeAreaPath: string | null;
  /** Trecho da curva já negativo, realçado em `--crit` por cima do traço tracejado normal. */
  negativeDashedPath: string | null;
  /** Índice do primeiro dia em que o saldo projetado fica negativo (null se nunca cruza). */
  crossIndex: number | null;
  crossX: number | null;
  /** Largura de cada "fatia" clicável por dia (30 áreas de hit invisíveis). */
  slotWidth: number;
}

function xFor(i: number, n: number): number {
  return PAD_L + i * (PLOT_W / (n - 1));
}

function pathFrom(points: TimelinePoint[], idxs: number[]): string {
  return idxs
    .map((i, k) => `${k === 0 ? 'M' : 'L'}${points[i].x.toFixed(1)},${points[i].y.toFixed(1)}`)
    .join(' ');
}

export function computeTimelineGeometry(valoresDiarios: Centavos[], hojeIndex: number): TimelineGeometry {
  const n = valoresDiarios.length;
  const min = Math.min(...valoresDiarios, 0);
  const max = Math.max(...valoresDiarios);
  const range = max - min || 1;
  const yFor = (v: number) => PAD_TOP + ((max - v) / range) * PLOT_H;

  const points: TimelinePoint[] = valoresDiarios.map((v, i) => ({ index: i, x: xFor(i, n), y: yFor(v) }));
  const zeroY = yFor(0);

  let crossIndex: number | null = null;
  let crossX: number | null = null;
  for (let i = hojeIndex; i < n - 1; i++) {
    if (valoresDiarios[i] >= 0 && valoresDiarios[i + 1] < 0) {
      crossIndex = i + 1;
      const frac = (0 - valoresDiarios[i]) / (valoresDiarios[i + 1] - valoresDiarios[i]);
      crossX = xFor(i, n) + frac * (xFor(i + 1, n) - xFor(i, n));
      break;
    }
  }

  const solidPath = pathFrom(
    points,
    Array.from({ length: hojeIndex + 1 }, (_, i) => i),
  );
  const dashedPath = pathFrom(
    points,
    Array.from({ length: n - hojeIndex }, (_, i) => i + hojeIndex),
  );

  let negativeAreaPath: string | null = null;
  let negativeDashedPath: string | null = null;
  if (crossIndex !== null && crossX !== null) {
    const tailIdxs = Array.from({ length: n - crossIndex }, (_, k) => (crossIndex as number) + k);
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
    solidPath,
    dashedPath,
    negativeAreaPath,
    negativeDashedPath,
    crossIndex,
    crossX,
    slotWidth: PLOT_W / n,
  };
}
