import { useId } from 'react';

import { computeDivergentLayout, type DivergentChartItem } from './derive';
import { formatCentavosWhole } from './money';

interface DivergentBarChartProps {
  items: DivergentChartItem[];
  /** Colunas clicáveis (visão por semana) — abrem o drill de dias. */
  clickable?: boolean;
  onColumnClick?: (id: string) => void;
}

/**
 * Gráfico "entrou × saiu" (barras divergentes a partir da linha zero) — réplica do `svgDivergent`
 * do mockup. Usado tanto na visão por semana (clicável) quanto no drill de dias (não clicável).
 */
export function DivergentBarChart({ items, clickable = false, onColumnClick }: DivergentBarChartProps) {
  const layout = computeDivergentLayout(items, clickable);
  const titleId = useId();

  return (
    <svg viewBox={layout.viewBox} role="img" aria-labelledby={titleId} className="block h-auto w-full">
      <title id={titleId}>Entrou e saiu</title>
      <line
        x1={layout.x0}
        y1={layout.zeroY}
        x2={layout.x1}
        y2={layout.zeroY}
        className="text-border"
        stroke="currentColor"
        strokeWidth={1}
      />
      {layout.bars.map((bar) => (
        <g key={bar.id}>
          {bar.colBg && (
            <rect
              className="cursor-pointer text-surface-2 opacity-0 transition-opacity duration-150 hover:opacity-[0.55]"
              fill="currentColor"
              x={bar.colBg.x}
              y={bar.colBg.y}
              width={bar.colBg.width}
              height={bar.colBg.height}
              rx={8}
              role="button"
              tabIndex={0}
              onClick={() => onColumnClick?.(bar.id)}
              onKeyDown={(e) => {
                if (e.key === 'Enter' || e.key === ' ') {
                  e.preventDefault();
                  onColumnClick?.(bar.id);
                }
              }}
            >
              <title>{bar.label} — clique p/ ver os dias</title>
            </rect>
          )}
          {bar.upHeight > 0 && (
            <rect
              className="pointer-events-none text-pos"
              fill="currentColor"
              x={bar.barX}
              y={bar.upY}
              width={bar.barWidth}
              height={bar.upHeight}
              rx={3}
              opacity={bar.opacity}
            >
              <title>
                {bar.label} · entrou {formatCentavosWhole(bar.entrouCentavos)}
              </title>
            </rect>
          )}
          {bar.downHeight > 0 && (
            <rect
              className="pointer-events-none text-crit"
              fill="currentColor"
              x={bar.barX}
              y={bar.downY}
              width={bar.barWidth}
              height={bar.downHeight}
              rx={3}
              opacity={bar.opacity}
            >
              <title>
                {bar.label} · saiu {formatCentavosWhole(bar.saiuCentavos)}
              </title>
            </rect>
          )}
          <text x={bar.labelX} y={bar.labelY} textAnchor="middle" fill="currentColor" className="text-muted-foreground text-[9px]">
            {bar.label}
          </text>
        </g>
      ))}
    </svg>
  );
}
