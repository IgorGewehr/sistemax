import { Eyebrow } from '@/components/shared';
import { Surface } from '@/components/ui/Surface';
import { cn } from '@/lib/utils';

import { MoneyValue } from './MoneyValue';
import type { DisponivelViewModel, DrillTarget, LinhaDisponibilidade } from './types';

interface HeroDisponivelCardProps {
  vm: DisponivelViewModel;
  onDrill: (target: DrillTarget) => void;
}

/** Hero "Você pode tirar até" (bloco ①a) — disponível hoje menos o que já tem dono. */
export function HeroDisponivelCard({ vm, onDrill }: HeroDisponivelCardProps) {
  return (
    <Surface
      padding="none"
      className="relative flex h-full flex-col overflow-hidden p-5 pb-[18px] before:pointer-events-none before:absolute before:inset-0 before:bg-[radial-gradient(120%_90%_at_100%_0,hsl(var(--primary)/0.09),transparent_60%)] sm:p-6 sm:pb-5"
    >
      <Eyebrow className="relative z-[1]">Você pode tirar até</Eyebrow>

      <div className="relative z-[1] mt-2 flex items-baseline gap-2.5 text-[34px] font-extrabold tracking-tight text-foreground sm:text-[46px]">
        <MoneyValue centavos={vm.livreDeVerdadeCentavos} />
        <span className="text-xl font-bold text-pos">✓</span>
      </div>

      <div className="relative z-[1] mt-1 flex-1 border-t border-dashed border-border pt-1.5">
        <DecompRow linha={vm.noBancoEGaveta} onDrill={onDrill} />
        <DecompRow linha={vm.jaTemDono} onDrill={onDrill} />

        <div className="mt-0.5 flex items-center justify-between gap-3 rounded-[11px] border-t border-border bg-pos-soft/50 px-2.5 pb-[11px] pt-[13px]">
          <span className="text-sm font-bold text-foreground">Livre de verdade</span>
          <div className="flex items-center gap-3">
            <MoneyValue centavos={vm.livreDeVerdadeCentavos} tone="pos" className="text-[19px] font-bold" />
            <span className="text-[15px] font-bold text-pos">✓</span>
          </div>
        </div>
      </div>
    </Surface>
  );
}

function DecompRow({ linha, onDrill }: { linha: LinhaDisponibilidade; onDrill: (target: DrillTarget) => void }) {
  return (
    <button
      type="button"
      onClick={() => onDrill(linha.drill)}
      className="group grid w-full grid-cols-[1fr_auto_auto] items-center gap-x-3.5 gap-y-1 rounded-[11px] px-2.5 py-2.5 text-left transition-colors hover:bg-surface-2 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
    >
      <span className="text-[13.5px] font-medium text-foreground">
        {linha.label}
        {linha.sublabel && <span className="ml-1 text-xs font-normal text-muted-foreground">{linha.sublabel}</span>}
      </span>
      <MoneyValue
        centavos={linha.valorCentavos}
        signed={linha.tone === 'crit'}
        tone={linha.tone}
        className={cn('text-[15px] font-bold')}
      />
      <span className="whitespace-nowrap text-[11.5px] font-semibold text-muted-foreground opacity-0 transition-opacity group-hover:text-primary-600 group-hover:opacity-100">
        {linha.arrowLabel}
      </span>
    </button>
  );
}
