/**
 * KPIs da lente Contas fixas — mockup: `#kpisFixas` (`renderKpisFixas`).
 */
import { KpiCard } from '@/components/shared';
import { InfoTooltip } from '@/components/ui/InfoTooltip';
import type { Centavos } from '@/lib/money';

import { formatCentavosWhole, formatPctSigned, formatSignedCentavosWhole } from './calc';
import { Sparkline } from './charts';
import { DeltaRow } from './primitives';
import type { ContaFixaDerivada } from './types';

interface FixasKpisProps {
  totalAtual: Centavos;
  custoPorDia: Centavos;
  diasUteisMes: number;
  deltaAbs: Centavos;
  deltaPct: number;
  notaVariacaoMensal: string;
  pesoReceitaPct: number;
  receitaMediaReferencia: Centavos;
  maiorPendente: ContaFixaDerivada | null;
  serieMensal: Centavos[];
}

export function FixasKpis({
  totalAtual,
  custoPorDia,
  diasUteisMes,
  deltaAbs,
  deltaPct,
  notaVariacaoMensal,
  pesoReceitaPct,
  receitaMediaReferencia,
  maiorPendente,
  serieMensal,
}: FixasKpisProps) {
  return (
    <section className="mb-4 grid grid-cols-2 gap-3.5 lg:grid-cols-4">
      <KpiCard
        hero
        label="Custo de existir"
        value={
          <span className="num">
            {formatCentavosWhole(totalAtual)}
            <small className="ml-1 text-[15px] font-semibold text-muted-foreground">/mês</small>
          </span>
        }
        foot={
          <>
            = {formatCentavosWhole(custoPorDia)} por dia útil só pra abrir as portas{' '}
            <span className="opacity-70">({diasUteisMes} dias úteis)</span>
          </>
        }
      >
        <Sparkline values={serieMensal} />
      </KpiCard>

      <KpiCard
        label="Vs. mês passado"
        value={<span className="num">{formatSignedCentavosWhole(deltaAbs)}</span>}
        foot={
          <>
            <DeltaRow tone="crit" icon="down">
              {formatPctSigned(deltaPct)} desde o mês passado
            </DeltaRow>
            <div className="mt-[3px] text-xs text-muted-foreground">{notaVariacaoMensal}</div>
          </>
        }
      />

      <KpiCard
        label={
          <>
            Peso na receita{' '}
            <InfoTooltip>
              {`Custo fixo ÷ receita média dos últimos 3 meses (${formatCentavosWhole(receitaMediaReferencia)}). Acima de 60% é sinal de alerta.`}
            </InfoTooltip>
          </>
        }
        value={
          <span className="num">
            {Math.round(pesoReceitaPct)}
            <small className="text-[15px] font-semibold text-muted-foreground">%</small>
          </span>
        }
        foot="da receita média"
      />

      <KpiCard
        label="Próxima grande"
        valueClassName="text-[19px]"
        value={
          maiorPendente ? (
            <>
              {maiorPendente.nome.split(' ')[0]}{' '}
              <span className="num text-[13px] font-semibold text-muted-foreground">· {maiorPendente.proximaLabel}</span>
            </>
          ) : (
            '—'
          )
        }
        foot={maiorPendente && <span className="num text-sm font-bold text-foreground">{formatCentavosWhole(maiorPendente.atual)}</span>}
      />
    </section>
  );
}
