import { Eyebrow } from '@/components/shared';

import { MoneyValue } from './MoneyValue';
import { Tile } from './Tile';
import type { DrillTarget, TileAReceberViewModel } from './types';

interface TileAReceberProps {
  vm: TileAReceberViewModel;
  onDrill: (target: DrillTarget) => void;
}

/** Tile ① "A receber" — total em aberto + barra dividida em-dia/atrasado. */
export function TileAReceber({ vm, onDrill }: TileAReceberProps) {
  const temAtraso = vm.atrasadoCentavos > 0;

  return (
    <Tile onClick={() => onDrill(vm.drill)}>
      <Eyebrow>A receber</Eyebrow>
      <MoneyValue centavos={vm.totalCentavos} className="text-[25px] font-extrabold tracking-tight text-foreground" />
      <span className="flex h-[7px] w-full max-w-[128px] gap-[2px] overflow-hidden rounded-full" aria-hidden="true">
        <span className="h-full rounded-full bg-pos" style={{ width: `${vm.pctEmDia}%` }} />
        {temAtraso && <span className="h-full rounded-full bg-warn" style={{ width: `${vm.pctAtrasado}%` }} />}
      </span>
      <span className="text-[11.5px] text-muted-foreground">
        {temAtraso ? (
          <>
            <MoneyValue centavos={vm.atrasadoCentavos} className="font-bold text-warn" /> atrasado
          </>
        ) : (
          <span className="font-semibold text-pos">tudo em dia</span>
        )}
      </span>
    </Tile>
  );
}
