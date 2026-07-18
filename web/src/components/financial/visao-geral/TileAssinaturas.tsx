import { Eyebrow } from '@/components/shared';

import { DecorativeSparkline } from './DecorativeSparkline';
import { MoneyValue } from './MoneyValue';
import { Tile } from './Tile';
import type { DrillTarget, TileAssinaturasViewModel } from './types';

interface TileAssinaturasProps {
  vm: TileAssinaturasViewModel;
  onDrill: (target: DrillTarget) => void;
}

/** Tile ④ "Assinaturas" — MRR e quantas assinaturas ativas. */
export function TileAssinaturas({ vm, onDrill }: TileAssinaturasProps) {
  return (
    <Tile onClick={() => onDrill(vm.drill)}>
      <Eyebrow>Assinaturas</Eyebrow>
      <span className="flex items-baseline gap-2 text-[25px] font-extrabold tracking-tight text-foreground">
        <MoneyValue centavos={vm.mrrCentavos} />
        <small className="text-[13px] font-semibold text-muted-foreground">/mês</small>
      </span>
      <DecorativeSparkline tone="pos" />
      <span className="text-[11.5px] text-muted-foreground">{vm.assinaturasAtivas} ativas</span>
    </Tile>
  );
}
