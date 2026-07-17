import { AnimatePresence, motion } from 'framer-motion';
import { useEffect, useMemo, useRef, useState } from 'react';

import { SectionCard } from '@/components/shared';
import { cn } from '@/lib/utils';

import { formatCentavosWhole, formatSignedCentavosWhole } from './money';
import { computeTimelineGeometry } from './timelineGeometry';
import type { TimelineViewModel } from './types';

interface CashTimelineSectionProps {
  vm: TimelineViewModel;
}

/** "DD/MM" de um ponto — usa a data real (`datasISO`) quando ela vem da API; o mock não a
 * preenche, então cai no fallback índice+1 = dia do mês (só válido dentro de um único mês). */
function dayLabel(index: number, mesLabel: string, datasISO?: string[]): string {
  const iso = datasISO?.[index];
  if (iso) {
    const [, mes, dia] = iso.split('-');
    return `${dia}/${mes}`;
  }
  return `${String(index + 1).padStart(2, '0')}/${mesLabel}`;
}

/**
 * "O caixa nos próximos 30 dias" (bloco ②, único gráfico da tela) — realizado sólido até hoje,
 * previsto tracejado depois, e um único marcador onde o saldo projetado cruza zero. SVG à mão (em
 * vez do `recharts` já usado no app) porque a fidelidade ao mockup pede um traço misto
 * sólido/tracejado com um trecho realçado e um balão de anotação no ponto exato do cruzamento —
 * a geometria vem pronta e testável de `timelineGeometry.ts`.
 */
export function CashTimelineSection({ vm }: CashTimelineSectionProps) {
  const { valoresDiarios, hojeIndex, eventosPorDia, mesLabel, datasISO } = vm;
  const geometry = useMemo(() => computeTimelineGeometry(valoresDiarios, hojeIndex), [valoresDiarios, hojeIndex]);
  const [activeIndex, setActiveIndex] = useState<number | null>(null);
  const containerRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    function onDocClick(e: MouseEvent) {
      if (containerRef.current && !containerRef.current.contains(e.target as Node)) {
        setActiveIndex(null);
      }
    }
    document.addEventListener('click', onDocClick);
    return () => document.removeEventListener('click', onDocClick);
  }, []);

  const n = valoresDiarios.length;
  const activePoint = activeIndex !== null ? geometry.points[activeIndex] : null;
  const activeEvento = activeIndex !== null ? eventosPorDia[activeIndex + 1] : undefined;

  const crossMarker = useMemo(() => {
    if (geometry.crossIndex === null || geometry.crossX === null) return null;
    const boxW = 196;
    const boxH = 36;
    const my = geometry.points[geometry.crossIndex].y;
    const bx = Math.max(16, Math.min(geometry.crossX - boxW / 2, geometry.width - 16 - boxW));
    return {
      mx: geometry.crossX,
      my,
      bx,
      by: 4,
      boxW,
      boxH,
      label: dayLabel(geometry.crossIndex, mesLabel, datasISO),
      valorCentavos: valoresDiarios[geometry.crossIndex],
    };
  }, [geometry, mesLabel, valoresDiarios, datasISO]);

  const axisTicks = [
    { i: 0, label: dayLabel(0, mesLabel, datasISO), anchor: 'start' as const },
    { i: hojeIndex, label: 'hoje', anchor: 'middle' as const },
    { i: n - 1, label: dayLabel(n - 1, mesLabel, datasISO), anchor: 'end' as const },
  ];

  return (
    <SectionCard title="O caixa nos próximos 30 dias" hint="clique num dia → o que entra/sai nele" bodyClassName="mt-0">
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

      <div ref={containerRef} className="relative mx-2 mb-2.5 mt-1">
        <svg
          viewBox={`0 0 ${geometry.width} ${geometry.height}`}
          role="img"
          aria-label="Projeção do caixa para os próximos 30 dias, com realizado e previsto"
          className="block w-full"
        >
          <rect
            x={16}
            y={geometry.zeroY}
            width={geometry.width - 32}
            height={geometry.height - geometry.padBottom - geometry.zeroY}
            className="fill-crit/[0.035]"
          />
          <line
            x1={16}
            y1={geometry.zeroY}
            x2={geometry.width - 16}
            y2={geometry.zeroY}
            strokeWidth={1}
            strokeDasharray="2 3"
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

          <path d={geometry.solidPath} fill="none" strokeWidth={2.4} strokeLinecap="round" strokeLinejoin="round" className="stroke-foreground" />
          <path
            d={geometry.dashedPath}
            fill="none"
            strokeWidth={2.4}
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

          {crossMarker && (
            <g>
              <line
                x1={crossMarker.mx}
                y1={crossMarker.by + crossMarker.boxH}
                x2={crossMarker.mx}
                y2={crossMarker.my - 9}
                strokeWidth={1.3}
                strokeDasharray="2 3"
                className="stroke-crit opacity-[0.55]"
              />
              <rect
                x={crossMarker.bx}
                y={crossMarker.by}
                width={crossMarker.boxW}
                height={crossMarker.boxH}
                rx={9}
                className="fill-crit-soft stroke-crit/[0.35]"
              />
              <text x={crossMarker.bx + 11} y={crossMarker.by + 15} fontSize={10.5} fontWeight={700} className="fill-crit">
                {crossMarker.label} · aqui fica negativo
              </text>
              <text x={crossMarker.bx + 11} y={crossMarker.by + 29} fontSize={11.5} fontWeight={700} className="fill-crit num">
                {formatSignedCentavosWhole(crossMarker.valorCentavos)} projetado
              </text>
              <circle cx={crossMarker.mx} cy={crossMarker.my} r={5} className="fill-crit stroke-card" strokeWidth={2.2} />
            </g>
          )}

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
          {activePoint && activeIndex !== null && (
            <motion.div
              key={activeIndex}
              initial={{ opacity: 0 }}
              animate={{ opacity: 1 }}
              exit={{ opacity: 0 }}
              transition={{ duration: 0.15 }}
              style={{
                left: `${(activePoint.x / geometry.width) * 100}%`,
                top: `${(activePoint.y / geometry.height) * 100}%`,
                transform: 'translate(-50%, -122%)',
              }}
              className="pointer-events-none absolute z-10 min-w-[178px] rounded-[10px] bg-foreground px-[11px] py-[9px] text-[11.5px] leading-[1.55] text-background shadow-xl"
            >
              <div className="mb-[3px] font-bold">
                {dayLabel(activeIndex, mesLabel, datasISO)}
                {activeIndex === hojeIndex ? ' · hoje' : ''}
              </div>
              <div className="num opacity-90">
                Saldo projetado: <b>{formatCentavosWhole(valoresDiarios[activeIndex])}</b>
              </div>
              {activeEvento ? (
                <div className={cn('num mt-1 font-semibold', activeEvento.tone === 'pos' ? 'text-pos' : 'text-crit')}>
                  {formatSignedCentavosWhole(activeEvento.deltaCentavos)} · {activeEvento.descricao}
                </div>
              ) : (
                <div className="mt-1 font-normal text-faint">Sem vencimento grande — variação do dia a dia.</div>
              )}
            </motion.div>
          )}
        </AnimatePresence>
      </div>
    </SectionCard>
  );
}
