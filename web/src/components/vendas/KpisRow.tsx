import { ArrowDownRight, ArrowUpRight } from 'lucide-react';

import { KpiCard, MoneyValue } from '@/components/shared';
import type { Centavos } from '@/lib/money';
import { cn } from '@/lib/utils';

import { deltaTone, formatPct1 } from './calc';
import { KpiClickable } from './KpiClickable';
import { Sparkline } from './Sparkline';
import type { VendasKpis } from './types';

interface KpisRowProps {
  kpis: VendasKpis;
  /** 5 meses anteriores — o mês corrente (`kpis.vendidoMesCentavos`) entra como 6º ponto do sparkline. */
  historicoVendidoMesCentavos: Centavos[];
  apenasEstornadas: boolean;
  onToggleEstornadas: () => void;
}

/** Linha de delta ("+14,0% vs ontem") dos 3 KPIs não-clicáveis — cresceu é bom (pos), caiu é ruim (crit). */
function DeltaLine({ pct, suffix }: { pct: number; suffix: string }) {
  const tone = deltaTone(pct);
  const Icon = tone === 'pos' ? ArrowUpRight : ArrowDownRight;
  return (
    <div className={cn('flex items-center gap-1 text-[12.5px] font-semibold', tone === 'pos' ? 'text-pos' : 'text-crit')}>
      <Icon className="h-3.5 w-3.5" strokeWidth={2.5} />
      {pct >= 0 ? '+' : ''}
      {formatPct1(pct)}% {suffix}
    </div>
  );
}

/** As 4 KPIs do topo de Vendas: Vendido hoje · Vendido no mês (hero+sparkline) · Ticket médio · Nº de vendas. */
export function KpisRow({ kpis, historicoVendidoMesCentavos, apenasEstornadas, onToggleEstornadas }: KpisRowProps) {
  return (
    <section className="mb-4 grid grid-cols-2 gap-3.5 lg:grid-cols-4">
      <KpiCard label="Vendido hoje" value={<MoneyValue centavos={kpis.vendidoHojeCentavos} />}>
        <DeltaLine pct={kpis.vendidoHojeDeltaPct} suffix="vs ontem" />
      </KpiCard>

      <KpiCard hero label="Vendido no mês" value={<MoneyValue centavos={kpis.vendidoMesCentavos} />}>
        <DeltaLine pct={kpis.vendidoMesDeltaPct} suffix="vs mês anterior" />
        <Sparkline valoresCentavos={[...historicoVendidoMesCentavos, kpis.vendidoMesCentavos]} />
      </KpiCard>

      <KpiCard label="Ticket médio" value={<MoneyValue centavos={kpis.ticketMedioCentavos} />}>
        <DeltaLine pct={kpis.ticketMedioDeltaPct} suffix="vs mês anterior" />
      </KpiCard>

      <KpiClickable label="Nº de vendas" value={kpis.numeroDeVendas} active={apenasEstornadas} onClick={onToggleEstornadas}>
        <div className="mt-[7px] text-[12.5px] font-semibold text-crit">{kpis.numeroDeVendasEstornadas} estornadas</div>
        <div className="mt-[3px] text-xs text-muted-foreground">→ ver estornadas</div>
      </KpiClickable>
    </section>
  );
}
