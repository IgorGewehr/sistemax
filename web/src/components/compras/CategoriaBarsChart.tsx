import { formatCentavos } from '@/lib/money';

import { buildGroupedBars } from './calc';
import type { CustoPorCategoria } from './types';

interface CategoriaBarsChartProps {
  data: CustoPorCategoria;
}

/** "Custo por categoria" — barras agrupadas por mês, uma barra por categoria (`groupedBarsSvg()` do mockup). */
export function CategoriaBarsChart({ data }: CategoriaBarsChartProps) {
  const geo = buildGroupedBars(data.meses, data.categorias, formatCentavos);

  return (
    <svg viewBox={`0 0 ${geo.viewW} ${geo.viewH}`} role="img" aria-label="Custo por categoria, mês a mês" className="block w-full">
      <line x1={geo.x0} y1={geo.zeroY} x2={geo.x1} y2={geo.zeroY} stroke="hsl(var(--border))" strokeWidth={1} />
      {geo.bars.map((bar, i) => (
        <rect key={i} x={bar.x} y={bar.y} width={bar.width} height={bar.height} rx={2.5} fill={bar.fill} className="opacity-100 transition-opacity hover:opacity-80">
          <title>{bar.titulo}</title>
        </rect>
      ))}
      {geo.monthLabels.map((m) => (
        <text key={m.label} x={m.x} y={geo.viewH - 5} textAnchor="middle" fill="hsl(var(--muted-foreground))" fontSize={9}>
          {m.label}
        </text>
      ))}
    </svg>
  );
}
