import { Eyebrow } from '@/components/shared';
import { Surface } from '@/components/ui/Surface';
import { cn } from '@/lib/utils';

import { diferencaCentavos, esperadoCentavos, totalSangriasCentavos, totalSuprimentosCentavos } from './calc';
import { formatCentavosWhole, MoneyWhole } from './MoneyWhole';
import type { SessaoCaixa } from './types';

interface SessaoHojeFormulaProps {
  sessaoHoje: SessaoCaixa;
}

/** O ritual de hoje resumido numa linha: abertura + vendas + suprimento − sangria − troco =
 * esperado — e, assim que fecha, o resultado da contagem cega logo abaixo. */
export function SessaoHojeFormula({ sessaoHoje }: SessaoHojeFormulaProps) {
  const sangriaTotal = totalSangriasCentavos(sessaoHoje);
  const suprimentoTotal = totalSuprimentosCentavos(sessaoHoje);
  const esperado = esperadoCentavos(sessaoHoje);
  const fechado = sessaoHoje.status === 'fechado';
  const diff = fechado ? diferencaCentavos(sessaoHoje) ?? 0 : null;

  return (
    <Surface padding="none" className="mb-4 flex flex-wrap items-baseline gap-x-2 gap-y-1.5 px-[18px] py-[13px]">
      <Eyebrow className="mb-0.5 w-full">Sessão de hoje</Eyebrow>

      <span className="text-[13.5px]">
        abertura <MoneyWhole centavos={sessaoHoje.aberturaCentavos} className="font-bold" />
      </span>
      <span className="font-semibold text-faint">+</span>
      <span className="text-[13.5px]">
        vendas espécie <MoneyWhole centavos={sessaoHoje.vendasEspecieCentavos} tone="pos" className="font-bold" />
      </span>
      <span className="font-semibold text-faint">+</span>
      <span className="text-[13.5px]">
        suprimento <MoneyWhole centavos={suprimentoTotal} tone="pos" className="font-bold" />
      </span>
      <span className="font-semibold text-faint">−</span>
      <span className="text-[13.5px]">
        sangria <MoneyWhole centavos={sangriaTotal} tone="crit" className="font-bold" />
      </span>
      <span className="font-semibold text-faint">−</span>
      <span className="text-[13.5px]">
        troco <MoneyWhole centavos={sessaoHoje.trocoCentavos} tone="crit" className="font-bold" />
      </span>
      <span className="font-bold text-faint">=</span>
      <MoneyWhole centavos={esperado} className="text-[15px] font-bold" />

      {fechado && diff !== null && (
        <div className="mt-1 w-full text-[12.5px] text-muted-foreground">
          Contado <MoneyWhole centavos={sessaoHoje.contadoCentavos} className="font-bold text-foreground" /> →{' '}
          <b className={cn('font-bold', diff === 0 ? 'text-muted-foreground' : diff > 0 ? 'text-pos' : 'text-crit')}>
            {diff === 0 ? 'bateu certinho' : diff > 0 ? `sobra de ${formatCentavosWhole(diff)}` : `falta de ${formatCentavosWhole(Math.abs(diff))}`}
          </b>
        </div>
      )}
    </Surface>
  );
}
