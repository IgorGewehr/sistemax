/**
 * Controle segmentado "Contas fixas ⇄ Assinaturas" + nota de contexto — mockup:
 * `#segControl` + `#ctxNote`. Puramente navegação de UI local à tela (não IA).
 */
import { Info } from 'lucide-react';

import { cn } from '@/lib/utils';

import type { LenteRecorrentes } from './types';

interface LensSwitchProps {
  lens: LenteRecorrentes;
  onChange: (lens: LenteRecorrentes) => void;
}

export function LensSwitch({ lens, onChange }: LensSwitchProps) {
  return (
    <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
      <div className="inline-flex gap-0.5 rounded-xl border border-border bg-surface-2 p-[3px]">
        <button
          type="button"
          onClick={() => onChange('fixas')}
          className={cn(
            'rounded-lg px-3.5 py-1.5 text-sm font-semibold transition-colors',
            lens === 'fixas' ? 'bg-card text-foreground shadow-sm' : 'text-muted-foreground hover:text-foreground',
          )}
        >
          Contas fixas <span className="font-medium opacity-60">· luz, aluguel, salários</span>
        </button>
        <button
          type="button"
          onClick={() => onChange('assinaturas')}
          className={cn(
            'rounded-lg px-3.5 py-1.5 text-sm font-semibold transition-colors',
            lens === 'assinaturas' ? 'bg-card text-foreground shadow-sm' : 'text-muted-foreground hover:text-foreground',
          )}
        >
          Assinaturas
        </button>
      </div>
      <div
        className={cn(
          'inline-flex items-center gap-1.5 text-xs text-muted-foreground transition-opacity',
          lens === 'fixas' ? 'invisible' : 'visible',
        )}
      >
        <Info className="h-3.5 w-3.5 shrink-0 text-primary-600" />
        Visível porque este negócio vende serviço recorrente
      </div>
    </div>
  );
}
