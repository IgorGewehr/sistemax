import { AnimatePresence, motion } from 'framer-motion';
import { ArrowLeft } from 'lucide-react';
import type { RefObject } from 'react';

import { SectionCard } from '@/components/shared';

import { DIA_SEMANA_COMPLETO, descreverDiferenca, diaLabel, sessoesFechadas, type DiaCritico } from './calc';
import { OverviewChart } from './OverviewChart';
import { PadraoCaixaStats } from './PadraoCaixaStats';
import { SessaoDrillStats } from './SessaoDrillStats';
import { SessaoDrillTimeline } from './SessaoDrillTimeline';
import type { DiaSemanaAbrev, SessaoCaixa } from './types';

interface AnaliseInterativaProps {
  todasAsSessoes: SessaoCaixa[];
  diaCritico: DiaCritico | null;
  mediaDiferencaCentavos: number;
  vendasEspeciePercentual: number;
  selectedDay: number | null;
  pulsingWeekday: DiaSemanaAbrev | null;
  onSelectDay: (dia: number) => void;
  onVoltarOverview: () => void;
  onAbrirModalFechar: () => void;
  containerRef: RefObject<HTMLDivElement | null>;
}

const TRANSITION = { duration: 0.22 };

/** Análise interativa: por padrão mostra o mês inteiro (gráfico ⇄ padrão); clicar num dia — na
 * barra, na tabela ou no "Ver as quintas →" do Consultor — troca as duas colunas para o drill
 * completo daquela sessão, com um botão "←" pra voltar. */
export function AnaliseInterativa({
  todasAsSessoes,
  diaCritico,
  mediaDiferencaCentavos,
  vendasEspeciePercentual,
  selectedDay,
  pulsingWeekday,
  onSelectDay,
  onVoltarOverview,
  onAbrirModalFechar,
  containerRef,
}: AnaliseInterativaProps) {
  const sessaoSelecionada = selectedDay === null ? null : todasAsSessoes.find((s) => s.dia === selectedDay) ?? null;
  const diasFechadosCount = sessoesFechadas(todasAsSessoes).length;

  return (
    <section ref={containerRef} className="mb-4 grid gap-3.5 lg:grid-cols-[1.15fr_1fr]">
      <SectionCard
        title={
          sessaoSelecionada ? (
            <span className="inline-flex items-center gap-2">
              <button
                type="button"
                onClick={onVoltarOverview}
                aria-label="Voltar para o mês"
                className="grid h-[26px] w-[26px] flex-none place-items-center rounded-lg bg-surface-2 text-foreground transition-colors hover:bg-primary-soft hover:text-primary-600"
              >
                <ArrowLeft className="h-3.5 w-3.5" />
              </button>
              {diaLabel(sessaoSelecionada.dia)} · {DIA_SEMANA_COMPLETO[sessaoSelecionada.diaSemana]}
            </span>
          ) : (
            'Diferenças por dia (mês)'
          )
        }
        hint={sessaoSelecionada ? 'Sessão completa' : 'sobra × falta — a quebra de caixa · clique num dia →'}
      >
        <AnimatePresence mode="wait" initial={false}>
          {sessaoSelecionada ? (
            <motion.div key={`drill-esq-${sessaoSelecionada.dia}`} initial={{ opacity: 0, y: 5 }} animate={{ opacity: 1, y: 0 }} exit={{ opacity: 0 }} transition={TRANSITION}>
              <SessaoDrillTimeline sessao={sessaoSelecionada} />
            </motion.div>
          ) : (
            <motion.div key="overview-esq" initial={{ opacity: 0, y: 5 }} animate={{ opacity: 1, y: 0 }} exit={{ opacity: 0 }} transition={TRANSITION}>
              <div className="px-[18px] pb-1.5 pt-3.5">
                <OverviewChart dias={todasAsSessoes} pulsingWeekday={pulsingWeekday} onSelectDay={onSelectDay} />
              </div>
              <div className="flex gap-4 px-[18px] pb-4 text-xs text-muted-foreground">
                <span className="inline-flex items-center gap-1.5">
                  <i className="h-2.5 w-2.5 rounded-[3px] bg-pos" />
                  Sobra
                </span>
                <span className="inline-flex items-center gap-1.5">
                  <i className="h-2.5 w-2.5 rounded-[3px] bg-crit" />
                  Falta
                </span>
                <span className="inline-flex items-center gap-1.5">
                  <i className="h-2.5 w-2.5 rounded-[3px] bg-faint" />
                  Bateu certinho
                </span>
              </div>
            </motion.div>
          )}
        </AnimatePresence>
      </SectionCard>

      <SectionCard
        title={sessaoSelecionada ? `${sessaoSelecionada.operador} · ${DIA_SEMANA_COMPLETO[sessaoSelecionada.diaSemana]}` : 'Padrão do caixa'}
        hint={sessaoSelecionada ? descreverDiferenca(sessaoSelecionada) : 'o que os números escondem'}
      >
        <AnimatePresence mode="wait" initial={false}>
          {sessaoSelecionada ? (
            <motion.div key={`drill-dir-${sessaoSelecionada.dia}`} initial={{ opacity: 0, y: 5 }} animate={{ opacity: 1, y: 0 }} exit={{ opacity: 0 }} transition={TRANSITION}>
              <SessaoDrillStats sessao={sessaoSelecionada} onAbrirModalFechar={onAbrirModalFechar} />
            </motion.div>
          ) : (
            <motion.div key="overview-dir" initial={{ opacity: 0, y: 5 }} animate={{ opacity: 1, y: 0 }} exit={{ opacity: 0 }} transition={TRANSITION}>
              <PadraoCaixaStats
                diaCritico={diaCritico}
                mediaDiferencaCentavos={mediaDiferencaCentavos}
                diasFechadosCount={diasFechadosCount}
                vendasEspeciePercentual={vendasEspeciePercentual}
              />
            </motion.div>
          )}
        </AnimatePresence>
      </SectionCard>
    </section>
  );
}
