import { InfoTip, KpiCard, MoneyWhole } from '@/components/shared';
import type { RoiDoNegocioDto } from '@/lib/api/financeiro';
import { formatCentavosWhole } from '@/lib/money';

import { addMonthsIso, mesLabel } from './deriveChart';


interface KpisRoiProps {
  roi: RoiDoNegocioDto;
}

/** As 6 `.kpi` do topo do mockup — todas derivadas de `GET /financeiro/roi-negocio`, nenhum
 * número hardcoded. `mês N` é o índice absoluto na série (desde `marcoInicial`), igual ao mockup;
 * a data ("previsto · mmm/aaaa") vem de `hoje + projetadoMeses` (o backend conta a partir de
 * hoje, não do marco — ver `RoiDoNegocioService.CalcularAsync`). */
export function KpisRoi({ roi }: KpisRoiProps) {
  const { investimento, recuperacao, payback, tir, roi: percentuais } = roi;
  const hojeCompetencia = roi.serie.length > 0 ? roi.serie[roi.serie.length - 1].competencia : roi.marcoInicial;
  const indiceHoje = roi.serie.length > 0 ? roi.serie.length - 1 : 0;

  const ritmoMensal = percentuais.mesesAteRoiCompleto && percentuais.mesesAteRoiCompleto > 0
    ? Math.round(recuperacao.faltamCentavos / percentuais.mesesAteRoiCompleto)
    : null;

  return (
    <section className="mb-4 grid grid-cols-1 gap-3.5 sm:grid-cols-2 lg:grid-cols-3">
      <KpiCard
        hero
        label="Total investido"
        value={formatCentavosWhole(investimento.totalCentavos)}
        foot={
          <>
            <MoneyWhole centavos={investimento.capexCentavos} className="text-foreground" /> em bens +{' '}
            <MoneyWhole centavos={investimento.aportesCentavos} className="text-foreground" /> de aportes
          </>
        }
      />

      <KpiCard
        label={<>Payback simples <InfoTip text="Primeiro mês em que o caixa acumulado do negócio volta a ficar ≥ 0, tendo ficado negativo antes. Pergunta de caixa, por natureza." /></>}
        value={
          payback.simplesRealizadoEm ? (
            <span className="text-pos">realizado</span>
          ) : payback.projetadoMeses !== null ? (
            <>mês {indiceHoje + payback.projetadoMeses}</>
          ) : (
            '—'
          )
        }
        foot={
          payback.simplesRealizadoEm
            ? `em ${new Date(payback.simplesRealizadoEm).toLocaleDateString('pt-BR')}`
            : payback.projetadoMeses !== null
              ? `previsto · ${mesLabel(addMonthsIso(hojeCompetencia, payback.projetadoMeses))} · o buraco de ${formatCentavosWhole(recuperacao.faltamCentavos)} se fecha`
              : 'não cruza em 120 meses'
        }
      />

      <KpiCard
        label={<>Payback descontado <InfoTip text="Mesmo cruzamento, mas trazendo cada fluxo a valor presente pela taxa de desconto configurada. Sempre ≥ o simples." /></>}
        value={
          roi.taxaDescontoAnualBps === null ? (
            '—'
          ) : payback.descontadoRealizadoEm ? (
            <span className="text-pos">realizado</span>
          ) : payback.descontadoProjetadoMeses !== null ? (
            <>mês {indiceHoje + payback.descontadoProjetadoMeses}</>
          ) : (
            '—'
          )
        }
        foot={
          roi.taxaDescontoAnualBps === null
            ? 'sem taxa de desconto configurada'
            : `a ${(roi.taxaDescontoAnualBps / 100).toFixed(1).replace('.', ',')}% a.a.`
        }
      />

      <KpiCard
        label={<>TIR anualizada <InfoTip text="Taxa que zera o VPL do fluxo do negócio (bisseção). Acima do seu custo de oportunidade = o investimento vale a pena." /></>}
        value={
          tir.anualizadaPercent !== null ? (
            <>
              {tir.anualizadaPercent.toFixed(1).replace('.', ',')}
              <small className="text-[15px] font-semibold text-muted-foreground"> % a.a.</small>
            </>
          ) : (
            '—'
          )
        }
        foot={tir.mensalPercent !== null ? `${tir.mensalPercent.toFixed(1).replace('.', ',')}% ao mês` : (tir.motivoIndefinida ?? 'sem TIR definida')}
      />

      <KpiCard
        label="Faltam para o ROI completo"
        value={percentuais.mesesAteRoiCompleto !== null ? <>{percentuais.mesesAteRoiCompleto} <small className="text-[15px] font-semibold text-muted-foreground">meses</small></> : '—'}
        foot={
          recuperacao.faltamCentavos > 0
            ? `${formatCentavosWhole(recuperacao.faltamCentavos)} a recuperar${ritmoMensal ? ` · ritmo de ~${formatCentavosWhole(ritmoMensal)}/mês` : ''}`
            : 'já recuperado'
        }
      />

      <KpiCard
        label={<>ROI acumulado · caixa <InfoTip text="100 × acumulado líquido ÷ total investido. Negativo enquanto o negócio ainda escava o buraco do investimento inicial." /></>}
        value={
          <span className={percentuais.caixaPercent >= 0 ? 'text-pos' : 'text-warn'}>
            {percentuais.caixaPercent > 0 ? '+' : ''}
            {percentuais.caixaPercent.toFixed(0)}%
          </span>
        }
        foot={`já recuperou ${recuperacao.percentRecuperado.toFixed(0)}% do investido · ${formatCentavosWhole(recuperacao.recuperadoCentavos)} de ${formatCentavosWhole(investimento.totalCentavos)}`}
      />
    </section>
  );
}
