import { AnimatePresence, motion } from 'framer-motion';
import { ChevronDown, Sparkles } from 'lucide-react';
import { useState } from 'react';

import { Button } from '@/components/ui/Button';
import { Skeleton } from '@/components/ui/Skeleton';
import { cn } from '@/lib/utils';

import type { ConsultorViewModel, DrillTarget, InsightConsultor } from './types';
import type { Recurso } from './useVisaoGeral';

interface SuperConsultorSectionProps {
  recurso: Recurso<ConsultorViewModel>;
  onDrill: (target: DrillTarget) => void;
}

/**
 * Super Consultor (bloco ⑤, única IA da tela) — só observa/explica/aconselha (Lei 2: read-only).
 * Consome `GET /financeiro/consultor`: insights JÁ narrados e rankeados pelo backend. O card
 * (borda em gradiente, ícone, tipografia) replica 1:1 `docs/ui/mockups/visao-geral.html` — a fonte
 * de dados trocou de mock para real, o layout não. Por padrão só a PRIORIDADE (o insight de maior
 * rank) aparece como parágrafo-destaque + os affordances read-only ("Ver de onde vem"/"Ver como
 * calculamos"); os demais insights ficam colapsados atrás de "ver mais N →" — o dono quer "passar
 * o que importa rápido", não uma lista de 6+ observações despejada de uma vez. Nenhum CTA de ação
 * — só narração e navegação.
 */
export function SuperConsultorSection({ recurso, onDrill }: SuperConsultorSectionProps) {
  return (
    <div className="rounded-2xl bg-gradient-to-br from-primary-600/50 to-border/20 p-px">
      <div className="flex items-start gap-3.5 rounded-2xl bg-card p-4 sm:p-[18px]">
        <span className="grid h-[38px] w-[38px] flex-none place-items-center rounded-xl bg-primary-soft text-primary-600">
          <Sparkles className="h-5 w-5" />
        </span>

        <div className="min-w-0 flex-1">
          <h3 className="mb-1.5 text-[13px] font-bold tracking-tight text-foreground">Super Consultor</h3>
          <Conteudo recurso={recurso} onDrill={onDrill} />
        </div>
      </div>
    </div>
  );
}

function Conteudo({ recurso, onDrill }: SuperConsultorSectionProps) {
  if (recurso.carregando) {
    return (
      <div className="space-y-2">
        <Skeleton className="h-4 w-full" />
        <Skeleton className="h-4 w-4/5" />
        <Skeleton className="mt-3 h-7 w-28" />
      </div>
    );
  }

  if (recurso.erro) {
    return <p className="text-[13.5px] leading-relaxed text-muted-foreground">{recurso.erro}</p>;
  }

  const insights = recurso.dado?.insights ?? [];
  if (insights.length === 0) {
    return (
      <p className="text-[13.5px] leading-relaxed text-muted-foreground">
        Sem análises no momento — assim que houver movimento de caixa, contas ou vendas registradas, o consultor volta a falar.
      </p>
    );
  }

  const [principal, ...secundarios] = insights;

  return (
    <>
      <InsightPrincipal insight={principal} onDrill={onDrill} />
      <InsightsSecundarios insights={secundarios} onDrill={onDrill} />
    </>
  );
}

/**
 * Os insights que não são a prioridade — colapsados por padrão atrás de "ver mais N →". O dono
 * quer "passar o que importa rápido": só a prioridade fica exposta de cara, o resto é opt-in.
 */
function InsightsSecundarios({ insights, onDrill }: { insights: InsightConsultor[]; onDrill: (target: DrillTarget) => void }) {
  const [aberto, setAberto] = useState(false);

  if (insights.length === 0) return null;

  return (
    <div className="mt-3 border-t border-border/60 pt-2.5">
      <button
        type="button"
        aria-expanded={aberto}
        onClick={() => setAberto((v) => !v)}
        className="inline-flex items-center gap-1 text-[12.5px] font-semibold text-primary-600 hover:underline"
      >
        {aberto ? 'Ver menos' : `Ver mais ${insights.length} →`}
        <ChevronDown className={cn('h-3.5 w-3.5 transition-transform', aberto && 'rotate-180')} />
      </button>

      <AnimatePresence initial={false}>
        {aberto && (
          <motion.div
            initial={{ height: 0, opacity: 0 }}
            animate={{ height: 'auto', opacity: 1 }}
            exit={{ height: 0, opacity: 0 }}
            transition={{ duration: 0.25, ease: [0, 0, 0.2, 1] }}
            className="overflow-hidden"
          >
            <div className="mt-2.5 space-y-2">
              {insights.map((insight) => (
                <InsightSecundario key={insight.id} insight={insight} onDrill={onDrill} />
              ))}
            </div>
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  );
}

function InsightPrincipal({ insight, onDrill }: { insight: InsightConsultor; onDrill: (target: DrillTarget) => void }) {
  const [aberto, setAberto] = useState(false);

  return (
    <div>
      <p className="text-[13.5px] leading-relaxed text-foreground">
        <b className="font-bold">Prioridade:</b> {insight.frase}
      </p>

      <div className="mt-3 flex flex-wrap items-center gap-4">
        {insight.drill && (
          <Button variant="primary" size="sm" onClick={() => onDrill(insight.drill!)}>
            Ver de onde vem →
          </Button>
        )}
        {insight.fatos.length > 0 && (
          <button
            type="button"
            aria-expanded={aberto}
            onClick={() => setAberto((v) => !v)}
            className="inline-flex items-center gap-1 text-[12.5px] font-semibold text-primary-600 hover:underline"
          >
            Ver como calculamos
            <ChevronDown className={cn('h-3.5 w-3.5 transition-transform', aberto && 'rotate-180')} />
          </button>
        )}
      </div>

      <AnimatePresence initial={false}>
        {aberto && insight.fatos.length > 0 && (
          <motion.div
            initial={{ height: 0, opacity: 0 }}
            animate={{ height: 'auto', opacity: 1 }}
            exit={{ height: 0, opacity: 0 }}
            transition={{ duration: 0.25, ease: [0, 0, 0.2, 1] }}
            className="overflow-hidden"
          >
            <div className="mt-3 space-y-1.5">
              {insight.fatos.map((fato) => (
                <div
                  key={fato.label}
                  className="flex items-center justify-between gap-3 rounded-[9px] bg-surface-2 px-2.5 py-[7px] text-[13px]"
                >
                  <span className="text-foreground">{fato.label}</span>
                  <span className="num font-bold text-foreground">{fato.valor}</span>
                </div>
              ))}
            </div>
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  );
}

function InsightSecundario({ insight, onDrill }: { insight: InsightConsultor; onDrill: (target: DrillTarget) => void }) {
  return (
    <p className="text-[13px] leading-relaxed text-muted-foreground">
      {insight.frase}
      {insight.drill && (
        <button
          type="button"
          onClick={() => onDrill(insight.drill!)}
          className="ml-1.5 whitespace-nowrap font-semibold text-primary-600 hover:underline"
        >
          ver →
        </button>
      )}
    </p>
  );
}
