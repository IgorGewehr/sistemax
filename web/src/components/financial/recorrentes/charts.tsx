/**
 * Gráficos SVG da tela Recorrentes — geometria replicada 1:1 do mockup
 * (`sparklineFixas`, `svgColunas`, `svgDivergente` em `recorrentes.html`), porém
 * como componentes React tipados (nada de `dangerouslySetInnerHTML`/string concat).
 * Cores vêm dos tokens do design system (`hsl(var(--token))`), nunca hex cru.
 */
import { useId } from 'react';

import type { Centavos } from '@/lib/money';

import { formatCentavosWhole, MESES_ASSINATURAS, MESES_FIXAS } from './calc';

// ───────────────────────── Sparkline (KPI hero) ─────────────────────────

interface SparklineProps {
  values: Centavos[];
  className?: string;
}

/** Área + linha com ponto final — mesmo desenho dos KPIs hero "Custo de existir" / "MRR". */
export function Sparkline({ values, className }: SparklineProps) {
  const gradientId = useId();
  const w = 260;
  const h = 34;
  const min = Math.min(...values);
  const max = Math.max(...values);
  const range = max - min || 1;
  const pts = values.map((v, i) => {
    const x = (i / (values.length - 1)) * w;
    const y = h - ((v - min) / range) * (h - 6) - 3;
    return [x, y] as const;
  });
  const path = pts.map(([x, y], i) => `${i === 0 ? 'M' : 'L'}${x.toFixed(1)},${y.toFixed(1)}`).join(' ');
  const area = `${path} L${w},${h} L0,${h} Z`;
  const [lastX, lastY] = pts[pts.length - 1];

  return (
    <svg
      className={className ?? 'mt-2.5 block h-[34px] w-full'}
      viewBox={`0 0 ${w} ${h}`}
      preserveAspectRatio="none"
      aria-hidden="true"
    >
      <defs>
        <linearGradient id={gradientId} x1="0" y1="0" x2="0" y2="1">
          <stop offset="0" stopColor="hsl(var(--primary))" stopOpacity="0.28" />
          <stop offset="1" stopColor="hsl(var(--primary))" stopOpacity="0" />
        </linearGradient>
      </defs>
      <path d={path} fill="none" stroke="hsl(var(--primary))" strokeWidth="2" strokeLinecap="round" />
      <path d={area} fill={`url(#${gradientId})`} />
      <circle cx={lastX} cy={lastY} r="3" fill="hsl(var(--primary))" />
    </svg>
  );
}

// ───────────────────────── Coluna de 12 meses + média (drill de conta fixa) ─────────────────────────

interface ColumnHistoryChartProps {
  nome: string;
  historico12m: Centavos[];
  media6m: Centavos;
  variacaoPct: number;
  emAlerta: boolean;
}

/** 12 colunas (Ago→Jul) + linha tracejada da média de 6 meses — mockup: `svgColunas`. */
export function ColumnHistoryChart({ nome, historico12m, media6m, variacaoPct, emAlerta }: ColumnHistoryChartProps) {
  const w = 400;
  const h = 158;
  const x0 = 20;
  const x1 = 388;
  const baseY = 122;
  const topPad = 16;
  const maxV = Math.max(...historico12m, media6m) * 1.1 || 1;
  const slot = (x1 - x0) / 12;
  const bw = Math.min(15, slot * 0.55);
  const scaleY = (v: number) => (baseY - topPad) * (v / maxV);
  const avgY = baseY - scaleY(media6m);

  return (
    <svg viewBox={`0 0 ${w} ${h}`} role="img" aria-label={`Histórico de valor — ${nome}`} className="block w-full">
      <line x1={x0} y1={baseY} x2={x1} y2={baseY} stroke="hsl(var(--border))" strokeWidth={1} />
      <line x1={x0} y1={avgY} x2={x1} y2={avgY} stroke="hsl(var(--muted-foreground))" strokeWidth={1.4} strokeDasharray="4 3" />
      <text x={x0} y={avgY - 6} fill="hsl(var(--muted-foreground))" fontSize={9.5} fontWeight={700}>
        média 6m · {formatCentavosWhole(media6m)}
      </text>
      {historico12m.map((v, i) => {
        const hgt = scaleY(v);
        const cx = x0 + slot * i + slot / 2;
        const isCur = i === 11;
        const flagged = isCur && emAlerta;
        const fill = flagged ? 'hsl(var(--crit))' : isCur ? 'hsl(var(--foreground) / 0.55)' : 'hsl(var(--foreground) / 0.22)';
        return (
          <g key={MESES_FIXAS[i]}>
            <rect x={cx - bw / 2} y={baseY - hgt} width={bw} height={hgt} rx={3} fill={fill}>
              <title>
                {MESES_FIXAS[i]} · {formatCentavosWhole(v)}
              </title>
            </rect>
            <text x={cx} y={baseY + 14} textAnchor="middle" fill="hsl(var(--muted-foreground))" fontSize={9}>
              {MESES_FIXAS[i]}
            </text>
            {flagged && (
              <text
                x={cx}
                y={baseY - hgt - 7}
                textAnchor="middle"
                fill="hsl(var(--crit))"
                fontSize={10}
                fontWeight={700}
                fontFamily="'JetBrains Mono', ui-monospace, monospace"
              >
                +{Math.round(variacaoPct)}%
              </text>
            )}
          </g>
        );
      })}
    </svg>
  );
}

// ───────────────────────── Novos × Churn, 6 meses (drill de serviço de assinatura) ─────────────────────────

interface DivergentFlowChartProps {
  novos6m: Centavos[];
  churn6m: Centavos[];
}

/** Barras divergentes acima/abaixo da linha zero — mockup: `svgDivergente`. */
export function DivergentFlowChart({ novos6m, churn6m }: DivergentFlowChartProps) {
  const zeroY = 58;
  const x0 = 16;
  const x1 = 330;
  const bw = 18;
  const max = Math.max(1, ...novos6m, ...churn6m);
  const f = 44 / max;
  const slot = (x1 - x0) / 6;

  return (
    <svg viewBox="0 0 340 145" role="img" aria-label="Novos e churn por mês" className="block w-full">
      <line x1={x0} y1={zeroY} x2={x1} y2={zeroY} stroke="hsl(var(--border))" strokeWidth={1} />
      {novos6m.map((v, i) => {
        if (v <= 0) return null;
        const hgt = v * f;
        const cx = x0 + slot * i + slot / 2;
        return (
          <rect key={`novo-${MESES_ASSINATURAS[i]}`} x={cx - bw / 2} y={zeroY - hgt} width={bw} height={hgt} rx={3} fill="hsl(var(--pos))">
            <title>
              {MESES_ASSINATURAS[i]} · novos {formatCentavosWhole(v)}
            </title>
          </rect>
        );
      })}
      {churn6m.map((v, i) => {
        if (v <= 0) return null;
        const hgt = v * f;
        const cx = x0 + slot * i + slot / 2;
        return (
          <rect key={`churn-${MESES_ASSINATURAS[i]}`} x={cx - bw / 2} y={zeroY} width={bw} height={hgt} rx={3} fill="hsl(var(--crit))">
            <title>
              {MESES_ASSINATURAS[i]} · churn {formatCentavosWhole(v)}
            </title>
          </rect>
        );
      })}
      {MESES_ASSINATURAS.map((m, i) => {
        const cx = x0 + slot * i + slot / 2;
        return (
          <text key={m} x={cx} y={136} textAnchor="middle" fill="hsl(var(--muted-foreground))" fontSize={9}>
            {m}
          </text>
        );
      })}
    </svg>
  );
}
