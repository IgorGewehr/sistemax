import { InfoTip, KpiCard, MoneyWhole } from '@/components/shared';
import type { PainelDoProjetoDto } from '@/lib/api/financeiro';
import { formatDate } from '@/lib/format';
import { formatCentavosWhole } from '@/lib/money';


interface KpisProjetoProps {
  painel: PainelDoProjetoDto;
}

/** As 4 `.kpi` do topo do mockup — generalizadas pro shape ÚNICO que `PainelDoProjetoService`
 * devolve pra QUALQUER projeto (o mockup tem 3 exemplos fixos com KPIs diferentes por natureza do
 * negócio; a tela real adapta o 3º card a capacidade/licenças quando existe, ou a custo direto
 * quando não existe — nunca inventa uma seção que o dado não sustenta). */
export function KpisProjeto({ painel }: KpisProjetoProps) {
  const { receita, margem, capacidade, payback } = painel;
  const temLicencas = capacidade.unidadesTotais > 0;
  const custoDiretoPercent = receita.mrr.centavos > 0 ? Math.round((margem.custoDireto.centavos / receita.mrr.centavos) * 1000) / 10 : 0;

  return (
    <section className="mb-4 grid grid-cols-1 gap-3.5 sm:grid-cols-2 lg:grid-cols-4">
      <KpiCard
        hero
        label="MRR · receita recorrente"
        value={
          <>
            {formatCentavosWhole(receita.mrr.centavos)}
            <small className="text-[15px] font-semibold text-muted-foreground">/mês</small>
          </>
        }
        foot={
          <>
            {receita.assinaturasAtivas} assinatura{receita.assinaturasAtivas === 1 ? '' : 's'} ativa
            {receita.assinaturasAtivas === 1 ? '' : 's'} · ticket <MoneyWhole centavos={receita.ticketMedio.centavos} className="text-foreground" />
          </>
        }
      />

      <KpiCard
        label={
          <>
            Margem de contribuição
            <InfoTip text="MC2 (cheia) = receita − custo direto tageado − amortização da capacidade comprada. É o que o projeto devolve depois de pagar tudo que é específico dele." />
          </>
        }
        value={
          <span className={margem.mc2.centavos >= 0 ? 'text-pos' : 'text-crit'}>
            {formatCentavosWhole(margem.mc2.centavos)}
            <small className="text-[15px] font-semibold text-muted-foreground">/mês</small>
          </span>
        }
        foot={`${margem.mc2Percent.toFixed(1).replace('.', ',')}% da receita`}
      />

      {temLicencas ? (
        <KpiCard
          label="Utilização das licenças"
          value={
            <>
              {capacidade.utilizacaoPercent.toFixed(0)}
              <small className="text-[15px] font-semibold text-muted-foreground">%</small>
            </>
          }
          foot={
            <>
              {capacidade.unidadesUtilizadas} de {capacidade.unidadesTotais} licenças em uso
              {capacidade.custoOciosidadeMesCentavos > 0 && (
                <>
                  {' '}
                  · ociosidade <MoneyWhole centavos={capacidade.custoOciosidadeMesCentavos} className="text-warn" />/mês
                </>
              )}
            </>
          }
        />
      ) : (
        <KpiCard
          label="Custo direto"
          value={<MoneyWhole centavos={margem.custoDireto.centavos} />}
          foot={`${custoDiretoPercent.toFixed(1).replace('.', ',')}% da receita`}
        />
      )}

      <KpiCard
        label={
          <>
            Payback
            <InfoTip text="Investimento total (compra de capacidade/ativos do projeto) ÷ margem gerada. 'Realizado' cruza o fluxo de caixa acumulado do projeto; 'projetado' é a simulação mês a mês." />
          </>
        }
        value={
          payback.paybackRealizadoEm ? (
            <span className="text-pos">realizado</span>
          ) : payback.paybackProjetadoMeses !== null ? (
            <>
              {payback.paybackProjetadoMeses}
              <small className="text-[15px] font-semibold text-muted-foreground"> meses</small>
            </>
          ) : (
            '—'
          )
        }
        foot={
          payback.paybackRealizadoEm
            ? `em ${formatDate(payback.paybackRealizadoEm)}`
            : payback.paybackProjetadoMeses !== null
              ? 'projeção mês a mês'
              : 'não cruza em 120 meses'
        }
      />
    </section>
  );
}
