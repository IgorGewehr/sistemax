import { SectionCard } from '@/components/shared';
import type { RoiDoNegocioDto } from '@/lib/api/financeiro';

import { RoiChart } from './RoiChart';


interface RoiChartCardProps {
  roi: RoiDoNegocioDto;
}

/** `.roi-card` do mockup — título + legenda inline + o SVG. */
export function RoiChartCard({ roi }: RoiChartCardProps) {
  return (
    <SectionCard title="Investido × recuperado — quando o negócio se paga" hint="o cruzamento é o payback" className="mb-4">
      <div className="flex flex-wrap gap-4 px-[18px] pb-0.5 pt-1.5 text-xs text-muted-foreground">
        <span className="inline-flex items-center gap-1.5">
          <i className="inline-block h-[2.5px] w-4 rounded-sm bg-primary-600" />
          caixa recuperado (realizado)
        </span>
        <span className="inline-flex items-center gap-1.5">
          <i className="inline-block h-0 w-4 border-t-[2.5px] border-dashed border-primary-600/80" />
          recuperado (previsto)
        </span>
        <span className="inline-flex items-center gap-1.5">
          <i className="inline-block h-0 w-4 border-t-2 border-dashed border-foreground/55" />
          total investido
        </span>
        <span className="inline-flex items-center gap-1.5">
          <i className="inline-block h-2.5 w-3 rounded-[3px] bg-crit-soft" />
          falta recuperar
        </span>
      </div>
      <RoiChart roi={roi} />
    </SectionCard>
  );
}
