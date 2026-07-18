import { InfoTip, SectionCard } from '@/components/shared';
import type { PainelDoProjetoDto } from '@/lib/api/financeiro';
import { formatCentavosWhole } from '@/lib/money';
import { cn } from '@/lib/utils';


interface MargemLtvSectionProps {
  painel: PainelDoProjetoDto;
}

function DecompRow({
  label,
  sub,
  valueCentavos,
  tone,
  total,
  pctLabel,
}: {
  label: string;
  sub?: string;
  valueCentavos: number;
  tone?: 'pos' | 'crit';
  total?: boolean;
  pctLabel?: string;
}) {
  return (
    <div
      className={cn(
        'grid grid-cols-[1fr_auto] items-center gap-x-3.5 gap-y-1 rounded-[11px] px-2.5 py-2.5',
        total ? 'mt-1 border-t border-border bg-pos-soft/50 pt-3.5' : 'border-t border-dashed border-border first:border-t-0',
      )}
    >
      <span className={cn('text-[13.5px]', total ? 'font-bold text-foreground' : 'font-medium text-foreground')}>
        {label} {sub && <span className="font-normal text-muted-foreground">{sub}</span>}
      </span>
      <span className={cn('num text-right font-bold', total ? 'text-lg' : 'text-[15px]', tone === 'pos' && 'text-pos', tone === 'crit' && 'text-crit')}>
        {formatCentavosWhole(valueCentavos)}
        {pctLabel && <span className="block text-[11.5px] font-semibold text-muted-foreground">{pctLabel}</span>}
      </span>
    </div>
  );
}

/** `.grid2` (Margem em camadas | Retenção/LTV/ROI) do mockup — sempre presente, é o coração do
 * painel v1 (design §9.3/§9.4/§9.5). MC3 só aparece quando o backend realmente calculou (nunca
 * `0` disfarçado de "tempo grátis" — mesma honestidade do domínio). */
