import type { ReactNode } from 'react';

import { cn } from '@/lib/utils';

interface StatTileProps {
  label: string;
  value: ReactNode;
  sub?: ReactNode;
  /** Números tabulares (`.num`) — desligue para valores que são nome/texto (ex.: "quinta"). */
  mono?: boolean;
  valueClassName?: string;
}

/** O bloco `.stat` do mockup: rótulo pequeno, valor grande, nota de contexto — reusado pelo card
 * "Padrão do caixa" (visão geral) e pelo drill de uma sessão específica. */
export function StatTile({ label, value, sub, mono = true, valueClassName }: StatTileProps) {
  return (
    <div className="rounded-xl bg-surface-2 px-3.5 py-3">
      <div className="text-xs font-semibold text-muted-foreground">{label}</div>
      <div className={cn('mt-1 text-2xl font-bold tracking-tight', mono && 'num', valueClassName)}>{value}</div>
      {sub && <div className="mt-0.5 text-xs text-faint">{sub}</div>}
    </div>
  );
}
