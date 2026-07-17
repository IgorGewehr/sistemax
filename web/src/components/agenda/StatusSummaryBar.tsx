import { cn } from '@/lib/utils';

import { STATUS_LABEL, STATUS_TONE_CLASSES } from './calc';
import type { AgendamentoStatus } from './types';

interface StatusSummaryBarProps {
  total: number;
  summary: Record<AgendamentoStatus, number>;
}

const ORDEM: AgendamentoStatus[] = ['agendado', 'confirmado', 'em_andamento', 'concluido', 'cancelado', 'nao_compareceu'];

/** "N agendamentos · N confirmados · N em andamento…" do período visível. Porte do bloco STATUS
 *  SUMMARY (L3716-3738) do saas-erp. */
export function StatusSummaryBar({ total, summary }: StatusSummaryBarProps) {
  return (
    <div className="flex flex-none items-center gap-1.5 overflow-x-auto border-b border-border/60 bg-surface-2/50 px-4 py-2 sm:px-6">
      <span className="mr-1 whitespace-nowrap text-xs text-muted-foreground">
        {total} {total === 1 ? 'agendamento' : 'agendamentos'}
      </span>
      <div className="mx-1 h-4 w-px flex-none bg-border" />
      {ORDEM.filter((status) => summary[status] > 0).map((status) => (
        <span
          key={status}
          className={cn(
            'inline-flex items-center gap-1 whitespace-nowrap rounded-md px-2 py-0.5 text-[10px] font-medium',
            STATUS_TONE_CLASSES[status].chip,
          )}
        >
          <span className="h-1.5 w-1.5 rounded-full bg-current" />
          {summary[status]} {STATUS_LABEL[status]}
        </span>
      ))}
    </div>
  );
}
