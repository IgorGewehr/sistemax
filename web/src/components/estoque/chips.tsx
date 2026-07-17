import { cn } from '@/lib/utils';

import type { EstadoItemCode } from './types';

/**
 * Chip de estado do item (`.chip` do mockup) — local em vez de reusar o `StatusChip` do
 * Financeiro porque os tons não casam 1:1 (`StatusChip` fala de sobra/falta/aberto/bateu; aqui é
 * ok/baixo/zerado/serviço). Mesma decisão de `components/compras/chips.tsx`.
 */
const TONE_CLASSES: Record<EstadoItemCode, string> = {
  ok: 'text-pos bg-pos-soft',
  baixo: 'text-warn bg-warn-soft',
  zerado: 'text-crit bg-crit-soft',
  servico: 'text-faint bg-surface-2',
};

interface EstadoChipProps {
  code: EstadoItemCode;
  label: string;
  className?: string;
}

export function EstadoChip({ code, label, className }: EstadoChipProps) {
  return (
    <span
      className={cn(
        'inline-flex items-center gap-1.5 whitespace-nowrap rounded-full px-2.5 py-0.5 text-[11.5px] font-semibold',
        TONE_CLASSES[code],
        className,
      )}
    >
      <span className="h-1.5 w-1.5 flex-none rounded-full bg-current" />
      {label}
    </span>
  );
}
