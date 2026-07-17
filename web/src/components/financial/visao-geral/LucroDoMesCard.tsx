import { ArrowDownRight, ArrowUpRight } from 'lucide-react';

import { Eyebrow, MoneyValue as MoneyValueComCentavos } from '@/components/shared';
import { Surface } from '@/components/ui/Surface';
import { cn } from '@/lib/utils';

import { MoneyValue } from './MoneyValue';
import type { DrillTarget, LucroDoMesViewModel } from './types';

interface LucroDoMesCardProps {
  vm: LucroDoMesViewModel;
  onDrill: (target: DrillTarget) => void;
}

/** "Lucro do mês" (bloco ①b) — é competência, não caixa; por isso o ⓘ e a ponte pro disponível. */
export function LucroDoMesCard({ vm, onDrill }: LucroDoMesCardProps) {
  const isUp = vm.deltaDirecao === 'up';

  return (
    <Surface padding="none" className="flex h-full flex-col p-5 sm:p-[22px]">
      <Eyebrow>
        Lucro do mês
        <InfoTip texto="Competência: conta o que foi vendido/gasto neste mês, mesmo que o dinheiro ainda não tenha mudado de mão." />
      </Eyebrow>

      <div className="mt-2.5 text-[30px] font-extrabold tracking-tight text-foreground">
        <MoneyValue centavos={vm.lucroCentavos} />
      </div>

      <div className={cn('mt-[7px] inline-flex w-fit items-center gap-1 text-[12.5px] font-semibold', isUp ? 'text-pos' : 'text-crit')}>
        {isUp ? <ArrowUpRight className="h-[13px] w-[13px]" /> : <ArrowDownRight className="h-[13px] w-[13px]" />}
        {isUp ? '▲' : '▼'} {vm.deltaPercentual}% vs mês passado
      </div>

      <div className="mt-3 rounded-[11px] bg-surface-2 px-3 py-2.5 text-[13px] leading-relaxed text-foreground">
        De cada <b className="font-bold">R$ 1</b> vendido, sobram{' '}
        <MoneyValueComCentavos centavos={vm.margemPorRealCentavos} className="font-bold" />
      </div>

      <div className="mt-2.5 text-[11.5px] leading-relaxed text-muted-foreground">
        Lucro é maior que o disponível porque{' '}
        <MoneyValue centavos={vm.aReceberCentavos} className="font-bold text-foreground" /> ainda estão pra receber.
      </div>

      <button
        type="button"
        onClick={() => onDrill(vm.verDeOndeVeio)}
        className="mt-auto w-fit self-start pt-3.5 text-[12.5px] font-semibold text-primary-600 hover:underline"
      >
        ver de onde veio →
      </button>
    </Surface>
  );
}

function InfoTip({ texto }: { texto: string }) {
  return (
    <span
      tabIndex={0}
      className="group relative ml-1.5 inline-flex h-[15px] w-[15px] cursor-help items-center justify-center rounded-full bg-surface-2 text-[10px] text-muted-foreground outline-none focus-visible:ring-2 focus-visible:ring-ring"
    >
      ⓘ
      <span className="pointer-events-none absolute bottom-[130%] left-1/2 z-10 w-[226px] -translate-x-1/2 rounded-[9px] bg-foreground px-[11px] py-2.5 text-[11.5px] font-medium leading-[1.45] text-background opacity-0 shadow-xl transition-opacity group-hover:opacity-100 group-focus-visible:opacity-100">
        {texto}
      </span>
    </span>
  );
}
