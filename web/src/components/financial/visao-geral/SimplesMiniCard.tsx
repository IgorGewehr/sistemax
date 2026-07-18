import { Eyebrow } from '@/components/shared';

import { Tile } from './Tile';
import type { DrillTarget, SimplesViewModel } from './types';

interface SimplesMiniCardProps {
  vm: SimplesViewModel;
  onDrill: (target: DrillTarget) => void;
}

/** "1,0" em vez de "1.0" — vírgula decimal pt-BR, mesmo padrão de `KpisRoi.tsx`. */
function formatUmaCasa(valor: number): string {
  return valor.toFixed(1).replace('.', ',');
}

/** Bloco ③c "Simples Nacional" — sempre visível (não é opt-in). Alíquota efetiva + distância até o
 * próximo degrau de faixa. */
export function SimplesMiniCard({ vm, onDrill }: SimplesMiniCardProps) {
  return (
    <Tile onClick={() => onDrill(vm.drill)} className="justify-center">
      <Eyebrow>Simples Nacional</Eyebrow>
      <span className="flex items-baseline gap-1.5 text-[25px] font-extrabold tracking-tight text-foreground">
        {formatUmaCasa(vm.aliquotaPercent)}%<small className="text-xs font-semibold text-muted-foreground">de imposto</small>
      </span>
      <span className="relative mt-0.5 h-2 overflow-hidden rounded-full bg-cat-serv/[0.14]" aria-hidden="true">
        <span className="absolute inset-y-0 left-0 rounded-full bg-cat-serv" style={{ width: `${vm.fillPercent}%` }} />
        <span className="absolute inset-y-[-2px] right-0 w-0.5 rounded-sm bg-foreground/50" />
      </span>
      <span className="text-[11.5px] text-muted-foreground">{vm.distanciaLabel}</span>
    </Tile>
  );
}
