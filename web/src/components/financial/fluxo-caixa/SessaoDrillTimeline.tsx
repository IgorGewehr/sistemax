import { useMemo } from 'react';

import { cn } from '@/lib/utils';

import { diferencaCentavos, esperadoCentavos } from './calc';
import { formatCentavosWhole } from './MoneyWhole';
import type { SessaoCaixa } from './types';

interface SessaoDrillTimelineProps {
  sessao: SessaoCaixa;
}

type DotTone = 'neutral' | 'crit' | 'warn' | 'pos';

interface TimelineItemData {
  time: string;
  dot: DotTone;
  label: string;
  sub: string;
  amount: string;
}

const DOT_CLASS: Record<DotTone, string> = {
  neutral: 'bg-faint',
  crit: 'bg-crit',
  warn: 'bg-warn',
  pos: 'bg-pos',
};

/** A sessão completa do dia contada como uma história: abertura → suprimento(s) → sangria(s) →
 * troco → fechamento (ou "ainda em aberto", se for hoje). */
export function SessaoDrillTimeline({ sessao }: SessaoDrillTimelineProps) {
  const itens = useMemo<TimelineItemData[]>(() => {
    const lista: TimelineItemData[] = [
      {
        time: sessao.horaAbertura,
        dot: 'neutral',
        label: 'Abertura de caixa',
        sub: `Operador: ${sessao.operador}`,
        amount: `+ ${formatCentavosWhole(sessao.aberturaCentavos)}`,
      },
    ];

    sessao.suprimentos.forEach((suprimento) => {
      lista.push({
        time: suprimento.hora,
        dot: 'pos',
        label: 'Suprimento',
        sub: `Reforço vindo de ${suprimento.origem}`,
        amount: `+ ${formatCentavosWhole(suprimento.valorCentavos)}`,
      });
    });

    sessao.sangrias.forEach((sangria) => {
      lista.push({
        time: sangria.hora,
        dot: 'crit',
        label: 'Sangria',
        sub: `Retirada para ${sangria.destino}`,
        amount: `− ${formatCentavosWhole(sangria.valorCentavos)}`,
      });
    });

    lista.push({
      time: '—',
      dot: 'warn',
      label: 'Troco fornecido no turno',
      sub: 'Soma dos trocos dados aos clientes',
      amount: `− ${formatCentavosWhole(sessao.trocoCentavos)}`,
    });

    if (sessao.status === 'fechado') {
      const diff = diferencaCentavos(sessao) ?? 0;
      lista.push({
        time: sessao.horaFechamento,
        dot: diff === 0 ? 'neutral' : diff > 0 ? 'pos' : 'crit',
        label: 'Fechamento',
        sub: `Esperado ${formatCentavosWhole(esperadoCentavos(sessao))} · Contado ${formatCentavosWhole(sessao.contadoCentavos)}`,
        amount: diff === 0 ? 'Bateu certinho' : diff > 0 ? `+ ${formatCentavosWhole(diff)} sobra` : `− ${formatCentavosWhole(Math.abs(diff))} falta`,
      });
    }

    return lista;
  }, [sessao]);

  return (
    <div className="px-[18px] pb-[18px] pt-2.5">
      {itens.map((item, i) => (
        <div key={`${item.label}-${item.time}-${i}`} className="relative grid grid-cols-[44px_18px_1fr_auto] gap-x-2.5 py-2.5 last:pb-1">
          {i < itens.length - 1 && <div className="absolute -bottom-0.5 left-[52px] top-6 w-0.5 bg-border" />}
          <div className="pt-0.5 font-mono text-[11px] font-semibold text-muted-foreground">{item.time}</div>
          <div className={cn('mt-[3px] h-3 w-3 flex-none rounded-full', DOT_CLASS[item.dot])} />
          <div className="min-w-0">
            <div className="text-[13px] font-semibold">{item.label}</div>
            <div className="mt-px text-xs text-muted-foreground">{item.sub}</div>
          </div>
          <div className="whitespace-nowrap text-right text-[13.5px] font-bold">{item.amount}</div>
        </div>
      ))}
      {sessao.status === 'aberto' && (
        <div className="text-[12.5px] italic text-muted-foreground">
          Sessão ainda em aberto — o fechamento aparece aqui assim que a gaveta for contada.
        </div>
      )}
    </div>
  );
}
