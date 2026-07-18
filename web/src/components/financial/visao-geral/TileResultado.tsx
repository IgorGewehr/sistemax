import { Eyebrow } from '@/components/shared';
import { cn } from '@/lib/utils';

import { DecorativeSparkline } from './DecorativeSparkline';
import { MoneyValue } from './MoneyValue';
import { Tile } from './Tile';
import type { DrillTarget, TileResultadoViewModel } from './types';

interface TileResultadoProps {
  vm: TileResultadoViewModel;
  onDrill: (target: DrillTarget) => void;
}

/** Tile ③ "Resultado" — resultado do mês, delta vs mês passado e margem. */
export function TileResultado({ vm, onDrill }: TileResultadoProps) {
  const isUp = vm.deltaDirecao === 'up';

  return (
    <Tile onClick={() => onDrill(vm.drill)}>
      <Eyebrow>Resultado</Eyebrow>
      <span className="flex flex-wrap items-baseline gap-2 text-[25px] font-extrabold tracking-tight text-foreground">
        <MoneyValue centavos={vm.resultadoCentavos} />
        <span className={cn('inline-flex items-center gap-[3px] text-xs font-bold', isUp ? 'text-pos' : 'text-crit')}>
          {isUp ? '▲' : '▼'} {vm.deltaPercentual}%
        </span>
      </span>
      <DecorativeSparkline tone={isUp ? 'pos' : 'crit'} />
      <span className="text-[11.5px] text-muted-foreground">margem {vm.margemPercent}%</span>
    </Tile>
  );
}
