import { AnimatePresence } from 'framer-motion';
import { useEffect, useRef, useState } from 'react';

import { Surface } from '@/components/ui/Surface';

import { AgendaToolbar } from './AgendaToolbar';
import { toISODate } from './calc';
import { DayView } from './DayView';
import { MonthView } from './MonthView';
import { ProfissionalFilterBar } from './ProfissionalFilterBar';
import { StatusSummaryBar } from './StatusSummaryBar';
import type { AgendaVm } from './useAgenda';
import { WeekView } from './WeekView';

interface AgendaCalendarCardProps {
  vm: AgendaVm;
}

/**
 * O "hero" da tela — `Surface` de altura fixa hospedando toolbar + filtros + a view ativa
 * (Dia/Semana/Mês, alternadas via `AnimatePresence` com slide horizontal replicando o source).
 * `h-[calc(100vh-260px)]` (não `100vh` cru) porque o `<main>` do `AppShell` já rola a página
 * inteira — dar altura própria ao card evita rolagem dupla (página + grid) brigando (ver README).
 */
export function AgendaCalendarCard({ vm }: AgendaCalendarCardProps) {
  const [direction, setDirection] = useState<1 | -1>(1);
  const prevRef = useRef(vm.currentDate);

  useEffect(() => {
    setDirection(vm.currentDate.getTime() >= prevRef.current.getTime() ? 1 : -1);
    prevRef.current = vm.currentDate;
  }, [vm.currentDate]);

  return (
    <Surface padding="none" rounded="2xl" className="flex h-[calc(100vh-260px)] min-h-[560px] flex-col overflow-hidden">
      <AgendaToolbar vm={vm} />
      <StatusSummaryBar total={vm.agendamentosVisiveis.length} summary={vm.statusSummary} />
      {vm.profissionais.length > 1 && <ProfissionalFilterBar vm={vm} />}

      <div className="flex-1 overflow-hidden">
        <AnimatePresence mode="wait">
          {vm.viewMode === 'dia' && <DayView key={`dia-${toISODate(vm.currentDate)}`} vm={vm} slideDirection={direction} />}
          {vm.viewMode === 'semana' && <WeekView key={`semana-${toISODate(vm.weekDays[0])}`} vm={vm} slideDirection={direction} />}
          {vm.viewMode === 'mes' && (
            <MonthView key={`mes-${vm.currentDate.getFullYear()}-${vm.currentDate.getMonth()}`} vm={vm} slideDirection={direction} />
          )}
        </AnimatePresence>
      </div>
    </Surface>
  );
}
