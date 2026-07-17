import { MoveLeft } from 'lucide-react';

import { MoneyValue, SectionCard } from '@/components/shared';

import { atrasoDias, categoriaCorCss, type FornecedorRanking } from './calc';
import { CategoriaBarsChart } from './CategoriaBarsChart';
import type { CustoPorCategoria, Fornecedor } from './types';

interface FornecedorCategoriaDrillProps {
  ranking: FornecedorRanking;
  custoPorCategoria: CustoPorCategoria;
  scorecardFornecedor: Fornecedor | null;
  onSelecionarBarra: (id: string) => void;
  onVoltarScorecard: () => void;
  onVerTodosFornecedores: () => void;
  onVerPerfil: (id: string) => void;
}

/** Cores das 3 primeiras barras + "resto" — mesma paleta decorativa do mockup (marca → cinza decrescente). */
const CORES_RANKING = ['hsl(var(--primary))', 'hsl(var(--foreground) / 0.55)', 'hsl(var(--foreground) / 0.35)'];
const COR_RESTO = 'hsl(var(--foreground) / 0.18)';

/** Grid2 da Home: "Compras por fornecedor" (esquerda, sempre) × "Custo por categoria" ou scorecard (direita, alterna). */
export function FornecedorCategoriaDrill({
  ranking,
  custoPorCategoria,
  scorecardFornecedor,
  onSelecionarBarra,
  onVoltarScorecard,
  onVerTodosFornecedores,
  onVerPerfil,
}: FornecedorCategoriaDrillProps) {
  return (
    <section className="mb-4 grid grid-cols-1 gap-4 lg:grid-cols-[1.15fr_1fr]">
      <SectionCard title="Compras por fornecedor" hint="últimos 90 dias · clique p/ scorecard →">
        <div className="flex flex-col gap-1 px-3 pb-4 pt-1">
          {ranking.top3.map((barra, i) => (
            <button
              key={barra.fornecedor.id}
              type="button"
              onClick={() => onSelecionarBarra(barra.fornecedor.id)}
              className="flex w-full flex-col gap-1.5 rounded-[11px] px-2.5 py-2 text-left transition-colors hover:bg-surface-2 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring active:brightness-95"
            >
              <div className="flex items-center justify-between gap-2.5">
                <span className="inline-flex items-center text-[13px] font-semibold">
                  <span className="mr-2 h-2.5 w-2.5 flex-none rounded-[3px]" style={{ background: CORES_RANKING[i] }} />
                  {barra.fornecedor.nome}
                </span>
                <MoneyValue centavos={barra.fornecedor.comprado90dCentavos} className="text-[12.5px] font-normal text-muted-foreground" />
              </div>
              <div className="h-2 overflow-hidden rounded-md bg-surface-2">
                <div className="h-full rounded-md" style={{ width: `${barra.pct.toFixed(1)}%`, background: CORES_RANKING[i] }} />
              </div>
              <div className="flex justify-end">
                <span className="num text-[12.5px] font-bold">{barra.pct.toFixed(1).replace('.', ',')}%</span>
              </div>
            </button>
          ))}

          {ranking.restoCount > 0 && (
            <button
              type="button"
              onClick={onVerTodosFornecedores}
              className="flex w-full flex-col gap-1.5 rounded-[11px] px-2.5 py-2 text-left transition-colors hover:bg-surface-2 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring active:brightness-95"
            >
              <div className="flex items-center justify-between gap-2.5">
                <span className="inline-flex items-center text-[13px] font-semibold">
                  <span className="mr-2 h-2.5 w-2.5 flex-none rounded-[3px]" style={{ background: COR_RESTO }} />+{ranking.restoCount} fornecedores
                </span>
                <MoneyValue centavos={ranking.restoValorCentavos} className="text-[12.5px] font-normal text-muted-foreground" />
              </div>
              <div className="h-2 overflow-hidden rounded-md bg-surface-2">
                <div className="h-full rounded-md" style={{ width: `${ranking.restoPct.toFixed(1)}%`, background: COR_RESTO }} />
              </div>
              <div className="flex justify-end">
                <span className="num text-[12.5px] font-bold">{ranking.restoPct.toFixed(1).replace('.', ',')}%</span>
              </div>
            </button>
          )}
        </div>
      </SectionCard>

      {scorecardFornecedor ? (
        <FornecedorScorecard fornecedor={scorecardFornecedor} onVoltar={onVoltarScorecard} onVerPerfil={onVerPerfil} />
      ) : (
        <SectionCard title="Custo por categoria" hint="mês a mês">
          <div className="px-[18px] pb-1.5 pt-2.5">
            <CategoriaBarsChart data={custoPorCategoria} />
          </div>
          <div className="flex flex-wrap gap-4 px-[18px] pb-3.5 pt-1 text-xs text-muted-foreground">
            {custoPorCategoria.categorias.map((c) => (
              <span key={c.nome} className="inline-flex items-center gap-1.5">
                <i className="inline-block h-2.5 w-2.5 rounded-[3px]" style={{ background: categoriaCorCss(c.cor) }} />
                {c.nome}
              </span>
            ))}
          </div>
        </SectionCard>
      )}
    </section>
  );
}

