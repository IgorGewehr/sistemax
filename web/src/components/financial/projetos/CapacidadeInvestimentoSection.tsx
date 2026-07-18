import { Check, Circle } from 'lucide-react';

import { SectionCard } from '@/components/shared';
import type { PainelDoProjetoDto } from '@/lib/api/financeiro';
import { formatCentavosWhole } from '@/lib/money';
import { cn } from '@/lib/utils';


interface CapacidadeInvestimentoSectionProps {
  painel: PainelDoProjetoDto;
}

/**
 * `.grid2` (Capacidade | Investimento & amortização) do mockup DigiSat. Só existe quando o
 * projeto tem capacidade/ativo de fato — projetos sem licença/ativo de capital (Aevo, ServicePro)
 * pulam direto pra "Margem em camadas". A grade de unidades é AGREGADA (usadas primeiro, ociosas
 * depois): o painel não devolve qual cliente ocupa qual licença, só o total usado/total — mostrar
 * nomes fictícios de cliente aqui violaria a mesma honestidade que o backend já pratica no LTV/MC3.
 */
export function CapacidadeInvestimentoSection({ painel }: CapacidadeInvestimentoSectionProps) {
  const { capacidade, payback, margem } = painel;
  const temLicencas = capacidade.unidadesTotais > 0;
  const temInvestimento = payback.investimentoTotalCentavos > 0;
  if (!temLicencas && !temInvestimento) return null;

  const custoPorUnidade = temLicencas ? Math.round(payback.investimentoTotalCentavos / capacidade.unidadesTotais) : null;

  return (
    <section className="mb-4 grid grid-cols-1 items-stretch gap-4 lg:grid-cols-2">
      {temLicencas ? (
        <SectionCard
          title={`Capacidade — ${capacidade.unidadesTotais} unidade${capacidade.unidadesTotais === 1 ? '' : 's'}`}
          hint={`${capacidade.unidadesUtilizadas} em uso · ${capacidade.unidadesTotais - capacidade.unidadesUtilizadas} paradas`}
        >
          <div className="grid grid-cols-3 gap-2.5 px-[18px] pb-1 sm:grid-cols-5">
            {Array.from({ length: capacidade.unidadesTotais }, (_, i) => i < capacidade.unidadesUtilizadas).map((usada, i) => (
              <div
                key={i}
                className={cn(
                  'rounded-xl border-[1.5px] px-2 py-3.5 text-center',
                  usada ? 'border-pos/50 bg-pos-soft' : 'border-dashed border-border bg-surface-2',
                )}
              >
                <div
                  className={cn(
                    'mx-auto mb-1.5 grid h-[26px] w-[26px] place-items-center rounded-lg',
                    usada ? 'bg-pos/15 text-pos' : 'bg-card text-faint',
                  )}
                >
                  {usada ? <Check className="h-[15px] w-[15px]" /> : <Circle className="h-[15px] w-[15px]" />}
                </div>
                <div className={cn('text-[11px] font-bold', usada ? 'text-pos' : 'text-muted-foreground')}>
                  {usada ? 'Em uso' : 'Ociosa'}
                </div>
              </div>
            ))}
          </div>

          <div className="flex flex-wrap items-center gap-3.5 px-[18px] pb-1 pt-3.5">
            <span className="num text-[12.5px] font-semibold text-foreground">{capacidade.utilizacaoPercent.toFixed(0)}%</span>
            <div className="h-2.5 min-w-[120px] flex-1 overflow-hidden rounded-full bg-surface-2">
              <div className="h-full rounded-full bg-pos" style={{ width: `${Math.min(100, capacidade.utilizacaoPercent)}%` }} />
            </div>
            {capacidade.custoOciosidadeMesCentavos > 0 && (
              <span className="num text-[12.5px] font-bold text-warn">ociosidade {formatCentavosWhole(capacidade.custoOciosidadeMesCentavos)}/mês</span>
            )}
          </div>

          <p className="flex gap-2 px-[18px] pb-4 pt-3 text-xs leading-relaxed text-muted-foreground">
            <span>
              A amortização corre sobre as <b className="text-foreground">{capacidade.unidadesTotais} unidades</b>, use-as ou não — unidade
              parada também queima dinheiro. A ociosidade é exibida, nunca abatida.
            </span>
          </p>
        </SectionCard>
      ) : (
        <div className="hidden lg:block" />
      )}

      {temInvestimento && (
        <SectionCard title="Investimento & amortização" hint="caixa ≠ competência">
          <div className="flex flex-col gap-2.5 px-[18px] pb-4">
            <div className="rounded-xl bg-surface-2 px-3.5 py-3">
              <div className="text-xs font-semibold text-muted-foreground">Investimento — trilho de caixa</div>
              <div className="num mt-1 text-xl font-bold tracking-tight text-foreground">{formatCentavosWhole(payback.investimentoTotalCentavos)}</div>
            </div>
            <div className="rounded-xl bg-surface-2 px-3.5 py-3">
              <div className="text-xs font-semibold text-muted-foreground">Amortização — trilho de competência</div>
              <div className="num mt-1 text-xl font-bold tracking-tight text-foreground">
                {formatCentavosWhole(margem.amortizacaoMes.centavos)}
                <small className="text-[13px] font-semibold text-muted-foreground">/mês</small>
              </div>
            </div>
            {custoPorUnidade !== null && (
              <div className="rounded-xl bg-surface-2 px-3.5 py-3">
                <div className="text-xs font-semibold text-muted-foreground">Custo por unidade</div>
                <div className="num mt-1 text-xl font-bold tracking-tight text-foreground">{formatCentavosWhole(custoPorUnidade)}</div>
              </div>
            )}
          </div>
          <p className="flex gap-2 px-[18px] pb-4 text-xs leading-relaxed text-muted-foreground">
            <span>
              Comprar capacidade é <b className="text-foreground">troca de ativo</b>: a despesa nasce mês a mês com o uso, não de uma vez na
              compra.
            </span>
          </p>
        </SectionCard>
      )}
    </section>
  );
}
