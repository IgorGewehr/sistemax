import type { ReactNode } from 'react';

import { cn } from '@/lib/utils';

import type { Tone } from './calc';

/**
 * Cor de cada `Tone`. O mockup usa 5 famílias de cor pros chips/tags/dots de Compras — 4 já são
 * tokens do design system (`pos`/`warn`/`crit`/`faint`); `info` (azul "aguardando ação" — status
 * "a conferir"/"Enviado") não tem token global ainda, então usa os HSL literais do mockup
 * (`--info`/`--info-soft` claro e escuro) via valor arbitrário — mesma cor, sem inventar estado.
 */
const TONE_CLASSES: Record<Tone, string> = {
  info: 'text-[hsl(217_80%_52%)] bg-[hsl(217_70%_95%)] dark:text-[hsl(213_90%_68%)] dark:bg-[hsl(217_45%_14%)]',
  warn: 'text-warn bg-warn-soft',
  pos: 'text-pos bg-pos-soft',
  crit: 'text-crit bg-crit-soft',
  faint: 'text-faint bg-surface-2',
};

const DOT_CLASSES: Record<Tone, string> = {
  info: 'bg-[hsl(217_80%_52%)] dark:bg-[hsl(213_90%_68%)]',
  warn: 'bg-warn',
  pos: 'bg-pos',
  crit: 'bg-crit',
  faint: 'bg-faint',
};

interface ChipProps {
  tone: Tone;
  children: ReactNode;
  className?: string;
}

/** Chip de status (`.chip` do mockup) — ponto + rótulo, usado nas tabelas de notas/pedidos/fornecedores. */
export function Chip({ tone, children, className }: ChipProps) {
  return (
    <span className={cn('inline-flex items-center gap-1.5 whitespace-nowrap rounded-full px-2.5 py-0.5 text-[11.5px] font-semibold', TONE_CLASSES[tone], className)}>
      <span className={cn('h-1.5 w-1.5 flex-none rounded-full', DOT_CLASSES[tone])} />
      {children}
    </span>
  );
}

/** Tag pequena uppercase ao lado do nome do item (`.item-nome .tag` do mockup) — sem ponto. */
export function MatchTag({ tone, children, className }: ChipProps) {
  return (
    <span className={cn('ml-1.5 rounded-full px-1.5 py-0.5 text-[10.5px] font-semibold uppercase tracking-[0.03em]', TONE_CLASSES[tone], className)}>
      {children}
    </span>
  );
}

/** Bolinha isolada (`.mdot` do mockup) — prefixo de cada linha de item na conferência. */
export function MatchDot({ tone, className }: { tone: Tone; className?: string }) {
  return <span className={cn('mt-1.5 h-[9px] w-[9px] flex-none rounded-full', DOT_CLASSES[tone], className)} />;
}
