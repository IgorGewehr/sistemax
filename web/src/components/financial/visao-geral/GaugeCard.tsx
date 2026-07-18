import { Eyebrow, InfoTip, StatusChip, type ChipTone } from '@/components/shared';
import { Surface } from '@/components/ui/Surface';

import { MoneyValue } from './MoneyValue';
import type { DrillTarget, GaugeViewModel, ZonaFolego } from './types';

interface GaugeCardProps {
  vm: GaugeViewModel;
  onDrill: (target: DrillTarget) => void;
}

const W = 236;
const H = 136;
const CX = 118;
const CY = 120;
const R = 94;
const SW = 15;
const MAX = 60;
const GAPF = 2.4 / 180;

const ZONES: { a: number; b: number; tone: ZonaFolego }[] = [
  { a: 0, b: 15, tone: 'crit' },
  { a: 15, b: 30, tone: 'warn' },
  { a: 30, b: MAX, tone: 'pos' },
];

const ZONE_STROKE: Record<ZonaFolego, string> = { pos: 'stroke-pos', warn: 'stroke-warn', crit: 'stroke-crit' };
const ZONE_CHIP: Record<ZonaFolego, ChipTone> = { pos: 'sobra', warn: 'aberto', crit: 'falta' };

function pontoNoArco(fracao: number, raio: number): [number, number] {
  const angulo = Math.PI * (1 - fracao);
  return [CX + raio * Math.cos(angulo), CY - raio * Math.sin(angulo)];
}

function arcoPath(f0: number, f1: number, raio: number): string {
  const [x0, y0] = pontoNoArco(f0, raio);
  const [x1, y1] = pontoNoArco(f1, raio);
  const largo = f1 - f0 > 0.5 ? 1 : 0;
  return `M ${x0.toFixed(1)},${y0.toFixed(1)} A ${raio},${raio} 0 ${largo} 1 ${x1.toFixed(1)},${y1.toFixed(1)}`;
}

/**
 * Bloco ① (dominante) — medidor de fôlego de caixa em arco (zonas verde/âmbar/vermelho), veredito
 * e os 2 chips "Em caixa"/"Pode tirar". 1:1 com `docs/ui/mockups/visao-geral-v3.html` — o SVG do
 * arco é o mesmo cálculo do `<script>` do mockup, portado pra React (path por zona + marcador no
 * valor real).
 */
export function GaugeCard({ vm, onDrill }: GaugeCardProps) {
  const valorClamped = vm.diasFolego === null ? 0 : Math.min(Math.max(vm.diasFolego, 0), MAX);
  const [mx, my] = pontoNoArco(valorClamped / MAX, R);

  return (
    <Surface padding="none" className="flex h-full flex-col p-[18px] pb-4">
      <div className="flex items-center justify-between gap-2.5">
        <Eyebrow className="flex items-center">
          Saúde do negócio
          <InfoTip text={vm.tooltip} />
        </Eyebrow>
        <StatusChip tone={ZONE_CHIP[vm.zona]}>{vm.verdictoLabel}</StatusChip>
      </div>

      <button
        type="button"
        onClick={() => onDrill(vm.drillDial)}
        aria-label={`Fôlego de caixa: ${vm.diasFolego ?? 'sem dado'} dias, zona ${vm.verdictoLabel}. Abrir Fluxo de caixa.`}
        className="mx-auto mt-1 block rounded-2xl pt-1 transition-colors hover:bg-surface-2/70 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
      >
        <div className="relative mx-auto w-[236px]">
          <svg viewBox={`0 0 ${W} ${H}`} className="block w-full" aria-hidden="true">
            {ZONES.map(({ a, b, tone }) => {
              const f0 = a / MAX + (a > 0 ? GAPF : 0);
              const f1 = b / MAX - (b < MAX ? GAPF : 0);
              const ativo = vm.zona === tone;
              return (
                <path
                  key={tone}
                  d={arcoPath(f0, f1, R)}
                  fill="none"
                  strokeWidth={SW}
                  strokeLinecap="round"
                  className={ZONE_STROKE[tone]}
                  opacity={ativo ? 1 : 0.22}
                />
              );
            })}
            {vm.diasFolego !== null && <circle cx={mx} cy={my} r={7} className="fill-foreground stroke-card" strokeWidth={3} />}
            <text x={CX - R} y={CY + 16} fontSize={9.5} textAnchor="middle" className="fill-faint">
              0
            </text>
            <text x={CX + R} y={CY + 16} fontSize={9.5} textAnchor="middle" className="fill-faint">
              60+
            </text>
          </svg>
          <div className="pointer-events-none absolute inset-x-0 top-[46px] text-center">
            <div className="num text-[44px] font-extrabold leading-none tracking-tight text-foreground">
              {vm.diasFolego ?? '—'}
            </div>
            <div className="mt-[3px] text-[11.5px] font-semibold text-muted-foreground">dias de fôlego</div>
          </div>
        </div>
      </button>

      <div className="mt-auto flex gap-2.5 pt-3.5">
        <button
          type="button"
          onClick={() => onDrill(vm.drillEmCaixa)}
          className="min-w-0 flex-1 rounded-xl bg-surface-2 px-3.5 py-2.5 text-left transition-transform hover:-translate-y-px hover:shadow-md focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
        >
          <div className="text-[11px] font-semibold text-muted-foreground">Em caixa</div>
          <MoneyValue centavos={vm.emCaixaCentavos} className="mt-0.5 block text-[15px] font-bold" />
        </button>
        <button
          type="button"
          onClick={() => onDrill(vm.drillPodeTirar)}
          className="min-w-0 flex-1 rounded-xl bg-surface-2 px-3.5 py-2.5 text-left transition-transform hover:-translate-y-px hover:shadow-md focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
        >
          <div className="text-[11px] font-semibold text-muted-foreground">Pode tirar</div>
          <div className="mt-0.5 flex items-center gap-1">
            <MoneyValue centavos={vm.podeTirarCentavos} className="text-[15px] font-bold" />
            <span className="text-xs font-bold text-pos">✓</span>
          </div>
        </button>
      </div>
    </Surface>
  );
}
