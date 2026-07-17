import { motion } from 'framer-motion';

import { cn } from '@/lib/utils';

import { STATUS_TONE_CLASSES, WEEKDAY_LABELS_PT, isHojeReal, isMesmoMes, toISODate } from './calc';
import type { AgendaVm } from './useAgenda';

const MAX_PREVIEW = 3;

interface MonthViewProps {
  vm: AgendaVm;
  slideDirection: 1 | -1;
}

/** Grade 6×7 (42 células) do mês — até 3 agendamentos por célula + "+N mais"; clique numa célula
 *  navega pro Dia. Porte de `renderMonthView` (L3472-3567) do saas-erp. Sem `whileHover`/motion
 *  por preview (risco de perf com até 42×3 nós) — só fade de entrada na célula, igual ao source. */
export function MonthView({ vm, slideDirection }: MonthViewProps) {
  return (
    <motion.div
      initial={{ opacity: 0, x: slideDirection * 16 }}
      animate={{ opacity: 1, x: 0 }}
      exit={{ opacity: 0, x: slideDirection * -16 }}
      transition={{ duration: 0.22 }}
      className="flex h-full flex-col overflow-hidden"
    >
      <div className="grid flex-none grid-cols-7 border-b border-border/60 bg-card">
        {WEEKDAY_LABELS_PT.map((label) => (
          <div key={label} className="border-r border-border/60 py-2.5 text-center text-[11px] font-semibold uppercase tracking-wider text-muted-foreground last:border-r-0">
            {label}
          </div>
        ))}
      </div>

      <div className="flex-1 overflow-y-auto">
        <div className="grid grid-cols-7">
          {vm.monthDays.map((dia) => {
            const diaISO = toISODate(dia);
            const noMesAtual = isMesmoMes(dia, vm.currentDate);
            const ehHoje = isHojeReal(dia);
            const agendamentosDoDia = vm.agendamentosPorData.get(diaISO) ?? [];
            const overflow = agendamentosDoDia.length - MAX_PREVIEW;

            return (
              <div
                key={diaISO}
                role="button"
                tabIndex={0}
                onClick={() => {
                  vm.irParaData(dia);
                  vm.setViewMode('dia');
                }}
                onKeyDown={(e) => {
                  if (e.key === 'Enter' || e.key === ' ') {
                    vm.irParaData(dia);
                    vm.setViewMode('dia');
                  }
                }}
                className={cn(
                  'min-h-[112px] cursor-pointer border-b border-r border-border/60 p-1.5 text-left transition-colors last:border-r-0 hover:bg-secondary/50 active:brightness-95',
                  !noMesAtual && 'bg-surface-2/50',
                )}
              >
                <div className="mb-1 flex justify-center">
                  <span
                    className={cn(
                      'inline-flex h-7 w-7 items-center justify-center rounded-full text-xs font-medium',
                      ehHoje && 'bg-primary-600 font-bold text-white',
                      !ehHoje && noMesAtual && 'text-foreground',
                      !ehHoje && !noMesAtual && 'text-muted-foreground/50',
                    )}
                  >
                    {dia.getDate()}
                  </span>
                </div>

                <div className="space-y-0.5">
                  {agendamentosDoDia.slice(0, MAX_PREVIEW).map((a) => {
                    const tone = STATUS_TONE_CLASSES[a.status];
                    return (
                      <div
                        key={a.id}
                        onClick={(e) => {
                          e.stopPropagation();
                          vm.abrirVer(a);
                        }}
                        className={cn(
                          // `rounded-r` (não `rounded`): mesmo cuidado do AppointmentBlock — evita
                          // o artefato de "meia-lua" do border-radius somado ao border-left.
                          'truncate rounded-r border-l-2 px-1.5 py-0.5 text-[10px] transition-shadow hover:shadow-sm',
                          tone.chip,
                          tone.borda,
                        )}
                      >
                        <span className="font-semibold">{a.horaInicio}</span> {a.clienteNome.split(' ')[0]}
                      </div>
                    );
                  })}
                  {overflow > 0 && <div className="py-0.5 text-center text-[10px] font-medium text-muted-foreground">+{overflow} mais</div>}
                </div>
              </div>
            );
          })}
        </div>
      </div>
    </motion.div>
  );
}
