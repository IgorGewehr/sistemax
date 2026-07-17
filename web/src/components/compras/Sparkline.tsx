import { useId } from 'react';

import type { Centavos } from '@/lib/money';

import { buildSparkline } from './calc';

interface SparklineProps {
  valoresCentavos: Centavos[];
  className?: string;
}

/** Área + linha com ponto final do KPI hero "Comprado no mês" — mesmo desenho do `sparkline()` do mockup. */
export function Sparkline({ valoresCentavos, className }: SparklineProps) {
  const gradientId = useId();
  const { viewW, viewH, path, area, lastPoint } = buildSparkline(valoresCentavos);
  const [lastX, lastY] = lastPoint;

  return (
    <svg className={className ?? 'mt-2.5 block h-[30px] w-full'} viewBox={`0 0 ${viewW} ${viewH}`} preserveAspectRatio="none" aria-hidden="true">
      <defs>
        <linearGradient id={gradientId} x1="0" y1="0" x2="0" y2="1">
          <stop offset="0" stopColor="hsl(var(--primary))" stopOpacity="0.28" />
          <stop offset="1" stopColor="hsl(var(--primary))" stopOpacity="0" />
        </linearGradient>
      </defs>
      <path d={area} fill={`url(#${gradientId})`} />
      <path d={path} fill="none" stroke="hsl(var(--primary))" strokeWidth="2" strokeLinecap="round" />
      <circle cx={lastX} cy={lastY} r="3" fill="hsl(var(--primary))" />
    </svg>
  );
}
