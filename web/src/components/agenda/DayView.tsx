import { motion } from 'framer-motion';
import { CalendarPlus } from 'lucide-react';

import { EmptyState } from '@/components/ui/EmptyState';
import { cn } from '@/lib/utils';

import { AppointmentBlock } from './AppointmentBlock';
import { END_HOUR, HOUR_HEIGHT, START_HOUR, isHojeReal, minutesToTime, toISODate } from './calc';
import { GridColumn, TimeColumn } from './TimeGridShell';
import type { AgendaVm } from './useAgenda';

const TOTAL_HORAS = END_HOUR - START_HOUR;
const DIA_SEMANA_FORMATTER = new Intl.DateTimeFormat('pt-BR', { weekday: 'long' });

interface DayViewProps {
  vm: AgendaVm;
  slideDirection: 1 | -1;
}

/** Grade de um único dia — 1 coluna, slots clicáveis de 30min, blocos do dia. Porte de
 *  `renderDayView` (L3258-3349) do saas-erp. */
export function DayView({ vm, slideDirection }: DayViewProps) {
  const dia = vm.currentDate;
  const diaISO = toISODate(dia);
  const agendamentosDoDia = vm.agendamentosPorData.get(diaISO) ?? [];
  const ehHoje = isHojeReal(dia);

  return (
    <motion.div
      initial={{ opacity: 0, x: slideDirection * 16 }}
      animate={{ opacity: 1, x: 0 }}
      exit={{ opacity: 0, x: slideDirection * -16 }}
      transition={{ duration: 0.22 }}
      className="flex h-full flex-col overflow-hidden"
    >
      <div className="flex flex-none items-center border-b border-border/60 bg-card px-4 py-3">
        <div className="w-16 flex-shrink-0" />
        <div className="flex-1 text-center">
          <div className={cn('text-xs font-medium uppercase tracking-wider', ehHoje ? 'text-primary-600' : 'text-muted-foreground')}>
            {DIA_SEMANA_FORMATTER.format(dia)}
          </div>
          <div
            className={cn(
              'mt-1 inline-flex h-9 w-9 items-center justify-center rounded-full text-base font-semibold',
              ehHoje ? 'bg-primary-600 text-white' : 'text-foreground',
            )}
          >
            {dia.getDate()}
          </div>
        </div>
      </div>

      <div className="flex-1 overflow-y-auto overflow-x-hidden">
        <div className="relative flex" style={{ height: `${TOTAL_HORAS * HOUR_HEIGHT}px` }}>
          <TimeColumn />
          <GridColumn ehHojeReal={ehHoje}>
            {Array.from({ length: TOTAL_HORAS * 2 }, (_, i) => {
              const hora = START_HOUR + Math.floor(i / 2);
              const horaStr = minutesToTime(hora * 60 + (i % 2) * 30);
              return (
                <div
                  key={`slot-${i}`}
                  className="absolute inset-x-0 z-[5] cursor-pointer transition-colors hover:bg-primary-soft/40"
                  style={{ top: `${i * (HOUR_HEIGHT / 2)}px`, height: `${HOUR_HEIGHT / 2}px` }}
                  onClick={() => vm.abrirNovo(diaISO, horaStr)}
                />
              );
            })}

            {agendamentosDoDia.map((a) => (
              <AppointmentBlock key={a.id} agendamento={a} onClick={vm.abrirVer} />
            ))}

            {agendamentosDoDia.length === 0 && (
              <div className="pointer-events-none absolute inset-0 z-0 flex items-center justify-center px-6">
                <EmptyState
                  icon={<CalendarPlus className="h-5 w-5" />}
                  title="Nenhum agendamento neste dia"
                  description="Clique em um horário na grade para agendar."
                  className="border-none bg-transparent"
                />
              </div>
            )}
          </GridColumn>
        </div>
      </div>
    </motion.div>
  );
}
