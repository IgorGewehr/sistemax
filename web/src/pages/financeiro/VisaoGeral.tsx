import { motion } from 'framer-motion';
import { Wallet } from 'lucide-react';

import { CashTimelineSection } from '@/components/financial/visao-geral/CashTimelineSection';
import { HeroDisponivelCard } from '@/components/financial/visao-geral/HeroDisponivelCard';
import { LancarFab } from '@/components/financial/visao-geral/LancarFab';
import { LucroDoMesCard } from '@/components/financial/visao-geral/LucroDoMesCard';
import { PeriodoTrigger } from '@/components/financial/visao-geral/PeriodoTrigger';
import { ProximosVencimentosSection } from '@/components/financial/visao-geral/ProximosVencimentosSection';
import { SobrevivenciaSection } from '@/components/financial/visao-geral/sobrevivencia/SobrevivenciaSection';
import { SuperConsultorSection } from '@/components/financial/visao-geral/SuperConsultorSection';
import { useDrillNav } from '@/components/financial/visao-geral/useDrillNav';
import { useVisaoGeral } from '@/components/financial/visao-geral/useVisaoGeral';
import { MockBadge, PageHeader } from '@/components/shared';
import { EmptyState } from '@/components/ui/EmptyState';
import { Skeleton } from '@/components/ui/Skeleton';
import { Surface } from '@/components/ui/Surface';

/**
 * Visão Geral — 1:1 com `docs/ui/mockups/visao-geral.html`, com dado REAL onde já existe
 * read-model no .NET (ver `useVisaoGeral` + docs/wiring/financeiro-api-contract.md):
 *
 * - `disponivel` ← `GET /financeiro/disponivel-retirada` (real)
 * - `timeline`   ← `GET /financeiro/fluxo` (real)
 * - `consultor`  ← `GET /financeiro/consultor` (real — insights narrados/rankeados, Fase 2)
 * - `lucroDoMes`/`proximosVencimentos` ← MOCK (sem `GET /financeiro/dre` ainda), com `MockBadge`.
 * - Bloco "Sobrevivência" (novo, sem mockup próprio) ← `previsao-caixa`/`ponto-equilibrio`/
 *   `inadimplencia`/`radar-simples`, os 4 read-models da F1 que não tinham tela nenhuma.
 */
export function VisaoGeral() {
  const vm = useVisaoGeral();
  const drill = useDrillNav();

  return (
    <div>
      <PageHeader
        subtitle="Como está o dinheiro do seu negócio — em 5 segundos."
        actions={<PeriodoTrigger label={vm.periodoLabel} />}
      />

      <div className="mb-4 grid grid-cols-1 items-stretch gap-4 min-[860px]:grid-cols-[2fr_1fr]">
        <motion.div initial={{ opacity: 0, y: 8 }} animate={{ opacity: 1, y: 0 }} transition={{ duration: 0.42, delay: 0.02 }}>
          {vm.disponivel.carregando ? (
            <Surface padding="lg" className="h-full min-h-[220px]">
              <Skeleton className="h-8 w-40" />
              <Skeleton className="mt-4 h-4 w-full" />
              <Skeleton className="mt-2 h-4 w-full" />
            </Surface>
          ) : vm.disponivel.erro ? (
            <Surface padding="lg" className="h-full min-h-[220px]">
              <EmptyState
                icon={<Wallet className="h-5 w-5" />}
                title="Não deu para carregar"
                description={vm.disponivel.erro}
                className="border-none py-6"
              />
            </Surface>
          ) : (
            vm.disponivel.dado && <HeroDisponivelCard vm={vm.disponivel.dado} onDrill={drill} />
          )}
        </motion.div>

        <motion.div initial={{ opacity: 0, y: 8 }} animate={{ opacity: 1, y: 0 }} transition={{ duration: 0.42, delay: 0.06 }} className="relative">
          <MockBadge className="absolute right-3 top-3 z-[2]" titulo="Lucro do mês precisa do DRE — endpoint ainda não exposto." />
          <LucroDoMesCard vm={vm.lucroDoMes} onDrill={drill} />
        </motion.div>
      </div>

      <motion.div initial={{ opacity: 0, y: 8 }} animate={{ opacity: 1, y: 0 }} transition={{ duration: 0.42, delay: 0.1 }} className="mb-4">
        {vm.timeline.carregando ? (
          <Surface padding="lg" className="min-h-[260px]">
            <Skeleton className="h-4 w-56" />
            <Skeleton className="mt-6 h-[180px] w-full" />
          </Surface>
        ) : vm.timeline.erro ? (
          <Surface padding="lg" className="min-h-[260px]">
            <EmptyState
              icon={<Wallet className="h-5 w-5" />}
              title="Não deu para carregar"
              description={vm.timeline.erro}
              className="border-none py-6"
            />
          </Surface>
        ) : (
          vm.timeline.dado && <CashTimelineSection vm={vm.timeline.dado} />
        )}
      </motion.div>

      <motion.div initial={{ opacity: 0, y: 8 }} animate={{ opacity: 1, y: 0 }} transition={{ duration: 0.42, delay: 0.14 }} className="relative mb-4">
        <MockBadge className="absolute right-3 top-3 z-[2]" titulo="Precisa juntar contas a pagar/receber por vencimento — read-model ainda não existe." />
        <ProximosVencimentosSection vencimentos={vm.proximosVencimentos} onDrill={drill} />
      </motion.div>

      <motion.div initial={{ opacity: 0, y: 8 }} animate={{ opacity: 1, y: 0 }} transition={{ duration: 0.42, delay: 0.18 }} className="mb-4">
        <SuperConsultorSection recurso={vm.consultor} onDrill={drill} />
      </motion.div>

      <motion.div initial={{ opacity: 0, y: 8 }} animate={{ opacity: 1, y: 0 }} transition={{ duration: 0.42, delay: 0.22 }}>
        <SobrevivenciaSection
          runway={vm.sobrevivencia.runway}
          breakeven={vm.sobrevivencia.breakeven}
          inadimplencia={vm.sobrevivencia.inadimplencia}
          radarSimples={vm.sobrevivencia.radarSimples}
        />
      </motion.div>

      <LancarFab />
    </div>
  );
}
