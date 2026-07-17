import { cn } from '@/lib/utils';

import type { SegmentoTabela } from './useCompras';

const OPCOES: { valor: SegmentoTabela; label: string }[] = [
  { valor: 'notas', label: 'Notas de entrada' },
  { valor: 'pedidos', label: 'Pedidos' },
  { valor: 'fornecedores', label: 'Fornecedores' },
];

interface SegmentedTabsProps {
  value: SegmentoTabela;
  onChange: (seg: SegmentoTabela) => void;
}

/** Segmented control da tabela da Home (`.seg` do mockup): Notas de entrada · Pedidos · Fornecedores. */
export function SegmentedTabs({ value, onChange }: SegmentedTabsProps) {
  return (
    <div className="inline-flex gap-0.5 rounded-[11px] border border-border bg-surface-2 p-[3px]">
      {OPCOES.map((o) => (
        <button
          key={o.valor}
          type="button"
          onClick={() => onChange(o.valor)}
          className={cn(
            'rounded-lg px-3.5 py-1.5 text-[13px] font-semibold transition-colors',
            value === o.valor ? 'bg-card text-foreground shadow-sm' : 'text-muted-foreground hover:text-foreground',
          )}
        >
          {o.label}
        </button>
      ))}
    </div>
  );
}
