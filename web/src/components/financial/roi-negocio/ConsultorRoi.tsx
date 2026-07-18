import { AnimatePresence, motion } from 'framer-motion';
import { ChevronDown, Sparkles } from 'lucide-react';
import { useState } from 'react';

import type { AtivoDeCapitalDto, RoiDoNegocioDto } from '@/lib/api/financeiro';
import { formatCentavosWhole } from '@/lib/money';
import { cn } from '@/lib/utils';

import { addMonthsIso, mesLabel } from './deriveChart';

interface ConsultorRoiProps {
  roi: RoiDoNegocioDto;
  bens: AtivoDeCapitalDto[];
}

/** Super Consultor do painel de ROI — Lei 2, só narra o que o `roi-negocio` real já calculou.
 * Mesma estrutura frasal do mockup (ritmo → payback → TIR → depreciação), sempre honesta quando
 * um número não existe (sem TIR definida, sem taxa de desconto, sem projeção de payback). */
export function ConsultorRoi({ roi, bens }: ConsultorRoiProps) {
  const [aberto, setAberto] = useState(false);
  const { investimento, recuperacao, payback, tir, roi: percentuais } = roi;

  const hojeCompetencia = roi.serie.length > 0 ? roi.serie[roi.serie.length - 1].competencia : roi.marcoInicial;
  const ritmoMensal =
    percentuais.mesesAteRoiCompleto && percentuais.mesesAteRoiCompleto > 0
      ? Math.round(recuperacao.faltamCentavos / percentuais.mesesAteRoiCompleto)
      : null;
  const depreciacaoMes = bens.reduce((acc, b) => acc + b.amortizacaoMensalCentavos, 0);
  const cobertura = depreciacaoMes > 0 && ritmoMensal ? ritmoMensal / depreciacaoMes : null;

  return (
    <div className="rounded-2xl bg-gradient-to-br from-primary-600/50 to-border/20 p-px">
      <div className="flex items-start gap-3.5 rounded-2xl bg-card p-4 sm:p-[18px]">
        <span className="grid h-[38px] w-[38px] flex-none place-items-center rounded-xl bg-primary-soft text-primary-600">
          <Sparkles className="h-5 w-5" />
        </span>

        <div className="min-w-0 flex-1">
          <div className="mb-1.5 flex items-center gap-2">
            <h3 className="text-[13px] font-bold tracking-tight text-foreground">Super Consultor</h3>
            <span className="rounded-full bg-surface-2 px-1.5 py-0.5 text-[10px] font-bold uppercase tracking-wide text-muted-foreground">
              somente leitura
            </span>
          </div>

          <p className="text-[13.5px] leading-relaxed text-foreground">
            {ritmoMensal !== null ? (
              <>
                No ritmo atual (<b className="num">+{formatCentavosWhole(ritmoMensal)}</b>/mês de caixa), o negócio recupera os{' '}
                <b>{formatCentavosWhole(investimento.totalCentavos)}</b> investidos em{' '}
                <span className="font-bold text-pos">~{percentuais.mesesAteRoiCompleto} meses</span> — o payback completo cai em{' '}
                <b>{mesLabel(addMonthsIso(hojeCompetencia, percentuais.mesesAteRoiCompleto!))}</b>.
              </>
            ) : payback.simplesRealizadoEm ? (
              <>
                O negócio já recuperou os <b>{formatCentavosWhole(investimento.totalCentavos)}</b> investidos — payback realizado em{' '}
                <b>{new Date(payback.simplesRealizadoEm).toLocaleDateString('pt-BR')}</b>.
              </>
            ) : (
              <>
                Faltam <b>{formatCentavosWhole(recuperacao.faltamCentavos)}</b> para recuperar os{' '}
                <b>{formatCentavosWhole(investimento.totalCentavos)}</b> investidos, mas o ritmo atual de caixa ainda não projeta uma data de
                cruzamento dentro de 120 meses.
              </>
            )}
            {tir.anualizadaPercent !== null && (
              <>
                {' '}
                A <b>TIR de {tir.anualizadaPercent.toFixed(1).replace('.', ',')}% a.a.</b>
                {roi.taxaDescontoAnualBps !== null ? (
                  <> supera {tir.anualizadaPercent >= roi.taxaDescontoAnualBps / 100 ? 'com folga' : 'abaixo de'} o custo de oportunidade de{' '}
                  {(roi.taxaDescontoAnualBps / 100).toFixed(1).replace('.', ',')}% a.a.</>
                ) : (
                  ' mede o retorno do fluxo do negócio.'
                )}
              </>
            )}
            {depreciacaoMes > 0 && (
              <>
                {' '}
                A depreciação come <span className="font-bold text-crit">{formatCentavosWhole(depreciacaoMes)}/mês</span> do resultado
                {cobertura !== null && cobertura > 0 && <>, mas o caixa operacional cobre isso quase {cobertura.toFixed(0)}×</>}.
              </>
            )}
          </p>

          <div className="mt-3 flex flex-wrap items-center gap-4">
            <button
              type="button"
              aria-expanded={aberto}
              onClick={() => setAberto((v) => !v)}
              className="inline-flex items-center gap-1 text-[12.5px] font-semibold text-primary-600 hover:underline"
            >
              Ver como calculamos
              <ChevronDown className={cn('h-3.5 w-3.5 transition-transform', aberto && 'rotate-180')} />
            </button>
          </div>

          <AnimatePresence initial={false}>
            {aberto && (
              <motion.div
                initial={{ height: 0, opacity: 0 }}
                animate={{ height: 'auto', opacity: 1 }}
                exit={{ height: 0, opacity: 0 }}
                transition={{ duration: 0.25, ease: [0, 0, 0.2, 1] }}
                className="overflow-hidden"
              >
                <div className="mt-3 space-y-1.5">
                  <CalcRow label="Total investido (bens + aportes)" valor={formatCentavosWhole(investimento.totalCentavos)} />
                  <CalcRow label="Já recuperado (aportes + caixa operacional)" valor={formatCentavosWhole(recuperacao.recuperadoCentavos)} tone="pos" />
                  <CalcRow label="Falta recuperar" valor={formatCentavosWhole(recuperacao.faltamCentavos)} tone="crit" />
                  {ritmoMensal !== null && (
                    <CalcRow
                      label={`Cruza zero em (${formatCentavosWhole(recuperacao.faltamCentavos)} ÷ ${formatCentavosWhole(ritmoMensal)}/mês)`}
                      valor={`~${percentuais.mesesAteRoiCompleto} meses`}
                    />
                  )}
                  <div className="px-0.5 pt-0.5 text-xs leading-relaxed text-muted-foreground">
                    O aporte não move a data do payback (ele entra dos dois lados e se cancela) — só muda o denominador do ROI%. Observação, não
                    instrução: o Consultor explica, nunca investe nem move dinheiro por você.
                  </div>
                </div>
              </motion.div>
            )}
          </AnimatePresence>
        </div>
      </div>
    </div>
  );
}

function CalcRow({ label, valor, tone }: { label: string; valor: string; tone?: 'pos' | 'crit' }) {
  return (
    <div className="flex items-center justify-between gap-3 rounded-[9px] bg-surface-2 px-2.5 py-[7px] text-[13px]">
      <span className="text-foreground">{label}</span>
      <span className={cn('num font-bold', tone === 'pos' && 'text-pos', tone === 'crit' && 'text-crit')}>{valor}</span>
    </div>
  );
}
