import { useMemo } from 'react';

import { Eyebrow } from '@/components/shared';
import { cn } from '@/lib/utils';

import { MoneyValue } from './MoneyValue';
import { Tile } from './Tile';
import type { CorrenteChave, DrillTarget, MixViewModel, SegmentoMix } from './types';

interface MixCorrentesCardProps {
  vm: MixViewModel;
  onDrill: (target: DrillTarget) => void;
}

const CAT_CLASS: Record<CorrenteChave, { dot: string; stroke: string }> = {
  serv: { dot: 'bg-cat-serv', stroke: 'stroke-cat-serv' },
  rec: { dot: 'bg-cat-rec', stroke: 'stroke-cat-rec' },
  com: { dot: 'bg-cat-com', stroke: 'stroke-cat-com' },
};

const CX = 58;
const CY = 58;
const R = 44;
const SW = 15;
const GAPF = 4 / 360;

function donutArcos(segmentos: SegmentoMix[]): { chave: CorrenteChave; d: string }[] {
  let f = 0;
  const arcos: { chave: CorrenteChave; d: string }[] = [];

  segmentos.forEach((seg) => {
    const fracao = seg.percent / 100;
    if (fracao <= 0) return;
    const f0 = f + GAPF / 2;
    const f1 = f + fracao - GAPF / 2;
    const a0 = -Math.PI / 2 + 2 * Math.PI * f0;
    const a1 = -Math.PI / 2 + 2 * Math.PI * f1;
    const x0 = CX + R * Math.cos(a0);
    const y0 = CY + R * Math.sin(a0);
    const x1 = CX + R * Math.cos(a1);
    const y1 = CY + R * Math.sin(a1);
    const largo = f1 - f0 > 0.5 ? 1 : 0;
    arcos.push({ chave: seg.chave, d: `M ${x0.toFixed(1)},${y0.toFixed(1)} A ${R},${R} 0 ${largo} 1 ${x1.toFixed(1)},${y1.toFixed(1)}` });
    f += fracao;
  });

  return arcos;
}

/** Bloco ③a "De onde vem" — rosca do mix de receita nas 3 correntes (Serviço/Assinatura/Loja). */
export function MixCorrentesCard({ vm, onDrill }: MixCorrentesCardProps) {
  const arcos = useMemo(() => donutArcos(vm.segmentos), [vm.segmentos]);

  return (
    <Tile onClick={() => onDrill(vm.drill)} className="sm:col-span-2 lg:col-span-1">
      <Eyebrow>De onde vem</Eyebrow>
      <div className="flex flex-1 items-center gap-4.5">
        <div
          className="relative h-[116px] w-[116px] flex-none"
          role="img"
          aria-label={`Receita do mês, ${vm.segmentos.map((s) => `${s.label} ${s.percent}%`).join(', ')}`}
        >
          <svg viewBox="0 0 116 116" className="block h-[116px] w-[116px]" aria-hidden="true">
            {arcos.map((a) => (
              <path key={a.chave} d={a.d} fill="none" strokeWidth={SW} className={CAT_CLASS[a.chave].stroke} />
            ))}
          </svg>
          <div className="pointer-events-none absolute inset-0 grid place-items-center text-center">
            <div>
              <MoneyValue centavos={vm.totalCentavos} className="block text-[14.5px] font-extrabold tracking-tight" />
              <div className="text-[10.5px] font-semibold text-muted-foreground">no mês</div>
            </div>
          </div>
        </div>

        <div className="flex min-w-0 flex-1 flex-col gap-2">
          {vm.segmentos.map((s) => (
            <div key={s.chave} className="flex items-center gap-2.5 text-[12.5px] font-semibold">
              <i className={cn('h-[9px] w-[9px] flex-none rounded-[3px]', CAT_CLASS[s.chave].dot)} />
              <span className="min-w-0 flex-1 truncate">{s.label}</span>
              <span className="text-[13px] font-bold text-foreground">{s.percent}%</span>
            </div>
          ))}
        </div>
      </div>
    </Tile>
  );
}
