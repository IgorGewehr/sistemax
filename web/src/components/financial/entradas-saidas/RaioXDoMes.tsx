import type { ReactNode } from 'react';

import { MockBadge, SectionCard } from '@/components/shared';

import { formatCentavosWhole } from './money';
import { MoneyValue } from './MoneyValue';
import type { Atrasados30DiasResumo, CategoriaDespesaResumo, CategoriaDrillStats, LiderAlta } from './types';

interface RaioXDoMesProps {
  categoriaSelecionada: CategoriaDespesaResumo | null;
  drillStats: CategoriaDrillStats | null;
  fixoPct: number;
  varPct: number;
  liderAlta: LiderAlta | null;
  atrasados30: Atrasados30DiasResumo;
  onClickAtrasados: () => void;
}

/** "Raio-X do mês" (`cardDir` do mockup) — fixo×variável, quem mais subiu, atrasados 30+ dias; ou o drill de uma categoria. */
export function RaioXDoMes({ categoriaSelecionada, drillStats, fixoPct, varPct, liderAlta, atrasados30, onClickAtrasados }: RaioXDoMesProps) {
  if (categoriaSelecionada && drillStats) {
    return (
      <SectionCard
        title={categoriaSelecionada.nome}
        hint="raio-x da categoria"
        actions={<MockBadge titulo="Histórico por categoria em 6 meses ainda não tem read-model no backend." />}
      >
        <div className="flex flex-col gap-2.5 px-4 pb-4 pt-1">
          <Stat k="Média histórica (5 meses)" v={<MoneyValue centavos={drillStats.avg5Centavos} />} s="antes deste mês" />
          <Stat
            k="Maior lançamento do mês"
            v={<MoneyValue centavos={categoriaSelecionada.maiorLancamento.valorCentavos} />}
            s={categoriaSelecionada.maiorLancamento.desc}
          />
          <Stat
            k="% do total do mês"
            v={
              <>
                {drillStats.pctDoTotal}
                <small className="ml-0.5 text-[13px] font-semibold text-muted-foreground">%</small>
              </>
            }
            s="de tudo que saiu em julho"
          />
        </div>
      </SectionCard>
    );
  }

  return (
    <SectionCard title="Raio-X do mês">
      <div className="flex flex-col gap-2.5 px-4 pb-4 pt-1">
        <div className="relative rounded-xl bg-surface-2 px-3.5 py-3">
          <MockBadge
            className="absolute right-2.5 top-2.5"
            titulo="Fixo × variável precisa da quebra por categoria em 6 meses — sem read-model ainda."
          />
          <div className="text-xs font-semibold text-muted-foreground">Fixo × variável</div>
          <div className="num mt-1 text-[23px] font-bold tracking-tight">
            {fixoPct}
            <small className="text-[13px] font-semibold text-muted-foreground">%</small>
            <span className="ml-1.5 font-sans text-sm font-semibold text-muted-foreground">
              {' '}
              · {varPct}
              <small className="text-[13px]">%</small>
            </span>
          </div>
          <div className="mt-2.5 flex h-[9px] overflow-hidden rounded-md border border-border">
            <div className="bg-primary-600" style={{ width: `${fixoPct}%` }} />
            <div className="bg-card" style={{ width: `${varPct}%` }} />
          </div>
          <div className="mt-0.5 text-xs text-faint">fixo · variável do mês</div>
        </div>

        <div className="relative rounded-xl bg-surface-2 px-3.5 py-3">
          <MockBadge
            className="absolute right-2.5 top-2.5"
            titulo="'Quem mais subiu' precisa da quebra por categoria em 6 meses — sem read-model ainda."
          />
          <div className="text-xs font-semibold text-muted-foreground">Quem mais subiu</div>
          {liderAlta ? (
            <div className="mt-1 text-[18px] font-bold tracking-tight">
              {liderAlta.categoria.nome} <span className="num text-warn">+{liderAlta.deltaPct}%</span>
            </div>
          ) : (
            <div className="mt-1 text-[18px] font-bold tracking-tight text-muted-foreground">—</div>
          )}
          <div className="mt-0.5 text-xs text-faint">vs a média dos últimos 5 meses</div>
        </div>

        <button
          type="button"
          onClick={onClickAtrasados}
          className="rounded-xl bg-surface-2 px-3.5 py-3 text-left transition-[filter] hover:brightness-[0.97] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring active:brightness-95 dark:hover:brightness-125"
        >
          <div className="text-xs font-semibold text-muted-foreground">Atrasados há mais de 30 dias</div>
          <div className="num mt-1 text-[23px] font-bold tracking-tight text-crit">
            {formatCentavosWhole(atrasados30.totalCentavos)}{' '}
            <span className="font-sans text-[13px] font-semibold text-muted-foreground">· {atrasados30.qtdClientes} clientes</span>
          </div>
          <div className="mt-0.5 text-xs text-faint">clique para ver na linha do tempo →</div>
        </button>
      </div>
    </SectionCard>
  );
}

function Stat({ k, v, s }: { k: string; v: ReactNode; s: string }) {
  return (
    <div className="rounded-xl bg-surface-2 px-3.5 py-3">
      <div className="text-xs font-semibold text-muted-foreground">{k}</div>
      <div className="num mt-1 text-[23px] font-bold tracking-tight">{v}</div>
      <div className="mt-0.5 text-xs text-faint">{s}</div>
    </div>
  );
}
