import { AnimatePresence, motion } from 'framer-motion';
import { Calendar, ChevronDown, ChevronLeft, ChevronRight } from 'lucide-react';
import { useEffect, useMemo, useRef, useState } from 'react';

import { cn } from '@/lib/utils';

import { addMeses, buildMonthGrid, isHojeReal, isMesmoMes, startOfMonth, toISODate } from './calc';
import type { AgendaVm } from './useAgenda';

const MES_ANO_FORMATTER = new Intl.DateTimeFormat('pt-BR', { month: 'long', year: 'numeric' });
const DIAS_CURTOS = ['D', 'S', 'T', 'Q', 'Q', 'S', 'S'];

interface MiniCalendarPopoverProps {
  vm: AgendaVm;
}

/**
 * Trigger (label do período) + mini-mês em popover — substitui o `Popover` do MUI (ausente do
 * SistemaX) por um `<div>` posicionado + `AnimatePresence`, fechando em click-outside. Porte do
 * `MiniCalendar` do saas-erp (L314-396), fundido com o botão-âncora que no source vivia solto na
 * HEADER BAR (lá dependia do `Popover` do MUI pra separar âncora de conteúdo).
 */
export function MiniCalendarPopover({ vm }: MiniCalendarPopoverProps) {
  const [open, setOpen] = useState(false);
  const [mesExibido, setMesExibido] = useState(() => startOfMonth(vm.currentDate));
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) return;
    setMesExibido(startOfMonth(vm.currentDate));
    function aoClicarFora(e: MouseEvent) {
      if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false);
    }
    document.addEventListener('mousedown', aoClicarFora);
    return () => document.removeEventListener('mousedown', aoClicarFora);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open]);

  const datasComAgendamento = useMemo(() => new Set(vm.agendamentos.map((a) => a.data)), [vm.agendamentos]);
  const dias = useMemo(() => buildMonthGrid(mesExibido), [mesExibido]);
  const dataSelecionadaISO = toISODate(vm.currentDate);

  return (
    <div ref={ref} className="relative">
      <button
        type="button"
        onClick={() => setOpen((v) => !v)}
        className="flex items-center gap-2 rounded-xl px-3 py-2 text-sm font-semibold text-foreground transition-colors hover:bg-secondary active:brightness-95 sm:text-base"
      >
        <Calendar className="h-4 w-4 text-muted-foreground" />
        <span className="capitalize">{vm.periodoLabel}</span>
        <ChevronDown className="h-3.5 w-3.5 text-muted-foreground" />
      </button>

      <AnimatePresence>
        {open && (
          <motion.div
            initial={{ opacity: 0, y: -6, scale: 0.98 }}
            animate={{ opacity: 1, y: 0, scale: 1 }}
            exit={{ opacity: 0, y: -4, scale: 0.98 }}
            transition={{ duration: 0.15 }}
            className="surface absolute left-0 top-full z-40 mt-2 w-[280px] rounded-2xl p-3 shadow-lg"
          >
            <div className="mb-3 flex items-center justify-between">
              <button
                type="button"
                onClick={() => setMesExibido((m) => addMeses(m, -1))}
                className="rounded-md p-1 text-muted-foreground transition-colors hover:bg-secondary hover:text-foreground"
              >
                <ChevronLeft className="h-4 w-4" />
              </button>
              <span className="text-sm font-semibold capitalize text-foreground">{MES_ANO_FORMATTER.format(mesExibido)}</span>
              <button
                type="button"
                onClick={() => setMesExibido((m) => addMeses(m, 1))}
                className="rounded-md p-1 text-muted-foreground transition-colors hover:bg-secondary hover:text-foreground"
              >
                <ChevronRight className="h-4 w-4" />
              </button>
            </div>

            <div className="mb-1 grid grid-cols-7">
              {DIAS_CURTOS.map((d, i) => (
                <div key={i} className="py-1 text-center text-[11px] font-medium text-muted-foreground">
                  {d}
                </div>
              ))}
            </div>

            <div className="grid grid-cols-7">
              {dias.map((dia) => {
                const diaISO = toISODate(dia);
                const noMes = isMesmoMes(dia, mesExibido);
                const selecionado = diaISO === dataSelecionadaISO;
                const hoje = isHojeReal(dia);
                const temAgendamento = datasComAgendamento.has(diaISO);
                return (
                  <button
                    key={diaISO}
                    type="button"
                    onClick={() => {
                      vm.irParaData(dia);
                      setOpen(false);
                    }}
                    className={cn(
                      'relative flex h-9 w-9 items-center justify-center rounded-lg text-[13px] transition-colors active:brightness-95',
                      !noMes && 'text-muted-foreground/40',
                      noMes && !selecionado && !hoje && 'text-foreground hover:bg-secondary',
                      hoje && !selecionado && 'font-bold text-primary-600',
                      selecionado && 'bg-primary-600 font-semibold text-white shadow-sm',
                    )}
                  >
                    {dia.getDate()}
                    {temAgendamento && !selecionado && (
                      <span
                        className={cn(
                          'absolute bottom-1 left-1/2 h-1 w-1 -translate-x-1/2 rounded-full',
                          hoje ? 'bg-primary-600' : 'bg-muted-foreground',
                        )}
                      />
                    )}
                  </button>
                );
              })}
            </div>
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  );
}
