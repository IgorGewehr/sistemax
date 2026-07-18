import type { ReactNode } from 'react';

import { KpiCard, MoneyValue } from '@/components/shared';
import { cn } from '@/lib/utils';

import type { KpisLista } from './types';

interface KpiRowProps {
  kpis: KpisLista;
}

/** As 4 KPI cards da fila (`.kpis` do mockup) — bancada, espera do cliente, prontas, faturado do mês. */
export function KpiRow({ kpis }: KpiRowProps) {
  const { naBancada, esperando, prontas, faturado } = kpis;
  const faturadoUp = faturado.deltaCentavos >= 0;

  return (
    <section className="grid grid-cols-2 gap-3.5 lg:grid-cols-4">
      <KpiCard
        hero
        label="Na bancada"
        value={
          <>
            {naBancada.count} <small className="text-[15px] font-semibold text-muted-foreground">OS</small>
          </>
        }
      >
        <DeltaLine>
          <MoneyValue centavos={naBancada.valorCentavos} /> em potencial
        </DeltaLine>
        <Sparkline />
        <Foot>entregas concluídas por semana</Foot>
      </KpiCard>

      <KpiCard label="Esperando o cliente" value={<MoneyValue centavos={esperando.valorCentavos} />}>
        <DeltaLine tone="down">
          {esperando.count} orçamento{esperando.count === 1 ? '' : 's'} em aberto
        </DeltaLine>
        <Foot>{esperando.diasMedio.toFixed(1).replace('.', ',')} dias médios de espera</Foot>
      </KpiCard>

      <KpiCard label="Prontas p/ retirada" value={<MoneyValue centavos={prontas.valorCentavos} />}>
        <DeltaLine>{prontas.count} OS na prateleira</DeltaLine>
        <Foot>{prontas.count ? `mais antiga: há ${prontas.maisAntigaDias}d` : 'nenhuma no momento'}</Foot>
      </KpiCard>

      <KpiCard label="Faturado no mês" value={<MoneyValue centavos={faturado.valorCentavos} />}>
        <DeltaLine tone={faturadoUp ? 'up' : 'down'}>
          {faturadoUp ? '▲' : '▼'} {Math.abs(faturado.deltaPct)}% vs jun
        </DeltaLine>
        <Foot>
          serviço {faturado.servicoPct}% · peças {faturado.pecasPct}%
        </Foot>
      </KpiCard>
    </section>
  );
}

function DeltaLine({ tone, children }: { tone?: 'up' | 'down'; children: ReactNode }) {
  return (
    <div
      className={cn(
        'num inline-flex items-center gap-1 text-[12.5px] font-semibold',
        tone === 'up' ? 'text-pos' : tone === 'down' ? 'text-crit' : 'text-foreground',
      )}
    >
      {children}
    </div>
  );
}

function Foot({ children }: { children: ReactNode }) {
  return <div className="mt-[3px] text-xs text-muted-foreground">{children}</div>;
}

/**
 * Traço ilustrativo de "entregas por semana" — no mockup é um path estático (não deriva dos
 * dados), então reproduzimos a mesma curva ao invés de fabricar uma série sintética.
 */
function Sparkline() {
  return (
    <svg className="mt-2.5 block h-[34px] w-full" viewBox="0 0 260 34" preserveAspectRatio="none" aria-hidden="true">
      <defs>
        <linearGradient id="kpi-os-spark" x1="0" y1="0" x2="0" y2="1">
          <stop offset="0" stopColor="hsl(var(--primary))" stopOpacity={0.28} />
          <stop offset="1" stopColor="hsl(var(--primary))" stopOpacity={0} />
        </linearGradient>
      </defs>
      <path
        d="M0,24 L52,20 L104,14 L156,17 L208,10 L260,13"
        fill="none"
        className="stroke-primary-600"
        strokeWidth={2}
        strokeLinecap="round"
      />
      <path d="M0,24 L52,20 L104,14 L156,17 L208,10 L260,13 L260,34 L0,34 Z" fill="url(#kpi-os-spark)" />
      <circle cx="260" cy="13" r="3" className="fill-primary-600" />
    </svg>
  );
}
