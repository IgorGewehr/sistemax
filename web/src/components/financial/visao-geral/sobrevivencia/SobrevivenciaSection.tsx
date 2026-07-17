import { AlertTriangle, Gauge, Percent, ShieldAlert } from 'lucide-react';
import type { ReactNode } from 'react';

import { Eyebrow, MoneyValue, SectionCard } from '@/components/shared';
import { EmptyState } from '@/components/ui/EmptyState';
import { Skeleton } from '@/components/ui/Skeleton';
import { Surface } from '@/components/ui/Surface';
import { formatPercent } from '@/lib/format';
import { cn } from '@/lib/utils';

import type { Recurso } from '../useVisaoGeral';
import type { BreakevenCardData, InadimplenciaCardData, RadarSimplesCardData, RunwayCardData } from './types';

interface SobrevivenciaSectionProps {
  runway: Recurso<RunwayCardData>;
  breakeven: Recurso<BreakevenCardData>;
  inadimplencia: Recurso<InadimplenciaCardData>;
  radarSimples: Recurso<RadarSimplesCardData>;
}

/**
 * Bloco "Sobrevivência" — não existe mockup próprio (é o 1º uso real do motor quant da F1). 4
 * cards, um por leitura: runway/bandas (`previsao-caixa`), ponto de equilíbrio, inadimplência e
 * Radar do Simples. Cada card trata LOADING/EMPTY/ERRO por conta própria — um endpoint fora do ar
 * não derruba os outros 3 (ver `useVisaoGeral`).
 */
export function SobrevivenciaSection({ runway, breakeven, inadimplencia, radarSimples }: SobrevivenciaSectionProps) {
  return (
    <SectionCard title="Sobrevivência" hint="o que o motor de risco calculou a partir do seu caixa">
      <div className="grid grid-cols-1 gap-3 px-3.5 pb-4 sm:grid-cols-2 sm:px-[18px] xl:grid-cols-4">
        <CardShell
          icone={<Gauge className="h-4 w-4" />}
          titulo="Runway"
          recurso={runway}
          vazio={(d) => d.diasRunwayRealista === null && d.diasRunwayBruto === null}
          tituloVazio="Sem histórico suficiente"
          descricaoVazio="Precisa de movimento de caixa registrado (a fórmula usa até 90 dias) para estimar quanto tempo o caixa aguenta."
        >
          {(d) => (
            <>
              <div className="text-2xl font-bold tracking-tight text-foreground">
                {d.diasRunwayRealista !== null ? `${d.diasRunwayRealista} dias` : '—'}
              </div>
              <div className="mt-1 text-xs text-muted-foreground">
                {d.diasRunwayBruto !== null ? `${d.diasRunwayBruto} dias sem margem de segurança` : 'sem estimativa bruta'}
              </div>
              <div className="mt-2.5 text-xs text-foreground">
                {formatPercent(d.probabilidadeSaldoNegativoEm30Dias * 100, 0)} de chance de caixa negativo em 30 dias
                {d.primeiroDiaP50NegativoLabel && (
                  <>
                    {' '}
                    · mediana cruza zero em <b className="font-bold">{d.primeiroDiaP50NegativoLabel}</b>
                  </>
                )}
              </div>
            </>
          )}
        </CardShell>

        <CardShell
          icone={<AlertTriangle className="h-4 w-4" />}
          titulo="Ponto de equilíbrio"
          recurso={breakeven}
          vazio={(d) => d.receitaNecessariaMensalCentavos === 0 && d.margemContribuicaoPercentual === 0}
          tituloVazio="Sem base para calcular"
          descricaoVazio="Sem recorrências (contas fixas) cadastradas nem vendas de produto na janela de 30 dias — cadastre as duas coisas para o breakeven aparecer aqui."
        >
          {(d) => (
            <>
              <div className="text-2xl font-bold tracking-tight text-foreground">
                <MoneyValue centavos={d.receitaNecessariaMensalCentavos} whole />
                <span className="ml-1 text-sm font-medium text-muted-foreground">/ mês</span>
              </div>
              <div className="mt-2 h-1.5 w-full overflow-hidden rounded-full bg-surface-2">
                <div
                  className={cn('h-full rounded-full', d.jaAtingiuNoMes ? 'bg-pos' : 'bg-primary-600')}
                  style={{ width: `${d.progressoPercentual}%` }}
                />
              </div>
              <div className="mt-1.5 text-xs text-muted-foreground">
                <MoneyValue centavos={d.receitaAcumuladaNoMesCentavos} whole /> já faturados ({d.progressoPercentual}%)
              </div>
              <div className="mt-1 text-xs text-foreground">
                {d.jaAtingiuNoMes
                  ? d.diaDoEquilibrio !== null
                    ? `Bateu no dia ${d.diaDoEquilibrio}`
                    : 'Já bateu este mês'
                  : 'Ainda não bateu este mês'}
              </div>
            </>
          )}
        </CardShell>

        <CardShell
          icone={<ShieldAlert className="h-4 w-4" />}
          titulo="Inadimplência"
          recurso={inadimplencia}
          vazio={(d) => d.valorTotalEmAbertoCentavos === 0}
          tituloVazio="Nenhuma conta a receber em aberto"
          descricaoVazio="Sem parcelas de recebíveis em aberto hoje — quando existirem, este card mostra quanto disso vale de verdade (descontada a perda esperada por atraso)."
          vazioTom="pos"
        >
          {(d) => (
            <>
              <div className="text-2xl font-bold tracking-tight text-foreground">
                <MoneyValue centavos={d.valorLiquidoEsperadoCentavos} whole />
              </div>
              <div className="mt-1 text-xs text-muted-foreground">
                líquido esperado de <MoneyValue centavos={d.valorTotalEmAbertoCentavos} whole /> em aberto
              </div>
              {d.porFaixa.length > 0 ? (
                <div className="mt-2.5 space-y-1">
                  {d.porFaixa.slice(0, 3).map((f) => (
                    <div key={f.label} className="flex items-center justify-between text-xs">
                      <span className="text-muted-foreground">
                        {f.label} · {f.quantidade}
                      </span>
                      <MoneyValue centavos={f.valorCentavos} whole className="font-semibold text-crit" />
                    </div>
                  ))}
                </div>
              ) : (
                <div className="mt-2.5 text-xs text-pos">tudo em dia</div>
              )}
            </>
          )}
        </CardShell>

        <CardShell
          icone={<Percent className="h-4 w-4" />}
          titulo="Radar do Simples"
          recurso={radarSimples}
          vazio={(d) => d.rbt12Centavos === 0}
          tituloVazio="Sem receita nos últimos 12 meses"
          descricaoVazio="RBT12 (receita bruta acumulada) ainda é zero — o radar de faixa/sublimite aparece assim que houver vendas registradas."
        >
          {(d) => (
            <>
              <div className="text-2xl font-bold tracking-tight text-foreground">Faixa {d.faixaAtual}</div>
              <div className="mt-1 text-xs text-muted-foreground">
                alíquota efetiva {formatPercent(d.aliquotaEfetiva * 100, 1)} · RBT12 <MoneyValue centavos={d.rbt12Centavos} whole />
              </div>
              <div className="mt-2.5 text-xs text-foreground">
                faltam <MoneyValue centavos={d.distanciaAoProximoDegrauCentavos} whole /> para o próximo degrau
                {d.mesesProjetadosAteOProximoDegrau !== null && ` · ~${d.mesesProjetadosAteOProximoDegrau} meses no ritmo atual`}
              </div>
            </>
          )}
        </CardShell>
      </div>
    </SectionCard>
  );
}

