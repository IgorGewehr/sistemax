import { Eyebrow } from '@/components/shared';

import { MoneyValue } from './MoneyValue';
import { Tile } from './Tile';
import type { DrillTarget, InvestimentoViewModel } from './types';

interface InvestimentoMiniCardProps {
  vm: InvestimentoViewModel;
  onDrill: (target: DrillTarget) => void;
}

/** Bloco ③b "Investimento" — opt-in (`imobilizadoRoiAtivo`). % recuperado do total investido. */
export function InvestimentoMiniCard({ vm, onDrill }: InvestimentoMiniCardProps) {
  const largura = Math.min(100, Math.max(0, vm.percentRecuperado));

  return (
    <Tile onClick={() => onDrill(vm.drill)} className="justify-center">
      <Eyebrow>Investimento</Eyebrow>
      <span className="flex items-baseline gap-1.5 text-[25px] font-extrabold tracking-tight text-foreground">
        {vm.percentRecuperado}%<small className="text-xs font-semibold text-muted-foreground">recuperado</small>
      </span>
      <span className="relative mt-0.5 h-2 overflow-hidden rounded-full bg-pos/[0.14]" aria-hidden="true">
        <span className="absolute inset-y-0 left-0 rounded-full bg-pos" style={{ width: `${largura}%` }} />
      </span>
      <span className="text-[11.5px] text-muted-foreground">
        <MoneyValue centavos={vm.recuperadoCentavos} /> de <MoneyValue centavos={vm.totalCentavos} />
      </span>
    </Tile>
  );
}
