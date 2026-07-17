import { Sparkles } from 'lucide-react';
import { useState } from 'react';

import { BancarioMoneyValue } from './BancarioMoneyValue';
import type { ConsultorBancarioInsight } from './types';

interface SuperConsultorBancarioProps {
  insight: ConsultorBancarioInsight;
}

/** "3.1" → "3,1" — porcentagem com 1 casa decimal e vírgula (pt-BR), sem depender de Intl aqui. */
function formatPercentPtBr(value: number): string {
  return value.toFixed(1).replace('.', ',');
}

/**
 * Card do Super Consultor do Bancário — observa e explica, nunca age (Lei 2 do contrato).
 *
 * Não reaproveita o `ConsultorInsight` compartilhado porque este card precisa de um painel de
 * disclosure progressiva ("Ver por forma") aninhado dentro do mesmo cartão — o primitivo
 * compartilhado não expõe esse slot e não deve ser editado fora da sua tela. O visual replica o
 * mesmo vocabulário (ícone, cores, tipografia) do `ConsultorInsight` para ficar indistinguível.
 */
export function SuperConsultorBancario({ insight }: SuperConsultorBancarioProps) {
  const [aberto, setAberto] = useState(false);

  return (
    <div className="mb-4 rounded-2xl bg-gradient-to-br from-primary-600/40 to-border/20 p-px">
      <div className="flex items-start gap-3.5 rounded-2xl bg-card p-4 sm:p-[18px]">
        <span className="grid h-[38px] w-[38px] flex-none place-items-center rounded-xl bg-primary-soft text-primary-600">
          <Sparkles className="h-5 w-5" />
        </span>
        <div className="min-w-0 flex-1">
          <h3 className="mb-1.5 text-[13px] font-bold tracking-tight text-foreground">Super Consultor</h3>
          <p className="text-[13.5px] leading-relaxed text-foreground">
            As maquininhas levaram <BancarioMoneyValue centavos={insight.taxaTotalCentavos} className="font-bold text-crit" /> este
            mês — {formatPercentPtBr(insight.percentualVolume)}% de tudo que passou por elas. No crédito parcelado a taxa
            efetiva foi <b className="font-bold text-primary-600">{formatPercentPtBr(insight.taxaCreditoParceladoPct)}%</b>,
            quase o dobro do débito.
            <button
              type="button"
              onClick={() => setAberto((v) => !v)}
              className="ml-1.5 font-bold text-primary-600 hover:underline"
            >
              {aberto ? 'Ocultar ' : 'Ver por forma '}
              <span aria-hidden="true">{aberto ? '↑' : '→'}</span>
            </button>
          </p>

          {aberto && (
            <div className="mt-3 flex flex-col gap-1.5 rounded-xl bg-surface-2 p-3.5">
              {insight.porForma.map((linha) => (
                <div key={linha.forma} className="flex items-center justify-between gap-2.5 text-[12.5px]">
                  <span className="font-semibold text-foreground">{linha.forma}</span>
                  <span className="flex gap-4 text-muted-foreground">
                    <BancarioMoneyValue centavos={linha.valorCentavos} className="text-muted-foreground" />
                    <span className={linha.destaque ? 'num text-crit' : 'num'}>{linha.taxaLabel}</span>
                  </span>
                </div>
              ))}
              <div className="mt-0.5 flex items-center justify-between border-t border-border pt-2 text-[12.5px] font-bold">
                <span>Total em taxas no mês</span>
                <BancarioMoneyValue centavos={insight.taxaTotalCentavos} className="text-crit" />
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
