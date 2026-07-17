import { cn } from '@/lib/utils';

import type { SegFiltro } from './types';

interface SegmentedFiltroProps {
  value: SegFiltro;
  onChange: (value: SegFiltro) => void;
}

const OPCOES: { value: SegFiltro; label: string }[] = [
  { value: 'tudo', label: 'Tudo' },
  { value: 'receber', label: 'A receber' },
  { value: 'pagar', label: 'A pagar' },
];

/** Segmentado Tudo / A receber / A pagar (`.subhead .seg` do mockup) — filtra só a Linha do tempo. */
export function SegmentedFiltro({ value, onChange }: SegmentedFiltroProps) {
  return (
    <div className="mb-4 inline-flex gap-0.5 rounded-[11px] border border-border bg-surface-2 p-[3px]">
      {OPCOES.map((opt) => (
        <button
          key={opt.value}
          type="button"
          onClick={() => onChange(opt.value)}
          className={cn(
            'rounded-lg px-3.5 py-1.5 text-[13px] font-semibold transition-colors',
            value === opt.value ? 'bg-card text-foreground shadow-sm' : 'text-muted-foreground hover:text-foreground',
          )}
        >
          {opt.label}
        </button>
      ))}
    </div>
  );
}
