import { AnimatePresence, motion } from 'framer-motion';
import { useState } from 'react';

import { SectionCard } from '@/components/shared';

import { BancarioMoneyValue } from './BancarioMoneyValue';
import { semanaEntrouTotal, semanaSaiuTotal } from './derive';
import { DivergentBarChart } from './DivergentBarChart';
import { DrillBackButton } from './DrillBackButton';
import type { MovimentoExtrato, SemanaMovimento } from './types';

interface WeeksAnalysisCardProps {
  semanas: SemanaMovimento[];
  movimentos: MovimentoExtrato[];
}

/**
 * Card "Entrou × saiu por semana" ⇄ drill de dias + principais movimentos. Clique numa coluna da
 * visão geral entra no drill da semana; "←" volta. Estado local — não é uma navegação de rota.
 */
export function WeeksAnalysisCard({ semanas, movimentos }: WeeksAnalysisCardProps) {
  const [semanaId, setSemanaId] = useState<number | null>(null);
  const semana = semanaId !== null ? (semanas.find((s) => s.id === semanaId) ?? null) : null;

  const overviewItems = semanas.map((s) => ({
    id: String(s.id),
    label: s.label,
    entrouCentavos: semanaEntrouTotal(s),
    saiuCentavos: semanaSaiuTotal(s),
    muted: s.parcial,
  }));

  const drillItems = semana
    ? semana.diasLabel.map((label, i) => ({
        id: String(i),
        label,
        entrouCentavos: semana.entrouPorDiaCentavos[i],
        saiuCentavos: semana.saiuPorDiaCentavos[i],
      }))
    : [];

  const movimentosDaSemana = semana ? movimentos.filter((m) => m.semanaId === semana.id) : [];

  return (
    <SectionCard
      className="min-h-[300px]"
      title={
        semana ? (
          <span className="inline-flex items-center gap-2">
            <DrillBackButton onClick={() => setSemanaId(null)} />
            {semana.label.replace('*', '')}
          </span>
        ) : (
          'Entrou × saiu por semana'
        )
      }
      hint={semana ? 'dias + principais movimentos' : 'clique numa semana p/ ver os dias →'}
    >
      <AnimatePresence mode="wait">
        <motion.div
          key={semana ? `dias-${semana.id}` : 'semanas'}
          initial={{ opacity: 0, y: 5 }}
          animate={{ opacity: 1, y: 0 }}
          exit={{ opacity: 0 }}
          transition={{ duration: 0.22, ease: 'easeOut' }}
        >
          <div className="px-[18px] pb-0.5 pt-3.5">
            <DivergentBarChart
              items={semana ? drillItems : overviewItems}
              clickable={!semana}
              onColumnClick={(id) => setSemanaId(Number(id))}
            />
          </div>
          <div className="flex flex-wrap gap-4 px-[18px] pb-4 pt-1 text-xs text-muted-foreground">
            <span className="inline-flex items-center gap-1.5">
              <i className="h-2.5 w-2.5 rounded-[3px] bg-pos" />
              Entrou
            </span>
            <span className="inline-flex items-center gap-1.5">
              <i className="h-2.5 w-2.5 rounded-[3px] bg-crit" />
              Saiu
            </span>
            {!semana && <span className="text-faint">* semana em andamento</span>}
          </div>
          {semana && (
            <div className="flex flex-col px-[18px] pb-4">
              {movimentosDaSemana.map((m) => (
                <div
                  key={m.id}
                  className="flex items-center gap-2.5 border-b border-border/50 py-2 text-[12.5px] last:border-b-0"
                >
                  <span className="num w-[34px] flex-none text-muted-foreground">{m.data}</span>
                  <span className="min-w-0 flex-1 truncate">{m.descricao}</span>
                  <BancarioMoneyValue centavos={m.valorCentavos} signed tone="auto" className="flex-none font-bold" />
                </div>
              ))}
            </div>
          )}
        </motion.div>
      </AnimatePresence>
    </SectionCard>
  );
}
