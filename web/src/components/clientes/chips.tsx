import type { ReactNode } from 'react';

import { cn } from '@/lib/utils';

import type { Tone } from './calc';

/**
 * Chip local (não o `StatusChip` de `components/shared` — aquele só conhece o vocabulário de caixa
 * `sobra/falta/aberto/bateu/neutro`). Clientes precisa de `ativo/inativo/venda/os`, mesmo caso que
 * `components/compras/chips.tsx` já resolveu — repetimos o padrão aqui. Sem tom `info`: lá ele decora
 * um status fixo ("a conferir"); aqui o status de OS é sempre dinâmico via `statusHistoricoTone`
 * (`pos/warn/faint`), então `info` nunca seria exercitado — ver `Tone` em `calc.ts`.
 */
const TONE_CLASSES: Record<Tone, string> = {
  warn: 'text-warn bg-warn-soft',
  pos: 'text-pos bg-pos-soft',
  crit: 'text-crit bg-crit-soft',
  faint: 'text-faint bg-surface-2',
};

const DOT_CLASSES: Record<Tone, string> = {
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

/** Chip de status/tag — ponto + rótulo, usado na Home (status do cliente) e na Ficha (histórico). */
export function Chip({ tone, children, className }: ChipProps) {
  return (
    <span className={cn('inline-flex items-center gap-1.5 whitespace-nowrap rounded-full px-2.5 py-0.5 text-[11.5px] font-semibold', TONE_CLASSES[tone], className)}>
      <span className={cn('h-1.5 w-1.5 flex-none rounded-full', DOT_CLASSES[tone])} />
      {children}
    </span>
  );
}

/** Tag pequena sem ponto — usada pras tags livres do operador (ex.: "vip", "atacado"). */
export function TagChip({ children, className }: { children: ReactNode; className?: string }) {
  return (
    <span className={cn('inline-flex items-center rounded-full bg-surface-2 px-2.5 py-0.5 text-[11.5px] font-semibold text-muted-foreground', className)}>
      {children}
    </span>
  );
}
