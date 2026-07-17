import type { ReactNode } from 'react';

import { Eyebrow } from '@/components/shared';
import { cn } from '@/lib/utils';

interface KpiClickableProps {
  label: ReactNode;
  value: ReactNode;
  active: boolean;
  onClick: () => void;
  children?: ReactNode;
}

/**
 * KPI clicável (`.kpi.clickable` do mockup) — os shared `KpiCard` não expõem `onClick`/estado
 * ativo, então este botão replica a mesma superfície (`.surface`) com borda de marca quando ativo.
 * Nunca `active:scale` (encolhe a hitbox) — só `active:brightness-95`.
 */
export function KpiClickable({ label, value, active, onClick, children }: KpiClickableProps) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={cn(
        'surface block w-full rounded-xl p-4 text-left transition-colors duration-150 active:brightness-95 sm:p-[18px]',
        active ? 'border-primary-600 bg-primary-soft' : 'hover:border-primary-600/45',
      )}
    >
      <Eyebrow>{label}</Eyebrow>
      <div className="num relative z-[1] mt-2.5 text-2xl font-bold tracking-tight">{value}</div>
      {children}
    </button>
  );
}
