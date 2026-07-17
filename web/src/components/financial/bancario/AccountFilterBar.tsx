import { Landmark, Upload } from 'lucide-react';

import { Button } from '@/components/ui/Button';
import { cn } from '@/lib/utils';

import type { ContaBancaria } from './types';

/** Chip "Todas" + 1 chip por conta real — ids são os ULIDs de `ContaBancariaCaixa`, não mais um
 * catálogo fixo ('itau'/'nubank'/'stone') como no mock (docs/wiring/financeiro-telas-restantes.md
 * §3): o filtro do extrato agora acompanha as contas de verdade cadastradas no tenant. */
export type AccountChipId = string;
export const TODAS_AS_CONTAS: AccountChipId = 'todas';

interface AccountFilterBarProps {
  contas: ContaBancaria[];
  selected: AccountChipId;
  onSelect: (id: AccountChipId) => void;
}

/** Linha "subhead": chips de conta (filtro real do extrato) + ações Importar OFX / Conectar banco. */
export function AccountFilterBar({ contas, selected, onSelect }: AccountFilterBarProps) {
  const chips = [{ id: TODAS_AS_CONTAS, label: 'Todas' }, ...contas.map((c) => ({ id: c.id, label: c.label }))];

  return (
    <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
      <div className="inline-flex flex-wrap gap-0.5 rounded-[11px] border border-border bg-surface-2 p-[3px]">
        {chips.map((chip) => (
          <button
            key={chip.id}
            type="button"
            onClick={() => onSelect(chip.id)}
            className={cn(
              'rounded-lg px-3.5 py-1.5 text-sm font-semibold transition-colors',
              selected === chip.id ? 'bg-card text-foreground shadow-sm' : 'text-muted-foreground hover:text-foreground',
            )}
          >
            {chip.label}
          </button>
        ))}
      </div>
      <div className="flex flex-wrap items-center gap-2.5">
        <Button variant="outline" size="sm" icon={<Upload className="h-3.5 w-3.5" />}>
          Importar OFX
        </Button>
        <Button variant="outline" size="sm" icon={<Landmark className="h-3.5 w-3.5" />}>
          Conectar banco
        </Button>
      </div>
    </div>
  );
}
