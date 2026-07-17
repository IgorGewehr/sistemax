import { ArrowDownRight, ArrowUpRight } from 'lucide-react';

import { KpiCard } from '@/components/shared';
import { InfoTooltip } from '@/components/ui/InfoTooltip';
import { cn } from '@/lib/utils';

import { MoneyValue } from './MoneyValue';
import { Sparkline } from './Sparkline';
import type { EntradasSaidasKpis } from './types';

interface KpiRowProps {
  kpis: EntradasSaidasKpis;
}

/** As 4 KPIs de topo (`.kpis` do mockup): a receber, a pagar, resultado (competência) e projeção de caixa. */
export function KpiRow({ kpis }: KpiRowProps) {
  const resultadoSubiu = kpis.resultadoDeltaPct >= 0;
  return (
    <section className="mb-3.5 grid grid-cols-2 gap-3.5 md:grid-cols-4">
      <KpiCard hero label="A receber em aberto" value={<MoneyValue centavos={kpis.aReceberAbertoCentavos} />}>
        <div className="flex items-center gap-1.5 text-[12.5px] font-semibold text-crit">
          <span className="h-[7px] w-[7px] flex-none rounded-full bg-current" />
          <MoneyValue centavos={kpis.aReceberAtrasadoCentavos} /> atrasado
        </div>
        <Sparkline pathLinha={kpis.sparklineReceber.pathLinha} pathArea={kpis.sparklineReceber.pathArea} />
        <div className="mt-[7px] text-xs text-muted-foreground">{kpis.aReceberParcelasAbertas} parcelas em aberto</div>
      </KpiCard>

      <KpiCard label="A pagar em aberto" value={<MoneyValue centavos={kpis.aPagarAbertoCentavos} />}>
        <div className="text-[12.5px] font-semibold text-foreground">
          maior: <b className="font-bold">{kpis.aPagarMaiorLabel}</b> · {kpis.aPagarMaiorData}
        </div>
        <div className="mt-[7px] text-xs text-muted-foreground">{kpis.aPagarLancamentosAbertos} lançamentos abertos</div>
      </KpiCard>

      <KpiCard
        label={
          <span className="inline-flex items-center gap-1">
            Resultado do mês
            <InfoTooltip>Competência: conta o que foi vendido/gasto, mesmo sem o dinheiro ter mudado de mão ainda.</InfoTooltip>
          </span>
        }
        value={<MoneyValue centavos={kpis.resultadoMesCentavos} />}
      >
        <div className={cn('flex items-center gap-1 text-[12.5px] font-semibold', resultadoSubiu ? 'text-pos' : 'text-crit')}>
          {resultadoSubiu ? <ArrowUpRight className="h-3.5 w-3.5" strokeWidth={2.5} /> : <ArrowDownRight className="h-3.5 w-3.5" strokeWidth={2.5} />}
          {resultadoSubiu ? '▲' : '▼'} {Math.abs(kpis.resultadoDeltaPct)}% vs {kpis.resultadoComparadoMes}
        </div>
        <div className="mt-[7px] text-xs text-muted-foreground">regime de competência</div>
      </KpiCard>

      <KpiCard label="Como fecha o mês" value={<MoneyValue centavos={kpis.fechamentoCaixaCentavos} signed />}>
        <div className="text-[12.5px] font-semibold text-foreground">projeção do caixa</div>
        <div className="mt-[7px] text-xs text-muted-foreground">se tudo que vence no mês for pago</div>
      </KpiCard>
    </section>
  );
}
