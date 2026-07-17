import { motion } from 'framer-motion';

import { cn } from '@/lib/utils';

import { AppointmentBlock } from './AppointmentBlock';
import { END_HOUR, HOUR_HEIGHT, START_HOUR, WEEKDAY_LABELS_PT, isHojeReal, minutesToTime, toISODate } from './calc';
import { GridColumn, TimeColumn } from './TimeGridShell';
import type { AgendaVm } from './useAgenda';

const TOTAL_HORAS = END_HOUR - START_HOUR;

interface WeekViewProps {
  vm: AgendaVm;
  slideDirection: 1 | -1;
}

/** Grade da semana — 7 colunas com cabeçalho de dia (num. + contagem). Porte de
 *  `renderWeekView` (L3352-3467) do saas-erp. `TimeColumn` aparece 1x; `GridColumn` 7x (uma por
 *  dia) — por isso `TimeGridShell` é "compartilhado dia/semana". */
export function WeekView({ vm, slideDirection }: WeekViewProps) {
  return (
    <motion.div
      initial={{ opacity: 0, x: slideDirection * 16 }}
      animate={{ opacity: 1, x: 0 }}
      exit={{ opacity: 0, x: slideDirection * -16 }}
      transition={{ duration: 0.22 }}
      className="flex h-full flex-col overflow-hidden"
    >
      <div className="sticky top-0 z-20 flex flex-none border-b border-border/60 bg-card">
        <div className="w-16 flex-shrink-0 border-r border-border/60" />
        {vm.weekDays.map((dia, i) => {
          const diaISO = toISODate(dia);
          const ehHoje = isHojeReal(dia);
          const count = vm.agendamentosPorData.get(diaISO)?.length ?? 0;
          return (
            <div
              key={diaISO}
              className={cn(
                'min-w-[100px] flex-1 border-r border-border/60 py-2.5 text-center last:border-r-0',
                ehHoje && 'bg-primary-soft/40',
              )}
            >
              <div className={cn('text-[11px] font-medium uppercase tracking-wider', ehHoje ? 'text-primary-600' : 'text-muted-foreground')}>
                {WEEKDAY_LABELS_PT[i]}
              </div>
              <button
                type="button"
                onClick={() => {
                  vm.irParaData(dia);
                  vm.setViewMode('dia');
                }}
                className={cn(
                  'mt-0.5 inline-flex h-8 w-8 items-center justify-center rounded-full text-sm font-semibold transition-colors active:brightness-95',
                  ehHoje ? 'bg-primary-600 text-white' : 'text-foreground hover:bg-secondary',
                )}
              >
                {dia.getDate()}
              </button>
              {count > 0 && <div className="mt-0.5 text-[10px] text-muted-foreground">{count} agend.</div>}
            </div>
          );
        })}
      </div>

      <div className="flex-1 overflow-auto">
        <div className="relative flex" style={{ height: `${TOTAL_HORAS * HOUR_HEIGHT}px`, minWidth: '760px' }}>
          <TimeColumn />
          {vm.weekDays.map((dia) => {
            const diaISO = toISODate(dia);
            const ehHoje = isHojeReal(dia);
            const agendamentosDoDia = vm.agendamentosPorData.get(diaISO) ?? [];
            return (
              <GridColumn
                key={diaISO}
                ehHojeReal={ehHoje}
                className={cn('min-w-[100px] border-r border-border/60 last:border-r-0', ehHoje && 'bg-primary-soft/10')}
              >
                {Array.from({ length: TOTAL_HORAS * 2 }, (_, i) => {
                  const hora = START_HOUR + Math.floor(i / 2);
                  const horaStr = minutesToTime(hora * 60 + (i % 2) * 30);
                  return (
                    <div
                      key={`slot-${diaISO}-${i}`}
                      className="absolute inset-x-0 z-[5] cursor-pointer transition-colors hover:bg-primary-soft/40"
                      style={{ top: `${i * (HOUR_HEIGHT / 2)}px`, height: `${HOUR_HEIGHT / 2}px` }}
                      onClick={() => vm.abrirNovo(diaISO, horaStr)}
                    />
                  );
                })}
                {agendamentosDoDia.map((a) => (
                  <AppointmentBlock key={a.id} agendamento={a} onClick={vm.abrirVer} compact />
                ))}
              </GridColumn>
            );
          })}
        </div>
      </div>
    </motion.div>
  );
}
