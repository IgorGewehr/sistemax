import { useId } from 'react';

import type { RoiDoNegocioDto } from '@/lib/api/financeiro';
import { formatCentavosWhole } from '@/lib/money';

import { computeRoiChartLayout } from './deriveChart';


interface RoiChartProps {
  roi: RoiDoNegocioDto;
}

/** Gráfico "investido × recuperado" (SVG puro, mesmo approach do mockup) — cores via
 * `className="text-X" fill/stroke="currentColor"`, igual ao `DivergentBarChart` (bancário). */
export function RoiChart({ roi }: RoiChartProps) {
  const layout = computeRoiChartLayout(roi);
  const titleId = useId();

  if (!layout.solidPath) return null;

  return (
    <div className="relative mx-2 mb-2.5 mt-1">
      <svg viewBox={layout.viewBox} role="img" aria-labelledby={titleId} className="block h-auto w-full">
        <title id={titleId}>Investido acumulado versus caixa recuperado, com o mês de payback destacado</title>

        {layout.gapPath && <path d={layout.gapPath} className="text-crit" fill="currentColor" fillOpacity={0.13} />}

        <line
          x1={16}
          y1={layout.investedY}
          x2={884}
          y2={layout.investedY}
          className="text-foreground"
          stroke="currentColor"
          strokeOpacity={0.55}
          strokeWidth={2}
          strokeDasharray="5 4"
        />
        <text x={20} y={layout.investedY - 7} fontSize={10.5} fontWeight={700} className="text-foreground" fill="currentColor" fillOpacity={0.7}>
          Investido {formatCentavosWhole(layout.investedTotalCentavos)}
        </text>

        <path d={layout.solidPath} fill="none" className="text-primary-600" stroke="currentColor" strokeWidth={2.6} strokeLinecap="round" strokeLinejoin="round" />
        {layout.dashedPath && (
          <path
            d={layout.dashedPath}
            fill="none"
            className="text-primary-600"
            stroke="currentColor"
            strokeWidth={2.6}
            strokeDasharray="1 5.5"
            strokeLinecap="round"
            strokeLinejoin="round"
            strokeOpacity={0.85}
          />
        )}

        {layout.crossPoint && (
          <>
            <line
              x1={layout.crossPoint.x}
              y1={40}
              x2={layout.crossPoint.x}
              y2={layout.investedY}
              className="text-pos"
              stroke="currentColor"
              strokeOpacity={0.6}
              strokeWidth={1.3}
              strokeDasharray="2 3"
            />
            <CrossCallout x={layout.crossPoint.x} label={layout.crossPoint.label} valor={formatCentavosWhole(layout.investedTotalCentavos)} />
            <circle cx={layout.crossPoint.x} cy={layout.investedY} r={5.5} className="text-pos" fill="currentColor" />
          </>
        )}

        <circle cx={layout.todayPoint.x} cy={layout.todayPoint.y} r={4.5} className="text-primary-600" fill="currentColor" />

        {layout.axisLabels.map((l) => (
          <text key={l.label} x={l.x} y={layout.axisY} fontSize={10} textAnchor={l.anchor} className="text-muted-foreground" fill="currentColor">
            {l.label}
          </text>
        ))}
      </svg>
    </div>
  );
}

function CrossCallout({ x, label, valor }: { x: number; label: string; valor: string }) {
  const boxW = 220;
  const boxH = 36;
  const bx = Math.max(16, Math.min(x - boxW / 2, 900 - 16 - boxW));

  return (
    <g>
      <rect x={bx} y={4} width={boxW} height={boxH} rx={9} className="text-pos" fill="currentColor" fillOpacity={0.12} stroke="currentColor" strokeOpacity={0.4} />
      <text x={bx + 12} y={4 + 15} fontSize={10.5} fontWeight={700} className="text-pos" fill="currentColor">
        {label}
      </text>
      <text x={bx + 12} y={4 + 29} fontSize={11.5} fontWeight={700} className="text-pos font-mono" fill="currentColor">
        {valor} recuperados
      </text>
    </g>
  );
}
