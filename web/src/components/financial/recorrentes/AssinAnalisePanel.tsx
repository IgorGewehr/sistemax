/**
 * Análise interativa do resumo da lente Assinaturas: MRR por serviço ⇄ Novos×Churn
 * e retenção por serviço — mockup: `#cardEsqAssin` + `#cardDirAssin` (`overviewA`/`projetoA`/`goA`).
 */
import { AnimatePresence, motion } from 'framer-motion';
import type { RefObject } from 'react';

import { SectionCard } from '@/components/shared';
import type { Centavos } from '@/lib/money';

import { calcularPctMrr, formatCentavosWhole, formatPctPlain } from './calc';
import { DivergentFlowChart } from './charts';
import { DrillHeaderTitle, RankedBarList, StatTile, StatTiles } from './primitives';
import type { AssinaturaServico, CarteiraAssinaturas } from './types';

interface AssinAnalisePanelProps {
  servicos: AssinaturaServico[];
  mrrTotal: Centavos;
  carteira: CarteiraAssinaturas;
  selectedId: string | null;
  onSelect: (id: string | null) => void;
  leftCardRef: RefObject<HTMLDivElement | null>;
}

export function AssinAnalisePanel({ servicos, mrrTotal, carteira, selectedId, onSelect, leftCardRef }: AssinAnalisePanelProps) {
  const selected = selectedId ? (servicos.find((s) => s.id === selectedId) ?? null) : null;
  const animKey = selected ? selected.id : 'overview';

  const rows = servicos.map((s) => {
    const pct = calcularPctMrr(s, mrrTotal);
    return {
      id: s.id,
      name: (
        <span className="inline-flex items-center gap-2">
          <span className={`h-2.5 w-2.5 shrink-0 rounded-sm ${s.corClasse}`} />
          {s.nome}
        </span>
      ),
      amount: formatCentavosWhole(s.mrr),
      pct,
      pctLabel: formatPctPlain(pct),
      barClassName: s.corClasse,
    };
  });

  return (
    <section className="mb-4 grid gap-4 lg:grid-cols-[1.15fr_1fr]">
      <div ref={leftCardRef}>
        <SectionCard
          className="min-h-[236px]"
          title={
            selected ? (
              <DrillHeaderTitle onBack={() => onSelect(null)} backLabel="Voltar para a carteira">
                {selected.nome}
              </DrillHeaderTitle>
            ) : (
              'MRR por serviço'
            )
          }
          hint={selected ? 'Novos × Churn, 6 meses' : 'clique num serviço p/ churn e retenção →'}
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
                  <DivergentFlowChart novos6m={selected.novos6m} churn6m={selected.churn6m} />
                  <div className="flex gap-4 px-1 pb-3 pt-1.5 text-xs text-muted-foreground">
                    <span className="inline-flex items-center gap-1.5">
                      <span className="h-2.5 w-2.5 rounded-sm bg-pos" />
                      Novos
                    </span>
                    <span className="inline-flex items-center gap-1.5">
                      <span className="h-2.5 w-2.5 rounded-sm bg-crit" />
                      Churn
                    </span>
                  </div>
                </div>
              ) : (
                <RankedBarList rows={rows} onSelect={onSelect} />
              )}
            </motion.div>
          </AnimatePresence>
        </SectionCard>
      </div>

      <SectionCard
        className="min-h-[236px]"
        title={selected ? `${selected.nome} · retenção` : 'Retenção da carteira'}
        hint={
          selected
            ? `${selected.clientes} ${selected.clientes > 1 ? 'clientes' : 'cliente'} · ${selected.churnClientesMes} churn no mês`
            : 'quanto tempo o cliente fica'
        }
      >
        <AnimatePresence mode="wait">
          <motion.div
            key={animKey}
            initial={{ opacity: 0, y: 5 }}
            animate={{ opacity: 1, y: 0 }}
            exit={{ opacity: 0 }}
            transition={{ duration: 0.18 }}
          >
            <StatTiles>
              <StatTile
                k="Tempo médio de casa"
                v={
                  <>
                    {(selected ?? carteira).tempoMedioMeses} <small className="text-[13px] font-semibold text-muted-foreground">meses</small>
                  </>
                }
                s="antes de cancelar"
              />
              <StatTile k="LTV médio estimado" v={formatCentavosWhole((selected ?? carteira).ltv)} s="ticket × tempo de vida" />
              <StatTile
                k="Retenção em 12 meses"
                v={
                  <>
                    {(selected ?? carteira).retencaoPct}
                    <small className="text-[13px] font-semibold text-muted-foreground">%</small>
                  </>
                }
                s="seguem ativos após 1 ano"
              />
            </StatTiles>
          </motion.div>
        </AnimatePresence>
      </SectionCard>
    </section>
  );
}
