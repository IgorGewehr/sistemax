import { ArrowDown, ArrowUp } from 'lucide-react';

import { SectionCard } from '@/components/shared';
import { cn } from '@/lib/utils';

import { MoneyValue } from './MoneyValue';
import type { DrillTarget, ProximoVencimento } from './types';

interface ProximosVencimentosSectionProps {
  vencimentos: ProximoVencimento[];
  onDrill: (target: DrillTarget) => void;
}

/** "Próximos 7 dias" (bloco ④) — cartões de vencimento; clique manda pra Entradas & Saídas. */
export function ProximosVencimentosSection({ vencimentos, onDrill }: ProximosVencimentosSectionProps) {
  return (
    <SectionCard title="Próximos 7 dias" hint="clique num dia → a parcela em Entradas & Saídas" bodyClassName="mt-0">
      <div className="flex flex-wrap gap-2.5 p-3.5 sm:p-[18px] sm:pt-3.5">
        {vencimentos.map((v) => (
          <button
            key={`${v.dataLabel}-${v.descricao}`}
            type="button"
            onClick={() => onDrill(v.drill)}
            className={cn(
              'min-w-[44%] flex-1 rounded-xl border-l-[3px] bg-surface-2 px-3.5 py-3 text-left transition-transform hover:-translate-y-0.5 hover:shadow-lg focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
              v.tone === 'pos' ? 'border-l-pos' : 'border-l-crit',
            )}
          >
            <div className="text-[11px] font-semibold text-muted-foreground">{v.dataLabel}</div>
            <div className="mt-[7px] flex items-center gap-1 text-[17px] font-bold tracking-tight">
              {v.tone === 'pos' ? <ArrowUp className="h-[13px] w-[13px] text-pos" /> : <ArrowDown className="h-[13px] w-[13px] text-crit" />}
              <MoneyValue centavos={v.valorCentavos} signed tone={v.tone} />
            </div>
            <div className="mt-[3px] truncate text-xs text-muted-foreground">{v.descricao}</div>
          </button>
        ))}
      </div>
    </SectionCard>
  );
}
