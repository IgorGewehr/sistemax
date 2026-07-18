import { Eyebrow } from '@/components/shared';
import { formatCentavosWhole } from '@/lib/money';
import { cn } from '@/lib/utils';

import { MoneyValue } from './MoneyValue';
import { Tile } from './Tile';
import type { DrillTarget, TileAPagarViewModel } from './types';

interface TileAPagarProps {
  vm: TileAPagarViewModel;
  onDrill: (target: DrillTarget) => void;
}

/** Tile ② "A pagar" — total em aberto + colunas semanais de quando o dinheiro sai. */
export function TileAPagar({ vm, onDrill }: TileAPagarProps) {
  return (
    <Tile onClick={() => onDrill(vm.drill)}>
      <Eyebrow>A pagar</Eyebrow>
      <MoneyValue centavos={vm.totalCentavos} className="text-[25px] font-extrabold tracking-tight text-foreground" />
      <span className="flex h-7 items-end gap-1.5" aria-hidden="true">
        {vm.semanas.map((s) => (
          <span
            key={s.label}
            title={`${s.label} · ${formatCentavosWhole(s.valorCentavos)}`}
            className={cn('block w-5 rounded-t-[3px]', s.destaque ? 'bg-foreground/55' : 'bg-foreground/[0.22]')}
            style={{ height: `${Math.max(3, (s.alturaPct / 100) * 28)}px` }}
          />
        ))}
      </span>
      <span className="truncate text-[11.5px] text-muted-foreground">
        maior: {vm.maiorLabel} · {vm.maiorDataLabel}
      </span>
    </Tile>
  );
}
