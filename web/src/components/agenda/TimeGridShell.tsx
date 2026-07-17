import { useEffect, useState, type ReactNode } from 'react';

import { cn } from '@/lib/utils';

import { END_HOUR, HOUR_HEIGHT, START_HOUR } from './calc';

const TOTAL_HORAS = END_HOUR - START_HOUR;

function offsetAgora(): number {
  const agora = new Date();
  const minutos = agora.getHours() * 60 + agora.getMinutes();
  return ((minutos - START_HOUR * 60) / 60) * HOUR_HEIGHT;
}

/**
 * Coluna de rótulos de hora (06:00…22:00) — compartilhada por Dia e Semana. Renderizada UMA vez
 * por view (ao lado das colunas de dia), nunca repetida por coluna.
 */
export function TimeColumn() {
  return (
    <div className="relative w-16 flex-shrink-0 border-r border-border/60" style={{ height: `${TOTAL_HORAS * HOUR_HEIGHT}px` }}>
      {Array.from({ length: TOTAL_HORAS + 1 }, (_, i) => {
        const hora = START_HOUR + i;
        return (
          <div key={hora} className="absolute right-0 flex w-full items-center justify-end pr-2" style={{ top: `${i * HOUR_HEIGHT - 6}px` }}>
            <span className="num text-[11px] font-medium text-muted-foreground">{String(hora).padStart(2, '0')}:00</span>
          </div>
        );
      })}
    </div>
  );
}

interface GridColumnProps {
  /** Este dia é o dia REAL do dispositivo — controla a linha do "agora" (independente do
   *  ANCHOR_HOJE fixo da demo; ver `isHojeReal` em calc.ts). */
  ehHojeReal?: boolean;
  className?: string;
  children: ReactNode;
}

/**
 * Uma coluna de grade: linhas horizontais (cheia + meia-hora) e, se `ehHojeReal`, a linha
 * pulsante do "agora" atualizada a cada minuto. `children` são os slots clicáveis + blocos de
 * agendamento, posicionados absolutamente por cima. Usado 1x no Dia e 7x (uma por dia) na Semana
 * — por isso é "compartilhado dia/semana" (TimeColumn ao lado é que não se repete).
 */
export function GridColumn({ ehHojeReal = false, className, children }: GridColumnProps) {
  const [offset, setOffset] = useState(offsetAgora);

  useEffect(() => {
    if (!ehHojeReal) return;
    const id = window.setInterval(() => setOffset(offsetAgora()), 60_000);
    return () => window.clearInterval(id);
  }, [ehHojeReal]);

  const agora = new Date();
  const dentroDaJanela = ehHojeReal && agora.getHours() >= START_HOUR && agora.getHours() < END_HOUR;

  return (
    <div className={cn('relative flex-1', className)}>
      {Array.from({ length: TOTAL_HORAS + 1 }, (_, i) => (
        <div key={`linha-${i}`} className="absolute inset-x-0 border-t border-border/60" style={{ top: `${i * HOUR_HEIGHT}px` }} />
      ))}
      {Array.from({ length: TOTAL_HORAS }, (_, i) => (
        <div key={`meia-${i}`} className="absolute inset-x-0 border-t border-dashed border-border/40" style={{ top: `${i * HOUR_HEIGHT + HOUR_HEIGHT / 2}px` }} />
      ))}
      {dentroDaJanela && (
        <div className="pointer-events-none absolute inset-x-0 z-30 flex items-center" style={{ top: `${offset}px` }}>
          <div className="-ml-1 h-2.5 w-2.5 rounded-full bg-primary-600 shadow-sm" />
          <div className="h-[2px] flex-1 bg-primary-600" />
        </div>
      )}
      {children}
    </div>
  );
}
