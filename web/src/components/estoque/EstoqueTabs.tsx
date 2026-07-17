import { cn } from '@/lib/utils';

import type { EstoqueTab } from './types';

const TABS: { key: EstoqueTab; label: string }[] = [
  { key: 'geral', label: 'Visão geral' },
  { key: 'produtos', label: 'Produtos' },
  { key: 'mov', label: 'Movimentações' },
  { key: 'inv', label: 'Inventários' },
  { key: 'rel', label: 'Relatórios' },
];

interface EstoqueTabsProps {
  ativa: EstoqueTab;
  onChange: (tab: EstoqueTab) => void;
}

/**
 * Barra de abas (`.tabs` do mockup) — navegação client-side dentro da MESMA rota `/estoque` (o
 * módulo continua com uma única entrada no `App.tsx`/Sidebar; diferente do Financeiro, que tem
 * uma rota por aba). Visual em sublinhado, igual ao `FinanceiroLayout`.
 */
export function EstoqueTabs({ ativa, onChange }: EstoqueTabsProps) {
  return (
    <nav aria-label="Seções do Estoque" className="mb-5 flex gap-1 overflow-x-auto border-b border-border/70 scrollbar-hide">
      {TABS.map((tab) => {
        const isActive = tab.key === ativa;
        return (
          <button
            key={tab.key}
            type="button"
            onClick={() => onChange(tab.key)}
            className={cn(
              'relative shrink-0 whitespace-nowrap px-3.5 py-2.5 text-sm font-semibold transition-colors',
              isActive ? 'text-primary-600' : 'text-muted-foreground hover:text-foreground',
            )}
          >
            {tab.label}
            {isActive && <span className="absolute inset-x-0 -bottom-px h-0.5 rounded-full bg-primary-600" />}
          </button>
        );
      })}
    </nav>
  );
}
