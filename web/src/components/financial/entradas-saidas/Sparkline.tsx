import { useId } from 'react';

import type { HeroSparkline } from './types';

/**
 * Mini-gráfico decorativo do KPI hero "A receber em aberto" — asset visual fixo do mockup (não
 * tem série numérica própria por trás; é ilustrativo, como no HTML original).
 */
export function Sparkline({ pathLinha, pathArea }: HeroSparkline) {
  const gradId = useId();
  return (
    <svg viewBox="0 0 260 34" preserveAspectRatio="none" aria-hidden="true" className="mt-2.5 block h-8 w-full">
      <defs>
        <linearGradient id={gradId} x1="0" y1="0" x2="0" y2="1">
          <stop offset="0" stopColor="hsl(var(--primary))" stopOpacity={0.28} />
          <stop offset="1" stopColor="hsl(var(--primary))" stopOpacity={0} />
        </linearGradient>
      </defs>
      <path d={pathLinha} fill="none" stroke="hsl(var(--primary))" strokeWidth={2} strokeLinecap="round" />
      <path d={pathArea} fill={`url(#${gradId})`} />
      <circle cx={260} cy={9} r={3} fill="hsl(var(--primary))" />
    </svg>
  );
}
