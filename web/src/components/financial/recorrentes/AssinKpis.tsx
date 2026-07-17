/**
 * KPIs do resumo da lente Assinaturas — mockup: `painelAssinaturas > section.kpis`
 * (mesmos números do `financeiro-assinaturas.html` aprovado).
 */
import { KpiCard } from '@/components/shared';
import type { Centavos } from '@/lib/money';

import { formatCentavosWhole, formatPctPlain, formatPctSigned, formatSignedCentavosWhole } from './calc';
import { Sparkline } from './charts';
import { DeltaRow } from './primitives';

interface AssinKpisProps {
  mrrAtual: Centavos;
  mrrDeltaAbs: Centavos;
  mrrDeltaPct: number;
  sparklineMrr6m: Centavos[];
  churnMesTotal: Centavos;
  churnClientesMesTotal: number;
  churnPctBase: number;
  churnClienteNomes: string;
  novosMaisExpansaoMes: Centavos;
  novosClientesMesTotal: number;
  novoClienteNomes: string;
  arrEstimado: Centavos;
  assinaturasAtivasCount: number;
  ticketMedio: Centavos;
}

export function AssinKpis({
  mrrAtual,
  mrrDeltaAbs,
  mrrDeltaPct,
  sparklineMrr6m,
  churnMesTotal,
  churnClientesMesTotal,
  churnPctBase,
  churnClienteNomes,
  novosMaisExpansaoMes,
  novosClientesMesTotal,
  novoClienteNomes,
  arrEstimado,
  assinaturasAtivasCount,
  ticketMedio,
}: AssinKpisProps) {
  return (
    <section className="mb-4 grid grid-cols-2 gap-3.5 lg:grid-cols-4">
      <KpiCard
        hero
        label="MRR · receita recorrente"
        value={<span className="num">{formatCentavosWhole(mrrAtual)}</span>}
        foot={
          <DeltaRow tone="crit" icon="down">
            {formatSignedCentavosWhole(mrrDeltaAbs)} ({formatPctSigned(mrrDeltaPct)}) no mês
          </DeltaRow>
        }
      >
        <Sparkline values={sparklineMrr6m} />
      </KpiCard>

      <KpiCard
        label="Churn do mês"
        value={<span className="num">{formatCentavosWhole(churnMesTotal)}</span>}
        foot={
          <>
            <DeltaRow tone="crit">
              {churnClientesMesTotal} {churnClientesMesTotal === 1 ? 'cliente' : 'clientes'} · {formatPctPlain(churnPctBase)} da base
            </DeltaRow>
            <div className="mt-[3px] text-xs text-muted-foreground">{churnClienteNomes}</div>
          </>
        }
      />

      <KpiCard
        label="Novos + expansão"
        value={<span className="num">{formatSignedCentavosWhole(novosMaisExpansaoMes)}</span>}
        foot={
          <>
            <DeltaRow tone="pos" icon="up">
              {novosClientesMesTotal} {novosClientesMesTotal === 1 ? 'cliente novo' : 'clientes novos'}
            </DeltaRow>
            <div className="mt-[3px] text-xs text-muted-foreground">{novoClienteNomes}</div>
          </>
        }
      />

      <KpiCard
        label="Valor estimado · ARR"
        value={<span className="num">{formatCentavosWhole(arrEstimado)}</span>}
        foot={
          <>
            {assinaturasAtivasCount} assinaturas ativas · ticket médio{' '}
            <span className="num font-semibold text-foreground">{formatCentavosWhole(ticketMedio)}</span>
          </>
        }
      />
    </section>
  );
}
