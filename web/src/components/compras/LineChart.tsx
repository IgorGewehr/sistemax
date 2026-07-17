import { formatCentavos } from '@/lib/money';

import { buildLineChart } from './calc';
import type { HistoricoCustoSerie } from './types';

interface LineChartProps {
  series: HistoricoCustoSerie[];
}

/** "Histórico de custo · itens mais comprados" do drill de fornecedor (`lineChartSvg()` do mockup). */
export function LineChart({ series }: LineChartProps) {
  const geo = buildLineChart(series);

  return (
    <svg viewBox={`0 0 ${geo.viewW} ${geo.viewH}`} role="img" aria-label="Histórico de custo unitário" className="block w-full">
      {geo.series.map((s) => (
        <g key={s.nome}>
          <path d={s.path} fill="none" stroke={s.cor} strokeWidth={2} strokeLinecap="round" />
          {s.pontos.map((p) => (
            <circle key={p.label} cx={p.x} cy={p.y} r={3} fill={s.cor}>
              <title>
                {s.nome} · {p.label} · {formatCentavos(p.valorCentavos)}
              </title>
            </circle>
          ))}
        </g>
      ))}
      {geo.monthLabels.map((m) => (
        <text key={m.label} x={m.x} y={geo.viewH - 6} textAnchor="middle" fill="hsl(var(--muted-foreground))" fontSize={9}>
          {m.label}
        </text>
      ))}
    </svg>
  );
}
