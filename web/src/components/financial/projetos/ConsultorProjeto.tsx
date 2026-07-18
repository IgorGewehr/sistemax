import { AnimatePresence, motion } from 'framer-motion';
import { ChevronDown, Sparkles } from 'lucide-react';
import { useState } from 'react';

import type { PainelDoProjetoDto } from '@/lib/api/financeiro';
import { formatCentavosWhole } from '@/lib/money';
import { cn } from '@/lib/utils';


interface ConsultorProjetoProps {
  painel: PainelDoProjetoDto;
}

/**
 * Super Consultor do painel de Projeto — Lei 2 (só observa, nenhum CTA de ação; "somente
 * leitura" no badge). O mockup narra 3 exemplos fixos (DigiSat/Aevo/ServicePro) com texto
 * hardcoded; a tela real narra a PARTIR do painel de verdade — a mesma estrutura frasal
 * (payback → margem/ociosidade → ROI), sempre honesta sobre o que falta (payback não-projetado,
 * ROI sem base de investimento, etc.), nunca um número inventado.
 */
export function ConsultorProjeto({ painel }: ConsultorProjetoProps) {
  const [aberto, setAberto] = useState(false);
  const { projeto, margem, capacidade, payback, roi } = painel;

  const paybackFrase = payback.paybackRealizadoEm
    ? `já recuperou o investimento (realizado em ${new Date(payback.paybackRealizadoEm).toLocaleDateString('pt-BR')})`
    : payback.paybackProjetadoMeses !== null
      ? `recupera o investimento em ~${payback.paybackProjetadoMeses} meses no ritmo atual`
      : 'ainda não tem uma data de payback projetada dentro do horizonte de 120 meses';

  const temOciosidade = capacidade.unidadesTotais > 0 && capacidade.custoOciosidadeMesCentavos > 0;

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
            O <b>{projeto.nome}</b> {paybackFrase}. A margem cheia é de{' '}
            <b className={margem.mc2.centavos >= 0 ? 'text-pos font-bold' : 'text-crit font-bold'}>{formatCentavosWhole(margem.mc2.centavos)}</b>
            {' '}({margem.mc2Percent.toFixed(1).replace('.', ',')}% da receita) sobre {formatCentavosWhole(painel.receita.mrr.centavos)}/mês de MRR.
            {temOciosidade && (
              <>
                {' '}
                Hoje <b>
                  {capacidade.unidadesTotais - capacidade.unidadesUtilizadas} de {capacidade.unidadesTotais} unidades estão paradas
                </b>
                , custando <span className="font-bold text-crit">{formatCentavosWhole(capacidade.custoOciosidadeMesCentavos)}/mês</span> — cada
                unidade vendida a mais tende a ser margem quase pura.
              </>
            )}
            {roi.roiSobreInvestimentoPercent !== null && (
              <>
                {' '}
                O ROI acumulado sobre o investimento está em{' '}
                <b className={roi.roiSobreInvestimentoPercent >= 0 ? 'text-pos font-bold' : 'text-crit font-bold'}>
                  {roi.roiSobreInvestimentoPercent >= 0 ? '+' : ''}
                  {roi.roiSobreInvestimentoPercent.toFixed(1).replace('.', ',')}%
                </b>
                .
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
                  <CalcRow label="MRR (receita)" valor={formatCentavosWhole(painel.receita.mrr.centavos)} tone="pos" />
                  {margem.custoDireto.centavos > 0 && (
                    <CalcRow label="Custo direto" valor={`−${formatCentavosWhole(margem.custoDireto.centavos)}`} tone="crit" />
                  )}
                  {margem.amortizacaoMes.centavos > 0 && (
                    <CalcRow label="Amortização da capacidade" valor={`−${formatCentavosWhole(margem.amortizacaoMes.centavos)}`} tone="crit" />
                  )}
                  <CalcRow
                    label="MC2 = receita − custo direto − amortização"
                    valor={`${formatCentavosWhole(margem.mc2.centavos)} (${margem.mc2Percent.toFixed(1).replace('.', ',')}%)`}
                    tone="pos"
                  />
                  {temOciosidade && (
                    <CalcRow
                      label={`Ociosidade (amortização × ${(100 - capacidade.utilizacaoPercent).toFixed(0)}%)`}
                      valor={formatCentavosWhole(capacidade.custoOciosidadeMesCentavos)}
                      tone="crit"
                    />
                  )}
                  {payback.paybackProjetadoMeses !== null && !payback.paybackRealizadoEm && (
                    <CalcRow
                      label="Payback (investimento ÷ margem mensal projetada)"
                      valor={`~${payback.paybackProjetadoMeses} meses`}
                    />
                  )}
                  <div className="px-0.5 pt-0.5 text-xs leading-relaxed text-muted-foreground">
                    Observação, não instrução: o Consultor explica os números — nunca cobra, envia ou altera nada. A decisão é sua.
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
