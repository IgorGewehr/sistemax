import { cn } from '@/lib/utils';

import { STATUS_LABEL } from './calc';
import type { OsStatus } from './types';

/**
 * Chip de status da OS (`.chip.st-*` do mockup) — vocabulário próprio do módulo: as 10 fases da
 * FSM não cabem no `ChipTone` de `@/components/shared/StatusChip` (que é o vocabulário do
 * Financeiro: sobra/falta/aberto/bateu/neutro). Reusa os mesmos tokens de cor reservados
 * (pos/crit/warn/faint/surface-2), nunca HSL cru.
 */
const TONE_CLASSES: Record<OsStatus, string> = {
  Aberta: 'text-muted-foreground bg-surface-2',
  // No mockup esse chip usa uma cor de marca própria (navy); aproximamos com o par
  // foreground/surface-2 pra não introduzir um token de cor novo (Lei 1: resultado visual, não CSS cru).
  EmDiagnostico: 'text-foreground bg-surface-2',
  AguardandoAprovacao: 'text-warn bg-warn-soft',
  Aprovada: 'text-pos bg-pos-soft',
  EmExecucao: 'text-primary-600 bg-primary-soft',
  Pronta: 'text-pos bg-pos-soft',
  Entregue: 'text-faint bg-surface-2',
  Reprovada: 'text-crit bg-crit-soft',
  DevolvidaSemReparo: 'text-faint bg-surface-2',
  Cancelada: 'text-faint bg-surface-2',
};

interface OsStatusChipProps {
  status: OsStatus;
  /** `Pronta` vira crítico quando o prazo já passou (`.chip.st-pronta.atraso` do mockup). */
  atrasada?: boolean;
  className?: string;
}

export function OsStatusChip({ status, atrasada = false, className }: OsStatusChipProps) {
  const tone = status === 'Pronta' && atrasada ? 'text-crit bg-crit-soft' : TONE_CLASSES[status];
  return (
    <span
      className={cn(
        'inline-flex items-center gap-1.5 whitespace-nowrap rounded-full px-2.5 py-0.5 text-[11.5px] font-semibold',
        tone,
        className,
      )}
    >
      <span className="h-1.5 w-1.5 rounded-full bg-current" />
      {STATUS_LABEL[status]}
    </span>
  );
}
