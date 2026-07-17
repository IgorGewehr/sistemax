import { ConsultorInsight } from '@/components/shared';

import type { ConsultorDashboardViewModel, DrillTarget } from './types';

interface ConsultorDashboardProps {
  vm: ConsultorDashboardViewModel;
  onDrill: (target: DrillTarget) => void;
}

/**
 * Super Consultor do Dashboard (bloco ④, única IA da tela) — cruza dois módulos (Estoque + Vendas)
 * numa frase só, o que nenhum KPI isolado do bloco ② mostra sozinho. Só observa e aconselha (Lei
 * 2: read-only) — o link é navegação de verdade, nunca uma ação que o Consultor executa.
 */
export function ConsultorDashboard({ vm, onDrill }: ConsultorDashboardProps) {
  return (
    <ConsultorInsight action={{ label: 'Ver estoque →', onClick: () => onDrill(vm.drill) }}>
      <b className="font-bold text-primary-600">{vm.itemNome}</b> está acabando (
      <b className="font-bold text-crit">
        {vm.quantidadeRestante} {vm.unidade}
      </b>
      ) e é o item mais vendido do fim de semana — {vm.mediaVendaLabel}. {vm.previsaoLabel}
    </ConsultorInsight>
  );
}
