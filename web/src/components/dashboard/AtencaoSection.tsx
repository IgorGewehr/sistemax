import { MoneyValue, SectionCard, StatusChip, type ChipTone } from '@/components/shared';

import type { DrillTarget, ItemAtencao, SeveridadeAtencao } from './types';

interface AtencaoSectionProps {
  itens: ItemAtencao[];
  onDrill: (target: DrillTarget) => void;
}

/** `falta`/`aberto` já carregam as cores crit/warn no `StatusChip` compartilhado — reaproveita em
 * vez de inventar tom novo; só o texto do chip (nome do módulo) muda por item. */
const CHIP_TONE: Record<SeveridadeAtencao, ChipTone> = {
  crit: 'falta',
  warn: 'aberto',
};

/**
 * "Precisa de atenção agora" (bloco ③) — um achado por módulo, o mais urgente primeiro. É a
 * segunda pergunta que o dono faz depois do número (bloco ②): "certo, e o que eu preciso resolver
 * hoje?". Lista vazia (todo mundo em dia) esconde a seção inteira em vez de mostrar um "tudo certo
 * por aqui" — o Dashboard só ocupa espaço com o que precisa de ação.
 */
export function AtencaoSection({ itens, onDrill }: AtencaoSectionProps) {
  if (itens.length === 0) return null;

  return (
    <SectionCard title="Precisa de atenção agora" hint="clique num item → o módulo de origem" bodyClassName="mt-0">
      <ul className="divide-y divide-border">
        {itens.map((item) => (
          <li key={`${item.modulo}-${item.titulo}`}>
            <button
              type="button"
              onClick={() => onDrill(item.drill)}
              className="flex w-full items-center justify-between gap-3 px-[18px] py-3 text-left transition-colors hover:bg-surface-2 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-inset active:brightness-95"
            >
              <div className="flex min-w-0 items-start gap-3">
                <StatusChip tone={CHIP_TONE[item.severidade]} className="mt-0.5 flex-none">
                  {item.moduloLabel}
                </StatusChip>
                <div className="min-w-0">
                  <div className="text-[13.5px] font-semibold text-foreground">{item.titulo}</div>
                  <div className="mt-0.5 truncate text-xs text-muted-foreground">{item.detalhe}</div>
                </div>
              </div>
              {item.valorCentavos !== undefined && (
                <MoneyValue centavos={item.valorCentavos} className="flex-none text-sm font-bold text-foreground" />
              )}
            </button>
          </li>
        ))}
      </ul>
    </SectionCard>
  );
}
