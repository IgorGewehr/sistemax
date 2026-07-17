/**
 * Análise interativa da lente Contas fixas: índice clicável ⇄ drill do item —
 * mockup: `#cardEsqFixas` + `#cardDirFixas` (`overviewFixas`/`drillFixas`/`goFixas`).
 */
import { AnimatePresence, motion } from 'framer-motion';
import type { RefObject } from 'react';

import { SectionCard } from '@/components/shared';
import type { Centavos } from '@/lib/money';

import { anosDesde, formatCentavosWhole, formatPctPlain, formatPctSigned } from './calc';
import { ColumnHistoryChart } from './charts';
import { DrillHeaderTitle, RankedBarList, StatTile, StatTiles } from './primitives';
import type { ContaFixaDerivada, RetratoFixo } from './types';

interface FixasAnalisePanelProps {
  itens: ContaFixaDerivada[];
  totalAtual: Centavos;
  retrato: RetratoFixo;
  selectedId: string | null;
  onSelect: (id: string | null) => void;
  leftCardRef: RefObject<HTMLDivElement | null>;
}

export function FixasAnalisePanel({ itens, totalAtual, retrato, selectedId, onSelect, leftCardRef }: FixasAnalisePanelProps) {
  const selected = selectedId ? (itens.find((i) => i.id === selectedId) ?? null) : null;
  const animKey = selected ? selected.id : 'overview';

  const rows = [...itens]
    .sort((a, b) => b.atual - a.atual)
    .map((it) => {
      const pct = totalAtual > 0 ? (it.atual / totalAtual) * 100 : 0;
      return {
        id: it.id,
        name: (
          <>
            {it.nome}
            {it.emAlerta && <span className="ml-1.5 font-bold text-warn">⚠ +{Math.round(it.variacaoPct)}%</span>}
          </>
        ),
        amount: formatCentavosWhole(it.atual),
        pct,
        pctLabel: formatPctPlain(pct),
        barClassName: it.emAlerta ? 'bg-warn' : 'bg-foreground/40',
      };
    });

  return (
    <section className="mb-4 grid gap-4 lg:grid-cols-[1.15fr_1fr]">
      <div ref={leftCardRef}>
        <SectionCard
          className="min-h-[236px]"
          title={
            selected ? (
              <DrillHeaderTitle onBack={() => onSelect(null)} backLabel="Voltar para os compromissos">
                {selected.nome}
              </DrillHeaderTitle>
            ) : (
              'Seus compromissos fixos'
            )
          }
          hint={selected ? '12 meses · degraus saltam à vista' : 'clique numa recorrência p/ ver histórico e degraus →'}
        >
          <AnimatePresence mode="wait">
            <motion.div
              key={animKey}
              initial={{ opacity: 0, y: 5 }}
              animate={{ opacity: 1, y: 0 }}
              exit={{ opacity: 0 }}
              transition={{ duration: 0.18 }}
            >
              {selected ? (
                <div className="px-3 pb-1 pt-2 sm:px-4">
                  <ColumnHistoryChart
                    nome={selected.nome}
                    historico12m={selected.historico12m}
                    media6m={selected.media6m}
                    variacaoPct={selected.variacaoPct}
                    emAlerta={selected.emAlerta}
                  />
                  <div className="flex gap-4 px-1 pb-3 pt-1.5 text-xs text-muted-foreground">
                    <span className="inline-flex items-center gap-1.5">
                      <span className="inline-block h-0 w-3.5 border-t-2 border-dashed border-muted-foreground" />
                      média (6 meses)
                    </span>
                    {selected.emAlerta ? (
                      <span className="inline-flex items-center gap-1.5">
                        <span className="h-2.5 w-2.5 rounded-sm bg-crit" />
                        mês atual, acima da média
                      </span>
                    ) : (
                      <span className="inline-flex items-center gap-1.5">
                        <span className="h-2.5 w-2.5 rounded-sm bg-foreground/55" />
                        mês atual
                      </span>
                    )}
                  </div>
                </div>
              ) : (
                <RankedBarList rows={rows} onSelect={onSelect} scrollable />
              )}
            </motion.div>
          </AnimatePresence>
        </SectionCard>
      </div>

      <SectionCard
        className="min-h-[236px]"
        title={selected ? selected.nome : 'Retrato do fixo'}
        hint={selected ? selected.categoria : undefined}
      >
        <AnimatePresence mode="wait">
          <motion.div
            key={animKey}
            initial={{ opacity: 0, y: 5 }}
            animate={{ opacity: 1, y: 0 }}
            exit={{ opacity: 0 }}
            transition={{ duration: 0.18 }}
          >
            {selected ? (
              <StatTiles>
                <StatTile k="Média (6 meses)" v={formatCentavosWhole(selected.media6m)} s="base de comparação" />
                <StatTile k="Ativa desde" v={selected.ativaDesde} s={anosDesde(selected.ativaDesde)} />
                <StatTile k="Total pago no ano" v={formatCentavosWhole(selected.totalAnoCorrente)} s="janeiro a julho/2026" />
              </StatTiles>
            ) : (
              <StatTiles>
                <StatTile k="Projeção anual" v={formatCentavosWhole(retrato.projecaoAnual)} s="se tudo continuar igual a julho" />
                <StatTile
                  k="Variação em 6 meses"
                  v={formatPctSigned(retrato.variacaoSeisMesesPct)}
                  s={`de ${formatCentavosWhole(retrato.totalHaSeisMeses)} pra ${formatCentavosWhole(retrato.totalAtual)}`}
                />
                <StatTile k="Compromissos ativos" v={retrato.compromissosAtivos} s="nenhum cancelado este ano" />
              </StatTiles>
            )}
          </motion.div>
        </AnimatePresence>
      </SectionCard>
    </section>
  );
}
