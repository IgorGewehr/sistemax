import { motion } from 'framer-motion';
import { Wallet } from 'lucide-react';
import type { ReactNode } from 'react';

import { CashTimelineSection } from '@/components/financial/visao-geral/CashTimelineSection';
import { GaugeCard } from '@/components/financial/visao-geral/GaugeCard';
import { InvestimentoMiniCard } from '@/components/financial/visao-geral/InvestimentoMiniCard';
import { LancarFab } from '@/components/financial/visao-geral/LancarFab';
import { MixCorrentesCard } from '@/components/financial/visao-geral/MixCorrentesCard';
import { PeriodoTrigger } from '@/components/financial/visao-geral/PeriodoTrigger';
import { SimplesMiniCard } from '@/components/financial/visao-geral/SimplesMiniCard';
import { TileAPagar } from '@/components/financial/visao-geral/TileAPagar';
import { TileAReceber } from '@/components/financial/visao-geral/TileAReceber';
import { TileAssinaturas } from '@/components/financial/visao-geral/TileAssinaturas';
import { TileResultado } from '@/components/financial/visao-geral/TileResultado';
import { useDrillNav } from '@/components/financial/visao-geral/useDrillNav';
import { useVisaoGeral } from '@/components/financial/visao-geral/useVisaoGeral';
import { PageHeader } from '@/components/shared';
import { EmptyState } from '@/components/ui/EmptyState';
import { Skeleton } from '@/components/ui/Skeleton';
import { Surface } from '@/components/ui/Surface';
import { cn } from '@/lib/utils';

/**
 * Visão Geral v3 — 1:1 com `docs/ui/mockups/visao-geral-v3.html`: medidor de fôlego (dominante) +
 * projeção de caixa, 4 tiles (a receber/a pagar/resultado/assinaturas), mix das 3 correntes, ROI
 * (opt-in) e Radar do Simples. SEM Super Consultor (a v3 tirou a IA desta tela) e SEM "próximos
 * vencimentos" da v2 — o sub de cada tile já carrega o que era essa seção. Sem título/eyebrow
 * repetindo "Visão Geral": a aba ativa do `FinanceiroLayout` já diz isso (mesmo padrão do
 * `PageHeader` em todas as outras telas do módulo).
 *
 * `useVisaoGeral` devolve um `Recurso<T>` por bloco — um endpoint fora do ar não derruba os
 * outros. ROI é opt-in de verdade: desligado em Configurações, a coluna some e a fila de baixo
 * reflui pra 2 colunas (mesmo comportamento do toggle de demo do mockup) — nunca um convite ou um
 * toggle na própria tela (Lei 2: só o usuário liga isso, em Configurações).
 */
