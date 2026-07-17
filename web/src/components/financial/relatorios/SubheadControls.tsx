import { ChevronDown } from 'lucide-react';

import { cn } from '@/lib/utils';

import type { PeriodOption, Regime } from './types';
import { useDisclosure } from './useDisclosure';

interface SubheadControlsProps {
  periods: PeriodOption[];
  periodLabel: string;
  onSelectPeriod: (label: string) => void;
  regime: Regime;
  onSetRegime: (regime: Regime) => void;
}

/**
 * Período + Regime (`.subhead` do mockup) — a única tela do Financeiro em que o regime é um
 * toggle explícito e global (as demais não expõem essa escolha).
 */
export function SubheadControls({ periods, periodLabel, onSelectPeriod, regime, onSetRegime }: SubheadControlsProps) {
  const { open, ref, toggle, close } = useDisclosure<HTMLDivElement>();

  return (
    <div className="mb-3 flex flex-wrap items-center justify-between gap-5">
      <div className="flex flex-wrap items-center gap-5">
        <div ref={ref} className="relative">
          <button
            type="button"
            onClick={toggle}
            className="flex items-center gap-2 rounded-[10px] border border-border bg-card px-3 py-2 text-[13px] font-semibold text-foreground transition-colors hover:bg-surface-2 active:brightness-95"
          >
            {periodLabel}
            <ChevronDown className="h-3.5 w-3.5" />
          </button>
          {open && (
            <div className="absolute left-0 top-[calc(100%+6px)] z-30 min-w-[175px] rounded-xl border border-border bg-card p-1.5 shadow-lg">
              {periods.map((period) => (
                <button
                  key={period.id}
                  type="button"
                  onClick={() => {
                    onSelectPeriod(period.label);
                    close();
                  }}
                  className={cn(
                    'block w-full rounded-lg px-2.5 py-2 text-left text-[13px] font-semibold transition-colors hover:bg-surface-2',
                    period.label === periodLabel ? 'bg-primary-soft text-primary-600' : 'text-foreground',
                  )}
                >
                  {period.label}
                </button>
              ))}
            </div>
          )}
        </div>

        <div className="flex items-center gap-2.5">
          <span className="text-[13px] font-semibold text-muted-foreground">Regime</span>
          <div className="inline-flex gap-0.5 rounded-[11px] border border-border bg-surface-2 p-[3px]">
            <button
              type="button"
              onClick={() => onSetRegime('competencia')}
              className={cn(
                'rounded-lg px-3.5 py-1.5 text-[13px] font-semibold transition-colors',
                regime === 'competencia' ? 'bg-card text-foreground shadow-sm' : 'text-muted-foreground hover:text-foreground',
              )}
            >
              Competência
            </button>
            <button
              type="button"
              onClick={() => onSetRegime('caixa')}
              className={cn(
                'rounded-lg px-3.5 py-1.5 text-[13px] font-semibold transition-colors',
                regime === 'caixa' ? 'bg-card text-foreground shadow-sm' : 'text-muted-foreground hover:text-foreground',
              )}
            >
              Caixa
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
