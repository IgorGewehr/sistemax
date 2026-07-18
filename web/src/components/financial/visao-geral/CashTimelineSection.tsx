import { AnimatePresence, motion } from 'framer-motion';
import { useId, useMemo, useState } from 'react';

import { SectionCard } from '@/components/shared';
import { formatCentavosWhole } from '@/lib/money';

import { computeTimelineGeometry } from './timelineGeometry';
import type { TimelineViewModel } from './types';

interface CashTimelineSectionProps {
  vm: TimelineViewModel;
}

/** ISO "2026-07-18" → "18/07". */
function dayLabel(iso: string): string {
  const [, mes, dia] = iso.split('-');
  return dia && mes ? `${dia}/${mes}` : iso;
}

/**
 * "Caixa · próximos 30 dias" (bloco ①b, único gráfico da tela v3) — área lavada sob a curva
 * inteira (realizado sólido até hoje, previsto tracejado depois) + o dia mais apertado destacado.
 * 1:1 com `docs/ui/mockups/visao-geral-v3.html`. SVG à mão (não `recharts`): a fidelidade ao
 * mockup pede um traço misto sólido/tracejado com wash de gradiente e um marcador de mínimo exato
 * — a geometria vem pronta e testável de `timelineGeometry.ts`.
 */
export function CashTimelineSection({ vm }: CashTimelineSectionProps) {
  const { pontos, hojeIndex, menorIndex } = vm;
  const valoresDiarios = useMemo(() => pontos.map((p) => p.saldoCentavos), [pontos]);
  const geometry = useMemo(() => computeTimelineGeometry(valoresDiarios, hojeIndex), [valoresDiarios, hojeIndex]);
  const [activeIndex, setActiveIndex] = useState<number | null>(null);
  const gradId = useId();

  const n = pontos.length;
  if (n === 0) {
    return (
      <SectionCard title="Caixa · próximos 30 dias" bodyClassName="mt-0">
        <div className="px-[18px] pb-5 text-sm text-muted-foreground">Sem projeção de caixa disponível.</div>
      </SectionCard>
    );
  }

  const activePonto = activeIndex !== null ? pontos[activeIndex] : null;
  const menorPonto = pontos[menorIndex];
  const menorPoint = geometry.points[menorIndex];
  const ultimoPoint = geometry.points[n - 1];

  const axisTicks = [
    { i: 0, label: dayLabel(pontos[0].dataIso), anchor: 'start' as const },
    { i: hojeIndex, label: 'hoje', anchor: 'middle' as const },
    { i: n - 1, label: dayLabel(pontos[n - 1].dataIso), anchor: 'end' as const },
  ];

  return (
    <SectionCard title="Caixa · próximos 30 dias" hint="toque num dia" bodyClassName="mt-0">
      <div className="flex gap-4 px-[18px] pb-0.5 pt-1.5 text-xs text-muted-foreground">
        <span className="inline-flex items-center gap-1.5">
          <i className="inline-block h-[2.5px] w-4 rounded-full bg-foreground" />
          realizado
        </span>
        <span className="inline-flex items-center gap-1.5">
          <i className="inline-block h-0 w-4 border-t-[2.5px] border-dashed border-foreground/70" />
          previsto
        </span>
      </div>

      <div className="relative mx-2 mb-2.5 mt-1">
        <svg
          viewBox={`0 0 ${geometry.width} ${geometry.height}`}
          role="img"
          aria-label="Projeção do caixa para os próximos 30 dias, com realizado e previsto"
          className="block w-full"
        >
          <defs>
            <linearGradient id={gradId} x1="0" y1="0" x2="0" y2="1">
              <stop offset="0" style={{ stopColor: 'hsl(var(--pos))' }} stopOpacity={0.14} />
              <stop offset="1" style={{ stopColor: 'hsl(var(--pos))' }} stopOpacity={0} />
            </linearGradient>
          </defs>

          {/* Lavagem sob a curva inteira — "o rio nunca toca o fundo = tudo bem". */}
          <path d={geometry.areaPath} fill={`url(#${gradId})`} />

          <line
            x1={16}
            y1={geometry.zeroY}
            x2={geometry.width - 16}
            y2={geometry.zeroY}
            strokeWidth={1}
            className="stroke-border"
          />
          <text x={18} y={geometry.zeroY - 4} fontSize={9} className="fill-faint">
            0
          </text>

          {geometry.negativeAreaPath && <path d={geometry.negativeAreaPath} className="fill-crit/[0.15]" />}

          <line
            x1={geometry.points[hojeIndex].x}
            y1={geometry.padTop - 8}
            x2={geometry.points[hojeIndex].x}
            y2={geometry.height - geometry.padBottom}
            strokeWidth={1}
            strokeDasharray="2 3"
            className="stroke-muted-foreground opacity-[0.55]"
          />

          <path d={geometry.solidPath} fill="none" strokeWidth={2.3} strokeLinecap="round" strokeLinejoin="round" className="stroke-foreground" />
          <path
            d={geometry.dashedPath}
            fill="none"
            strokeWidth={2.3}
            strokeDasharray="1 5.5"
            strokeLinecap="round"
            strokeLinejoin="round"
            className="stroke-foreground opacity-80"
          />
          {geometry.negativeDashedPath && (
            <path
              d={geometry.negativeDashedPath}
              fill="none"
              strokeWidth={2.6}
              strokeDasharray="1 5.5"
              strokeLinecap="round"
              strokeLinejoin="round"
              className="stroke-crit"
            />
          )}

          {/* Único destaque fora do "hoje": o dia mais apertado da série. */}
          <circle cx={menorPoint.x} cy={menorPoint.y} r={4.5} className="fill-warn stroke-card" strokeWidth={2.2} />
          <text x={menorPoint.x} y={menorPoint.y + 20} fontSize={10} fontWeight={600} textAnchor="middle" className="fill-muted-foreground">
            menor ·{' '}
            <tspan className="num fill-foreground" fontWeight={700}>
              {formatCentavosWhole(menorPonto.saldoCentavos)}
            </tspan>
          </text>

          <circle cx={ultimoPoint.x} cy={ultimoPoint.y} r={3.5} className="fill-foreground stroke-card" strokeWidth={2} />
          <text x={ultimoPoint.x} y={ultimoPoint.y - 10} fontSize={10} fontWeight={700} textAnchor="end" className="num fill-muted-foreground">
            {formatCentavosWhole(pontos[n - 1].saldoCentavos)}
          </text>

          {axisTicks.map(({ i, label, anchor }) => (
            <text
              key={i}
              x={geometry.points[i].x}
              y={geometry.height - geometry.padBottom + 15}
              fontSize={10}
              textAnchor={anchor}
              className="fill-muted-foreground"
            >
              {label}
            </text>
          ))}

          {geometry.points.map((p) => (
            <rect
              key={p.index}
              x={p.x - geometry.slotWidth / 2}
              y={geometry.padTop - 8}
              width={geometry.slotWidth}
              height={geometry.height - geometry.padBottom - (geometry.padTop - 8)}
              fill="transparent"
              className="cursor-pointer"
              onClick={(e) => {
                e.stopPropagation();
                setActiveIndex(p.index);
              }}
            />
          ))}
        </svg>

        <AnimatePresence>
          {activePonto && activeIndex !== null && (
            <motion.div
              key={activeIndex}
              initial={{ opacity: 0 }}
              animate={{ opacity: 1 }}
              exit={{ opacity: 0 }}
              transition={{ duration: 0.15 }}
              style={{
                left: `${(geometry.points[activeIndex].x / geometry.width) * 100}%`,
                top: `${(geometry.points[activeIndex].y / geometry.height) * 100}%`,
                transform: 'translate(-50%, -122%)',
              }}
              className="pointer-events-none absolute z-10 min-w-[178px] rounded-[10px] bg-foreground px-[11px] py-[9px] text-[11.5px] leading-[1.55] text-background shadow-xl"
            >
              <div className="mb-[3px] font-bold">
                {dayLabel(activePonto.dataIso)}
                {activeIndex === hojeIndex ? ' · hoje' : ''}
              </div>
              <div className="num opacity-90">
                Saldo: <b>{formatCentavosWhole(activePonto.saldoCentavos)}</b>
              </div>
              <div className="mt-1 font-normal text-faint">Dia comum — só o movimento do balcão.</div>
            </motion.div>
          )}
        </AnimatePresence>
      </div>
    </SectionCard>
  );
}
