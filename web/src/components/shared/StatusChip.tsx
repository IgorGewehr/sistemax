import type { ReactNode } from 'react';

import { cn } from '@/lib/utils';

/** Estado de uma linha/valor financeiro. Cores reservadas (pos/crit/warn) — nunca série categórica. */
export type ChipTone = 'sobra' | 'falta' | 'aberto' | 'bateu' | 'neutro';

const TONE_CLASSES: Record<ChipTone, string> = {
  sobra: 'text-pos bg-pos-soft',
  falta: 'text-crit bg-crit-soft',
  aberto: 'text-warn bg-warn-soft',
  bateu: 'text-muted-foreground bg-surface-2',
  neutro: 'text-muted-foreground bg-surface-2',
};

interface StatusChipProps {
  tone: ChipTone;
  children: ReactNode;
  /** Ponto colorido antes do texto (padrão dos chips do mockup). */
  dot?: boolean;
  className?: string;
}

/** Chip de estado (`.chip` do mockup): "Sobra", "Falta", "Em aberto", "Bateu certinho". */
export function StatusChip({ tone, children, dot = true, className }: StatusChipProps) {
  return (
    <span
      className={cn(
        'inline-flex items-center gap-1.5 whitespace-nowrap rounded-full px-2.5 py-0.5 text-[11.5px] font-semibold',
        TONE_CLASSES[tone],
        className,
      )}
    >
      {dot && <span className="h-1.5 w-1.5 rounded-full bg-current" />}
      {children}
    </span>
  );
}