export function VisaoGeral() {
  const vm = useVisaoGeral();
  const drill = useDrillNav();

  const configResolved = !vm.configuracao.carregando && !vm.configuracao.erro;
  // Otimista enquanto a config ainda carrega — evita o layout "pular" de 3 pra 2 colunas se o
  // toggle realmente estiver ligado (o caso mais comum).
  const roiLigado = vm.configuracao.dado?.imobilizadoRoiAtivo ?? true;
  const mostrarColunaInvestimento = !configResolved || roiLigado;

  return (
    <div>
      <PageHeader subtitle="Bateu o olho, entendeu." actions={<PeriodoTrigger label={vm.periodoLabel} />} />

      {/* ① Medidor de fôlego (dominante) + projeção do caixa */}
      <section className="mb-4 grid grid-cols-1 items-stretch gap-4 min-[980px]:grid-cols-[340px_1fr]">
        <Anim delay={0.02}>
          {vm.gauge.carregando ? (
            <Surface padding="lg" className="h-full min-h-[300px]">
              <Skeleton className="h-3 w-32" />
              <Skeleton className="mx-auto mt-6 h-[130px] w-[200px] rounded-full" />
              <Skeleton className="mt-6 h-10 w-full" />
            </Surface>
          ) : vm.gauge.erro || !vm.gauge.dado ? (
            <Surface padding="lg" className="h-full min-h-[300px]">
              <ErroBloco mensagem={vm.gauge.erro} />
            </Surface>
          ) : (
            <GaugeCard vm={vm.gauge.dado} onDrill={drill} />
          )}
        </Anim>

        <Anim delay={0.06}>
          {vm.timeline.carregando ? (
            <Surface padding="lg" className="min-h-[260px]">
              <Skeleton className="h-4 w-56" />
              <Skeleton className="mt-6 h-[180px] w-full" />
            </Surface>
          ) : vm.timeline.erro || !vm.timeline.dado ? (
            <Surface padding="lg" className="min-h-[260px]">
              <ErroBloco mensagem={vm.timeline.erro} />
            </Surface>
          ) : (
            <CashTimelineSection vm={vm.timeline.dado} />
          )}
        </Anim>
      </section>

      {/* ② Tiles: a receber · a pagar · resultado · assinaturas */}
      <Anim delay={0.1}>
        <section className="mb-4 grid grid-cols-1 gap-3.5 sm:grid-cols-2 min-[1040px]:grid-cols-4">
          {vm.abertoResumo.carregando ? (
            <>
              <TileSkeleton />
              <TileSkeleton />
            </>
          ) : vm.abertoResumo.erro || !vm.abertoResumo.dado ? (
            <>
              <TileErroCard mensagem={vm.abertoResumo.erro} />
              <TileErroCard mensagem={vm.abertoResumo.erro} />
            </>
          ) : (
            <>
              <TileAReceber vm={vm.abertoResumo.dado.receber} onDrill={drill} />
              <TileAPagar vm={vm.abertoResumo.dado.pagar} onDrill={drill} />
            </>
          )}

          {vm.dre.carregando ? (
            <TileSkeleton />
          ) : vm.dre.erro || !vm.dre.dado ? (
            <TileErroCard mensagem={vm.dre.erro} />
          ) : (
            <TileResultado vm={vm.dre.dado.resultado} onDrill={drill} />
          )}

          {vm.recorrente.carregando ? (
            <TileSkeleton />
          ) : vm.recorrente.erro || !vm.recorrente.dado ? (
            <TileErroCard mensagem={vm.recorrente.erro} />
          ) : (
            <TileAssinaturas vm={vm.recorrente.dado} onDrill={drill} />
          )}
        </section>
      </Anim>

      {/* ③ Mix de receita · Investimento (opt-in) · Radar do Simples */}
      <Anim delay={0.14}>
        <section
          className={cn(
            'grid grid-cols-1 gap-3.5 sm:grid-cols-2',
            mostrarColunaInvestimento ? 'min-[1040px]:grid-cols-[1.3fr_1fr_1fr]' : 'min-[1040px]:grid-cols-[1.3fr_1fr]',
          )}
        >
          {vm.dre.carregando ? (
            <TileSkeleton className="sm:col-span-2 lg:col-span-1" />
          ) : vm.dre.erro ? (
            <TileErroCard mensagem={vm.dre.erro} className="sm:col-span-2 lg:col-span-1" />
          ) : vm.dre.dado?.mix ? (
            <MixCorrentesCard vm={vm.dre.dado.mix} onDrill={drill} />
          ) : (
            <Surface padding="lg" className="min-h-[130px] sm:col-span-2 lg:col-span-1">
              <EmptyState
                icon={<Wallet className="h-5 w-5" />}
                title="Sem receita no período"
                description="Assim que houver receita reconhecida no mês, o mix por corrente aparece aqui."
                className="border-none py-4"
              />
            </Surface>
          )}

          {mostrarColunaInvestimento &&
            (!configResolved || vm.investimento.carregando ? (
              <TileSkeleton />
            ) : vm.investimento.erro ? (
              <TileErroCard mensagem={vm.investimento.erro} />
            ) : vm.investimento.dado ? (
              <InvestimentoMiniCard vm={vm.investimento.dado} onDrill={drill} />
            ) : null)}

          {vm.radar.carregando ? (
            <TileSkeleton />
          ) : vm.radar.erro || !vm.radar.dado ? (
            <TileErroCard mensagem={vm.radar.erro} />
          ) : (
            <SimplesMiniCard vm={vm.radar.dado} onDrill={drill} />
          )}
        </section>
      </Anim>

      <LancarFab />
    </div>
  );
}

function Anim({ delay, children }: { delay: number; children: ReactNode }) {
  return (
    <motion.div initial={{ opacity: 0, y: 8 }} animate={{ opacity: 1, y: 0 }} transition={{ duration: 0.42, delay }} className="h-full">
      {children}
    </motion.div>
  );
}

function ErroBloco({ mensagem }: { mensagem: string | null }) {
  return (
    <EmptyState
      icon={<Wallet className="h-5 w-5" />}
      title="Não deu para carregar"
      description={mensagem ?? 'Nada retornou do servidor.'}
      className="border-none py-6"
    />
  );
}

function TileSkeleton({ className }: { className?: string }) {
  return (
    <Surface padding="lg" className={cn('min-h-[130px]', className)}>
      <Skeleton className="h-3 w-20" />
      <Skeleton className="mt-3 h-6 w-24" />
      <Skeleton className="mt-3 h-2 w-full" />
    </Surface>
  );
}

function TileErroCard({ mensagem, className }: { mensagem: string | null; className?: string }) {
  return (
    <Surface padding="lg" className={cn('min-h-[130px]', className)}>
      <ErroBloco mensagem={mensagem} />
    </Surface>
  );
}