interface CardShellProps<T> {
  icone: ReactNode;
  titulo: string;
  recurso: Recurso<T>;
  vazio: (dado: T) => boolean;
  tituloVazio: string;
  descricaoVazio: string;
  /** Tom do estado vazio — `pos` quando "vazio" é uma notícia boa (ex.: zero inadimplência). */
  vazioTom?: 'neutro' | 'pos';
  children: (dado: T) => ReactNode;
}

function CardShell<T>({ icone, titulo, recurso, vazio, tituloVazio, descricaoVazio, vazioTom = 'neutro', children }: CardShellProps<T>) {
  return (
    <Surface padding="none" className="p-3.5 sm:p-4">
      <Eyebrow className="flex items-center gap-1.5">
        {icone}
        {titulo}
      </Eyebrow>

      <div className="mt-2.5">
        {recurso.carregando ? (
          <div className="space-y-2">
            <Skeleton className="h-7 w-24" />
            <Skeleton className="h-3 w-32" />
            <Skeleton className="h-3 w-28" />
          </div>
        ) : recurso.erro ? (
          <p className="text-xs text-crit">{recurso.erro}</p>
        ) : recurso.dado && vazio(recurso.dado) ? (
          <div className={cn('text-xs', vazioTom === 'pos' ? 'text-pos' : 'text-muted-foreground')}>
            <div className="font-semibold">{tituloVazio}</div>
            <p className="mt-1 leading-relaxed">{descricaoVazio}</p>
          </div>
        ) : recurso.dado ? (
          children(recurso.dado)
        ) : (
          <EmptyState icon={icone} title="Sem dado" description="Nada retornou do servidor." className="border-none py-2" />
        )}
      </div>
    </Surface>
  );
}
