import { cn } from '@/lib/utils';

import { STATUS_LABEL, STATUS_TONE_CLASSES } from './calc';
import type { AgendamentoStatus } from './types';

interface AgendaStatusChipProps {
  status: AgendamentoStatus;
  className?: string;
}

/**
 * Chip de status do agendamento (`.chip.st-*` do source, sem hex cru) — vocabulário próprio do
 * módulo: os 6 status da FSM não cabem no `ChipTone` de `@/components/shared/StatusChip`
 * (vocabulário do Financeiro: sobra/falta/aberto/bateu/neutro). Reusa os tokens reservados
 * (pos/warn/crit/primary/faint/surface-2) com variações de tom — mesmo princípio do
 * `OsStatusChip` de Ordem de Serviço.
 */
export function AgendaStatusChip({ status, className }: AgendaStatusChipProps) {
  return (
    <span
      className={cn(
        'inline-flex items-center gap-1.5 whitespace-nowrap rounded-full px-2.5 py-0.5 text-[11.5px] font-semibold',
        STATUS_TONE_CLASSES[status].chip,
        className,
      )}
    >
      <span className="h-1.5 w-1.5 rounded-full bg-current" />
      {STATUS_LABEL[status]}
    </span>
  );
}