export function MargemLtvSection({ painel }: MargemLtvSectionProps) {
  const { margem, churn, ltv, roi, tempo } = painel;
  const semCustoVariavel = margem.custoDireto.centavos === 0;

  return (
    <section className="mb-4 grid grid-cols-1 items-stretch gap-4 lg:grid-cols-2">
      <SectionCard title="Margem em camadas" hint="receita → custo específico → resultado" bodyClassName="px-2 pb-2">
        <div className="px-2.5">
          <DecompRow
            label="Receita do mês"
            sub={`(competência · ${painel.receita.assinaturasAtivas} assinatura${painel.receita.assinaturasAtivas === 1 ? '' : 's'} ativa${painel.receita.assinaturasAtivas === 1 ? '' : 's'})`}
            valueCentavos={margem.receita.centavos}
          />
          <DecompRow
            label="MC1 · variável"
            sub={semCustoVariavel ? 'sem custo variável' : 'receita − custo direto'}
            valueCentavos={margem.mc1.centavos}
            tone="pos"
          />
          {margem.custoDireto.centavos > 0 && (
            <DecompRow label="(−) Custo direto" sub="tageado ao projeto" valueCentavos={-margem.custoDireto.centavos} tone="crit" />
          )}
          {margem.amortizacaoMes.centavos > 0 && (
            <DecompRow label="(−) Amortização" sub="capacidade comprada" valueCentavos={-margem.amortizacaoMes.centavos} tone="crit" />
          )}
          <DecompRow
            label="MC2 · margem cheia"
            valueCentavos={margem.mc2.centavos}
            tone="pos"
            total
            pctLabel={`${margem.mc2Percent.toFixed(1).replace('.', ',')}% da receita`}
          />
          {margem.mc3 !== null && (
            <DecompRow
              label="MC3 · gerencial"
              sub="após custo de tempo"
              valueCentavos={margem.mc3.centavos}
              tone={margem.mc3.centavos >= 0 ? 'pos' : 'crit'}
              total
              pctLabel={margem.mc3Percent !== null ? `${margem.mc3Percent.toFixed(1).replace('.', ',')}% da receita` : undefined}
            />
          )}
        </div>
        <p className="flex gap-2 px-4 pb-4 pt-2 text-xs leading-relaxed text-muted-foreground">
          <span>
            {margem.mc3 !== null ? (
              <>
                MC3 já soma <b className="text-foreground">{tempo.minutosJanela} minutos</b> de tempo lançado nesta janela.
              </>
            ) : (
              <>
                MC3 (gerencial) não aparece:{' '}
                {tempo.minutosJanela > 0 ? (
                  <>
                    há <b className="text-foreground">{tempo.minutosJanela} minutos</b> de tempo lançado, mas custo/hora não está configurado.
                  </>
                ) : (
                  <>
                    <b className="text-foreground">nenhum tempo foi lançado</b> nesta janela.
                  </>
                )}{' '}
                Custo/hora é opcional — o dono decide sem ele por ora.
              </>
            )}
          </span>
        </p>
      </SectionCard>

      <SectionCard title="Retenção, LTV & ROI" hint="honesto quando o dado não existe">
        <div className="flex flex-col gap-2.5 px-[18px] pb-4">
          <div className="rounded-xl bg-surface-2 px-3.5 py-3">
            <div className="text-xs font-semibold text-muted-foreground">Churn na janela</div>
            <div className="num mt-1 text-xl font-bold tracking-tight text-foreground">
              {churn.churnMensalPercent.toFixed(1).replace('.', ',')}
              <small className="text-[13px] font-semibold text-muted-foreground">%</small>
            </div>
            <div className="mt-0.5 text-xs text-faint">
              {churn.cancelamentos12m} cancelamento{churn.cancelamentos12m === 1 ? '' : 's'} · exposição{' '}
              {churn.exposicaoAssinaturaMeses12m.toFixed(1).replace('.', ',')} meses-assinatura
            </div>
          </div>

          <div className="rounded-xl bg-surface-2 px-3.5 py-3">
            <div className="flex items-center text-xs font-semibold text-muted-foreground">
              LTV estimado
              <InfoTip text={ltv.observacao ?? 'LTV = margem de contribuição por assinatura × vida esperada (1/churn).'} />
            </div>
            {ltv.ltv !== null ? (
              <div className="num mt-1 text-xl font-bold tracking-tight text-foreground">{formatCentavosWhole(ltv.ltv.centavos)}</div>
            ) : (
              <div className="mt-1 text-xl font-bold tracking-tight text-foreground">
                indefinido <small className="num text-[13px] font-semibold text-muted-foreground">· piso {formatCentavosWhole(ltv.limiteInferior.centavos)}</small>
              </div>
            )}
            <div className="mt-0.5 text-xs text-faint">{ltv.ltv !== null ? 'com base no churn observado' : 'churn 0 → o LTV já é ≥ margem acumulada'}</div>
          </div>

          <div className={cn('rounded-xl px-3.5 py-3', roi.roiSobreInvestimentoPercent !== null && roi.roiSobreInvestimentoPercent >= 0 ? 'bg-pos-soft/50' : 'bg-surface-2')}>
            <div className="text-xs font-semibold text-muted-foreground">ROI sobre investimento</div>
            {roi.roiSobreInvestimentoPercent !== null ? (
              <div className={cn('num mt-1 text-xl font-bold tracking-tight', roi.roiSobreInvestimentoPercent >= 0 ? 'text-pos' : 'text-crit')}>
                {roi.roiSobreInvestimentoPercent >= 0 ? '+' : ''}
                {roi.roiSobreInvestimentoPercent.toFixed(1).replace('.', ',')}%
              </div>
            ) : (
              <div className="mt-1 text-xl font-bold tracking-tight text-foreground">—</div>
            )}
            <div className="mt-0.5 text-xs text-faint">
              {roi.roiSobreInvestimentoPercent !== null
                ? 'receita acumulada − custo econômico, sobre o investimento'
                : 'sem investimento registrado neste projeto'}
            </div>
          </div>
        </div>
      </SectionCard>
    </section>
  );
}
