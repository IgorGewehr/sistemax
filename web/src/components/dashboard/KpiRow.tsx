import { ArrowDownRight, ArrowUpRight } from 'lucide-react';

import { KpiCard, MoneyValue } from '@/components/shared';
import { cn } from '@/lib/utils';

import type { DrillTarget, KpiDashboardItem, ToneKpi } from './types';

interface KpiRowProps {
  kpis: KpiDashboardItem[];
  onDrill: (target: DrillTarget) => void;
}

const TONE_CLASS: Record<ToneKpi, string> = {
  pos: 'text-pos',
  crit: 'text-crit',
  warn: 'text-warn',
  neutro: 'text-foreground',
};

/**
 * Fileira de KPIs (bloco ②) — um número por módulo, na ordem Vendas → Financeiro → Estoque →
 * Compras → OS (mesma ordem do `Sidebar`). Cada card já chega filtrado por
 * `usePermissoesDashboard` em `Dashboard.tsx` — este componente só decide layout/estilo, nunca
 * permissão (fonte única é o hook). O card inteiro é clicável (drill pro módulo): não precisa de
 * um "ver mais" separado quando o próprio número já é o resumo de uma tela cheia lá.
 */
export function KpiRow({ kpis, onDrill }: KpiRowProps) {
  if (kpis.length === 0) return null;

  return (
    <section className="mb-4 grid grid-cols-2 gap-3.5 sm:grid-cols-3 lg:grid-cols-5">
      {kpis.map((kpi) => (
        <button
          key={kpi.modulo}
          type="button"
          onClick={() => onDrill(kpi.drill)}
          className="block rounded-xl text-left transition-transform hover:-translate-y-0.5 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring active:brightness-95"
        >
          <KpiCard
            hero={kpi.hero}
            label={kpi.label}
            value={
              kpi.formato === 'moeda' ? (
                <MoneyValue centavos={kpi.valorCentavos} className={TONE_CLASS[kpi.tone]} />
              ) : (
                <span className={cn('num', TONE_CLASS[kpi.tone])}>{kpi.valorContagem}</span>
              )
            }
          >
            {kpi.deltaPercentual !== undefined && (
              <div
                className={cn(
                  'inline-flex items-center gap-1 text-[12.5px] font-semibold',
                  kpi.deltaDirecao === 'up' ? 'text-pos' : 'text-crit',
                )}
              >
                {kpi.deltaDirecao === 'up' ? (
                  <ArrowUpRight className="h-3.5 w-3.5" strokeWidth={2.5} />
                ) : (
                  <ArrowDownRight className="h-3.5 w-3.5" strokeWidth={2.5} />
                )}
                {kpi.deltaPercentual}% vs ontem
              </div>
            )}
            <div className={cn('text-xs text-muted-foreground', kpi.deltaPercentual !== undefined && 'mt-[7px]')}>
              {kpi.foot}
            </div>
          </KpiCard>
        </button>
      ))}
    </section>
  );
}
