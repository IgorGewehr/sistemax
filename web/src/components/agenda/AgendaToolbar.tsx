import { CalendarDays, ChevronLeft, ChevronRight, Columns3, LayoutGrid, Plus } from 'lucide-react';

import { Button } from '@/components/ui/Button';
import { cn } from '@/lib/utils';
import { ANCHOR_HOJE } from '@/mocks/agenda';

import { toISODate } from './calc';
import { MiniCalendarPopover } from './MiniCalendarPopover';
import type { ViewMode } from './types';
import type { AgendaVm } from './useAgenda';

interface AgendaToolbarProps {
  vm: AgendaVm;
}

const VIEW_OPTIONS: { mode: ViewMode; label: string; icon: typeof CalendarDays }[] = [
  { mode: 'dia', label: 'Dia', icon: CalendarDays },
  { mode: 'semana', label: 'Semana', icon: Columns3 },
  { mode: 'mes', label: 'Mês', icon: LayoutGrid },
];

/** Prev/Hoje/Próximo + label do período (abre `MiniCalendarPopover`) + toggle Dia/Semana/Mês +
 *  botão "Novo agendamento". Porte da HEADER BAR do saas-erp (L3593-3714). */
export function AgendaToolbar({ vm }: AgendaToolbarProps) {
  const emHoje = toISODate(vm.currentDate) === toISODate(ANCHOR_HOJE);

  return (
    <div className="flex flex-none flex-col gap-3 border-b border-border/60 px-4 py-4 sm:flex-row sm:items-center sm:justify-between sm:px-6">
      <div className="flex items-center gap-2 sm:gap-3">
        <div className="flex items-center gap-0.5 rounded-xl bg-surface-2 p-0.5">
          <button
            type="button"
            onClick={vm.navegarAnterior}
            title="Anterior"
            className="rounded-lg p-2 text-muted-foreground transition-colors hover:bg-card hover:text-foreground active:brightness-95"
          >
            <ChevronLeft className="h-4 w-4" />
          </button>
          <button
            type="button"
            onClick={vm.irParaHoje}
            className={cn(
              'rounded-lg px-3 py-1.5 text-xs font-semibold transition-colors active:brightness-95',
              emHoje ? 'bg-primary-600 text-white shadow-sm' : 'text-muted-foreground hover:bg-card hover:text-foreground',
            )}
          >
            Hoje
          </button>
          <button
            type="button"
            onClick={vm.navegarProximo}
            title="Próximo"
            className="rounded-lg p-2 text-muted-foreground transition-colors hover:bg-card hover:text-foreground active:brightness-95"
          >
            <ChevronRight className="h-4 w-4" />
          </button>
        </div>

        <MiniCalendarPopover vm={vm} />
      </div>

      <div className="flex items-center gap-2 sm:gap-3">
        <div className="flex items-center gap-0.5 rounded-xl bg-surface-2 p-0.5">
          {VIEW_OPTIONS.map(({ mode, label, icon: Icon }) => (
            <button
              key={mode}
              type="button"
              onClick={() => vm.setViewMode(mode)}
              className={cn(
                'flex items-center gap-1.5 rounded-lg px-3 py-1.5 text-xs font-medium transition-colors active:brightness-95',
                vm.viewMode === mode ? 'bg-card text-foreground shadow-sm' : 'text-muted-foreground hover:text-foreground',
              )}
            >
              <Icon className="h-3.5 w-3.5" />
              <span className="hidden sm:inline">{label}</span>
            </button>
          ))}
        </div>

        <Button size="md" icon={<Plus className="h-4 w-4" />} onClick={() => vm.abrirNovo()}>
          <span className="hidden sm:inline">Novo agendamento</span>
          <span className="sm:hidden">Novo</span>
        </Button>
      </div>
    </div>
  );
}
