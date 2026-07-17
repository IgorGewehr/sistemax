import type { ReactNode } from 'react';

import { cn } from '@/lib/utils';

/** Rótulo pequeno maiúsculo acima do título (ex.: "Financeiro · Fluxo de Caixa"). */
export function Eyebrow({ children, className }: { children: ReactNode; className?: string }) {
  return (
    <div className={cn('text-[11px] font-semibold uppercase tracking-[0.08em] text-muted-foreground', className)}>
      {children}
    </div>
  );
}
