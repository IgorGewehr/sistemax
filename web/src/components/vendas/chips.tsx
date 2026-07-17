import type { ReactNode } from 'react';

import { cn } from '@/lib/utils';

import type { Tone } from './calc';

const TONE_CLASSES: Record<Tone, string> = {
  pos: 'text-pos bg-pos-soft',
  warn: 'text-warn bg-warn-soft',
  crit: 'text-crit bg-crit-soft',
};

const DOT_CLASSES: Record<Tone, string> = {
  pos: 'bg-pos',
  warn: 'bg-warn',
  crit: 'bg-crit',
};

interface ChipProps {
  tone: Tone;
  children: ReactNode;
  className?: string;
}

/**
 * Chip de status da venda — ponto + rótulo. Não reusa o `StatusChip` compartilhado: seus tones
 * (`sobra/falta/aberto/bateu/neutro`) foram cunhados pro vocabulário de conferência de caixa do
 * Financeiro — mapear `Concluida→'sobra'` seria tecnicamente igual (mesma cor) mas semanticamente
 * estranho pra quem ler o código depois. Mesmo precedente de `components/compras/chips.tsx`, que
 * resolveu o mesmo problema criando seu próprio chip com tones genéricos.
 */
export function Chip({ tone, children, className }: ChipProps) {
  return (
    <span
      className={cn(
        'inline-flex items-center gap-1.5 whitespace-nowrap rounded-full px-2.5 py-0.5 text-[11.5px] font-semibold',
        TONE_CLASSES[tone],
        className,
      )}
    >
      <span className={cn('h-1.5 w-1.5 flex-none rounded-full', DOT_CLASSES[tone])} />
      {children}
    </span>
  );
}
