import { ArrowLeft } from 'lucide-react';

import { SectionCard } from '@/components/shared';

import { categoriaCorCss } from './calc';
import { CategoriaColunas } from './CategoriaColunas';
import { MoneyValue } from './MoneyValue';
import type { CategoriaBarra, CategoriaDespesaId, CategoriaDespesaResumo, CategoriaDrillStats } from './types';

interface GastosPorCategoriaProps {
  barras: CategoriaBarra[];
  categoriaSelecionada: CategoriaDespesaResumo | null;
  drillStats: CategoriaDrillStats | null;
  meses: string[];
  onSelecionar: (id: CategoriaDespesaId | null) => void;
}

/** "Para onde foi o dinheiro" — barras por categoria; clique drilla pros últimos 6 meses da categoria (`cardEsq` do mockup). */
export function GastosPorCategoria({ barras, categoriaSelecionada, drillStats, meses, onSelecionar }: GastosPorCategoriaProps) {
  if (categoriaSelecionada && drillStats) {
    return (
      <SectionCard
        title={
          <span className="inline-flex items-center gap-2">
            <button
              type="button"
              onClick={() => onSelecionar(null)}
              aria-label="Voltar"
              className="grid h-[26px] w-[26px] flex-none place-items-center rounded-lg bg-surface-2 text-foreground transition-colors hover:bg-primary-soft hover:text-primary-600 active:brightness-95"
            >
              <ArrowLeft className="h-3.5 w-3.5" />
            </button>
            {categoriaSelecionada.nome}
          </span>
        }
        hint="6 meses · sua média em tracejado"
      >
        <div className="px-3.5 pb-1">
          <CategoriaColunas categoria={categoriaSelecionada} mediaCentavos={drillStats.avg5Centavos} anomalia={drillStats.isAnomalia} meses={meses} />
        </div>
        <div className="flex flex-wrap gap-4 px-[18px] py-4 text-xs text-muted-foreground">
          <span className="inline-flex items-center gap-1.5">
            <i className="inline-block h-2.5 w-3.5 rounded-[3px]" style={{ background: categoriaCorCss(categoriaSelecionada.cor) }} />
            Gasto do mês
          </span>
          <span className="inline-flex items-center gap-1.5">
            <i className="inline-block h-0 w-3.5 border-t-2 border-dashed border-muted-foreground" />
            Sua média (5m)
          </span>
        </div>
      </SectionCard>
    );
  }

  return (
    <SectionCard title="Para onde foi o dinheiro" hint="clique numa categoria p/ ver 6 meses →">
      <div className="flex flex-col gap-3 px-3.5 pb-4 pt-1">
        {barras.map((barra) => (
          <button
            key={barra.categoria.id}
            type="button"
            onClick={() => onSelecionar(barra.categoria.id)}
            className="flex w-full flex-col gap-1.5 rounded-[11px] px-2.5 py-2 text-left transition-colors hover:bg-surface-2 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring active:brightness-95"
          >
            <div className="flex items-center justify-between gap-2.5">
              <span className="inline-flex items-center text-[13px] font-semibold">
                <span className="mr-2 h-2.5 w-2.5 flex-none rounded-[3px]" style={{ background: categoriaCorCss(barra.categoria.cor) }} />
                {barra.categoria.nome}
                {barra.anomalia && (
                  <span className="ml-2 whitespace-nowrap rounded-md bg-warn-soft px-1.5 py-0.5 text-[10.5px] font-bold text-warn">
                    ▲ {barra.variacaoPct}%
                  </span>
                )}
              </span>
              <MoneyValue centavos={barra.categoria.totalCentavos} className="text-[12.5px] font-normal text-muted-foreground" />
            </div>
            <div className="h-2 overflow-hidden rounded-md bg-surface-2">
              <div className="h-full rounded-md" style={{ width: `${barra.widthPct}%`, background: categoriaCorCss(barra.categoria.cor) }} />
            </div>
            <div className="flex justify-end">
              <span className="num text-[12.5px] font-bold">{barra.pctDoTotal}%</span>
            </div>
          </button>
        ))}
      </div>
    </SectionCard>
  );
}
