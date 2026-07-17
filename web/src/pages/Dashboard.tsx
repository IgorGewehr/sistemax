import { motion } from 'framer-motion';

import { AtencaoSection } from '@/components/dashboard/AtencaoSection';
import { ConsultorDashboard } from '@/components/dashboard/ConsultorDashboard';
import { FreshnessBadge } from '@/components/dashboard/FreshnessBadge';
import { KpiRow } from '@/components/dashboard/KpiRow';
import type { ModuloDashboard } from '@/components/dashboard/types';
import { useDashboardDrill } from '@/components/dashboard/useDashboardDrill';
import { usePermissoesDashboard, type PermissoesDashboard } from '@/components/dashboard/usePermissoesDashboard';
import { PageHeader } from '@/components/shared';
import { DASHBOARD_MOCK } from '@/mocks/dashboard';

/** Qual flag de `usePermissoesDashboard` libera cada módulo — fonte única de mapeamento, usada
 * pra filtrar KPIs e itens de atenção antes de chegar em qualquer seção. */
const FLAG_POR_MODULO: Record<ModuloDashboard, keyof PermissoesDashboard> = {
  vendas: 'podeVerVendas',
  financeiro: 'podeVerFinanceiro',
  estoque: 'podeVerEstoque',
  compras: 'podeVerCompras',
  os: 'podeVerOs',
};

/**
 * Dashboard — visão do negócio inteiro num olhar. Página fina: filtra o view-model (mock hoje,
 * API amanhã) pelas permissões e compõe as 3 seções, nesta ordem: números-chave dos 5 módulos →
 * o que precisa de atenção agora → um insight cross-módulo do Super Consultor.
 *
 * PERMISSION-AWARE: o filtro por `usePermissoesDashboard` acontece AQUI, uma única vez — as seções
 * (`KpiRow`, `AtencaoSection`, `ConsultorDashboard`) só recebem o que já pode aparecer; nenhuma
 * delas reavalia permissão sozinha.
 */
export function Dashboard() {
  const vm = DASHBOARD_MOCK;
  const permissoes = usePermissoesDashboard();
  const drill = useDashboardDrill();

  const podeVerModulo = (modulo: ModuloDashboard) => permissoes[FLAG_POR_MODULO[modulo]];

  const kpis = vm.kpis.filter((kpi) => podeVerModulo(kpi.modulo));
  const atencao = vm.atencao.filter((item) => podeVerModulo(item.modulo));
  // O insight de hoje cruza Vendas com Estoque — só aparece se as duas permissões estiverem de pé.
  const mostrarConsultor = podeVerModulo('estoque') && podeVerModulo('vendas');

  return (
    <div className="mx-auto max-w-6xl px-4 py-6 sm:px-6 lg:py-8">
      <PageHeader
        subtitle="Como está o seu negócio agora — vendas, caixa, estoque, compras e ordens em um só lugar."
        actions={<FreshnessBadge />}
      />

      <motion.div initial={{ opacity: 0, y: 8 }} animate={{ opacity: 1, y: 0 }} transition={{ duration: 0.42, delay: 0.02 }}>
        <KpiRow kpis={kpis} onDrill={drill} />
      </motion.div>

      <motion.div
        initial={{ opacity: 0, y: 8 }}
        animate={{ opacity: 1, y: 0 }}
        transition={{ duration: 0.42, delay: 0.08 }}
        className="mb-4"
      >
        <AtencaoSection itens={atencao} onDrill={drill} />
      </motion.div>

      {mostrarConsultor && (
        <motion.div initial={{ opacity: 0, y: 8 }} animate={{ opacity: 1, y: 0 }} transition={{ duration: 0.42, delay: 0.14 }}>
          <ConsultorDashboard vm={vm.consultor} onDrill={drill} />
        </motion.div>
      )}
    </div>
  );
}