interface FornecedorScorecardProps {
  fornecedor: Fornecedor;
  onVoltar: () => void;
  onVerPerfil: (id: string) => void;
}

/** Scorecard compacto do fornecedor selecionado (cardDir quando uma barra é clicada). */
function FornecedorScorecard({ fornecedor, onVoltar, onVerPerfil }: FornecedorScorecardProps) {
  const atraso = atrasoDias(fornecedor);
  const leadTimeReal = fornecedor.leadTimeRealDias.toFixed(1).replace('.', ',');

  return (
    <SectionCard
      title={
        <span className="inline-flex items-center gap-2">
          <button
            type="button"
            onClick={onVoltar}
            aria-label="Voltar"
            className="grid h-[26px] w-[26px] flex-none place-items-center rounded-lg bg-surface-2 text-foreground transition-colors hover:bg-primary-soft hover:text-primary-600 active:brightness-95"
          >
            <MoveLeft className="h-3.5 w-3.5" />
          </button>
          {fornecedor.nome}
        </span>
      }
      hint="scorecard"
    >
      <div className="flex flex-col gap-2.5 px-[18px] pb-4 pt-1">
        <div className="rounded-xl bg-surface-2 px-3.5 py-3">
          <div className="text-xs font-semibold text-muted-foreground">Comprado · 12 meses</div>
          <div className="num mt-1 text-lg font-bold">
            <MoneyValue centavos={fornecedor.comprado12mCentavos} />
          </div>
        </div>
        <div className="rounded-xl bg-surface-2 px-3.5 py-3">
          <div className="text-xs font-semibold text-muted-foreground">Lead time real × prometido</div>
          <div className={`num mt-1 text-lg font-bold ${atraso > 0 ? 'text-warn' : ''}`}>
            {leadTimeReal}d <small className="text-sm font-semibold text-muted-foreground">vs {fornecedor.leadTimePrometidoDias}d</small>
          </div>
          <div className="mt-0.5 text-xs text-faint">
            {atraso > 0 ? `▲ ${atraso.toFixed(1).replace('.', ',')} dias de atraso médio` : 'dentro do prometido'}
          </div>
        </div>
        <div className="rounded-xl bg-surface-2 px-3.5 py-3">
          <div className="text-xs font-semibold text-muted-foreground">Taxa de divergência</div>
          <div className={`num mt-1 text-lg font-bold ${fornecedor.divergNotas > 0 ? 'text-warn' : ''}`}>
            {fornecedor.divergNotas} <small className="text-sm font-semibold text-muted-foreground">de {fornecedor.divergTotal} notas</small>
          </div>
        </div>
        <button type="button" onClick={() => onVerPerfil(fornecedor.id)} className="mt-1 text-left text-[12.5px] font-bold text-primary-600 hover:underline">
          Ver perfil completo do fornecedor →
        </button>
      </div>
    </SectionCard>
  );
}
