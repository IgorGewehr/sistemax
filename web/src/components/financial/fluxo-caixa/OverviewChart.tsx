import { motion } from 'framer-motion';
import { useMemo, type ReactElement } from 'react';

import { cn } from '@/lib/utils';

import { diaLabel, diferencaCentavos, DIA_SEMANA_COMPLETO } from './calc';
import { formatCentavosWhole } from './MoneyWhole';
import type { DiaSemanaAbrev, SessaoCaixa } from './types';

interface OverviewChartProps {
  dias: SessaoCaixa[];
  /** Fim-de-semana em destaque pulsante — acionado pelo "Ver as quintas →" do Super Consultor. */
  pulsingWeekday: DiaSemanaAbrev | null;
  onSelectDay: (dia: number) => void;
}

const VIEWBOX_W = 624;
const VIEWBOX_H = 184;
const X0 = 18;
const X1 = 606;
const ZERO_Y = 88;
const BAR_WIDTH = 20;
const MAX_BAR_HEIGHT = 46;
/** Piso de 4 reais (em centavos) — evita barras minúsculas/ilegíveis num mês "limpo". */
const PISO_ESCALA_CENTAVOS = 400;

/** Gráfico divergente: sobra sobe, falta desce, "bateu certinho" vira uma tacinha na linha zero,
 * sessão em aberto vira um quadrado tracejado — clique num dia leva pro drill (`AnaliseInterativa`). */
export function OverviewChart({ dias, pulsingWeekday, onSelectDay }: OverviewChartProps) {
  const ordenados = useMemo(() => [...dias].sort((a, b) => a.dia - b.dia), [dias]);
  const slot = (X1 - X0) / ordenados.length;
  const maxAbs = Math.max(PISO_ESCALA_CENTAVOS, ...ordenados.map((d) => Math.abs(diferencaCentavos(d) ?? 0)));
  const fator = MAX_BAR_HEIGHT / maxAbs;

  return (
    <svg viewBox={`0 0 ${VIEWBOX_W} ${VIEWBOX_H}`} role="img" aria-label="Diferenças de caixa por dia" className="block h-auto w-full overflow-visible">
      <line x1={X0} y1={ZERO_Y} x2={X1} y2={ZERO_Y} strokeWidth={1} className="stroke-current text-border" />
      {ordenados.map((dia, i) => {
        const cx = X0 + slot * i + slot / 2;
        return (
          <DayBar
            key={dia.dia}
            sessao={dia}
            cx={cx}
            slot={slot}
            fator={fator}
            isPulsing={pulsingWeekday !== null && dia.diaSemana === pulsingWeekday}
            onSelect={onSelectDay}
          />
        );
      })}
    </svg>
  );
}

interface DayBarProps {
  sessao: SessaoCaixa;
  cx: number;
  slot: number;
  fator: number;
  isPulsing: boolean;
  onSelect: (dia: number) => void;
}

function DayBar({ sessao, cx, slot, fator, isPulsing, onSelect }: DayBarProps) {
  const diaSemanaCompleta = DIA_SEMANA_COMPLETO[sessao.diaSemana];
  const diff = diferencaCentavos(sessao);
  const hitX = cx - slot / 2 + 1;
  const hitW = Math.max(slot - 2, 0);

  let descricao: string;
  let barra: ReactElement;

  if (sessao.status === 'aberto') {
    descricao = 'sessão em aberto';
    barra = (
      <rect
        x={cx - 8}
        y={ZERO_Y - 8}
        width={16}
        height={16}
        rx={4}
        className="fill-none stroke-current text-faint transition-colors [stroke-dasharray:3_3] [stroke-width:1.4px] group-hover:text-primary-600"
      />
    );
  } else if (diff === 0) {
    descricao = 'bateu certinho';
    barra = (
      <rect
        x={cx - BAR_WIDTH / 2}
        y={ZERO_Y - 2}
        width={BAR_WIDTH}
        height={4}
        rx={2}
        className="fill-current text-faint transition-opacity group-hover:opacity-80"
      />
    );
  } else {
    // Aberto e "bateu certinho" já cobertos acima — aqui `diff` é garantidamente não-nulo e ≠ 0.
    const diffValor = diff ?? 0;
    const altura = Math.abs(diffValor) * fator;
    descricao = diffValor > 0 ? `sobra ${formatCentavosWhole(diffValor)}` : `falta ${formatCentavosWhole(Math.abs(diffValor))}`;
    barra = (
      <rect
        x={cx - BAR_WIDTH / 2}
        y={diffValor > 0 ? ZERO_Y - altura : ZERO_Y}
        width={BAR_WIDTH}
        height={altura}
        rx={3}
        className={cn('transition-opacity group-hover:opacity-80', diffValor > 0 ? 'fill-current text-pos' : 'fill-current text-crit')}
      />
    );
  }

  return (
    <motion.g
      className="group cursor-pointer"
      onClick={() => onSelect(sessao.dia)}
      animate={isPulsing ? { opacity: [1, 0.3, 1, 0.3, 1, 0.3, 1] } : { opacity: 1 }}
      transition={isPulsing ? { duration: 1.8, ease: 'easeInOut' } : { duration: 0 }}
    >
      <title>{`${diaLabel(sessao.dia)} · ${diaSemanaCompleta} · ${descricao}`}</title>
      <rect x={hitX} y={14} width={hitW} height={150} className="fill-transparent transition-colors group-hover:fill-surface-2" />
      {barra}
      <text x={cx} y={176} textAnchor="middle" className="fill-current text-[9.5px] text-muted-foreground">
        {sessao.dia}
      </text>
    </motion.g>
  );
}
