import { MoneyValue } from '@/components/shared';
import { Surface } from '@/components/ui/Surface';

import { fatorSugeridoNumero, matchCounts, pendentesPadrao, resolvidosOuIgnorados } from './calc';
import { MatchDot, MatchTag } from './chips';
import { DeltaBadgeView } from './DeltaBadgeView';
import type { ItemNotaPadrao } from './types';

interface ItensPadraoCardProps {
  itens: ItemNotaPadrao[];
  readonly: boolean;
  onAcao: (nItem: number, acao: 'confirmar' | 'outro' | 'criar' | 'vincular' | 'ignorar') => void;
}

/** Card esquerdo da conferência padrão: itens pendentes (sugerido/sem match) primeiro, depois auto/ignorado. */
export function ItensPadraoCard({ itens, readonly, onAcao }: ItensPadraoCardProps) {
  const contagens = matchCounts(itens);
  const pendentes = pendentesPadrao(itens);
  const resolvidos = resolvidosOuIgnorados(itens);

  return (
    <Surface padding="none" className="overflow-hidden">
      <div className="flex flex-wrap items-center justify-between gap-2.5 px-[18px] pt-[15px]">
        <div className="text-[13px] font-bold">Itens ({itens.length})</div>
        <div className="flex flex-wrap gap-2.5 text-xs text-muted-foreground">
          <span className="inline-flex items-center gap-1">
            <span className="h-[9px] w-[9px] rounded-full bg-pos" /> {contagens.auto} auto
          </span>
          <span className="inline-flex items-center gap-1">
            <span className="h-[9px] w-[9px] rounded-full bg-warn" /> {contagens.sugerido} sugerido
          </span>
          <span className="inline-flex items-center gap-1">
            <span className="h-[9px] w-[9px] rounded-full bg-crit" /> {contagens.semmatch} sem match
          </span>
        </div>
      </div>

      <div className="mt-1.5">
        {pendentes.map((it) => (
          <ItemPendente key={it.nItem} item={it} readonly={readonly} onAcao={(acao) => onAcao(it.nItem, acao)} />
        ))}
        {resolvidos.map((it) => (
          <ItemResolvido key={it.nItem} item={it} />
        ))}
      </div>
    </Surface>
  );
}

function ItemPendente({ item, readonly, onAcao }: { item: ItemNotaPadrao; readonly: boolean; onAcao: (acao: 'confirmar' | 'outro' | 'criar' | 'vincular' | 'ignorar') => void }) {
  if (item.match === 'sugerido') {
    return (
      <div className="border-b border-border/60 px-[18px] py-2.5 last:border-b-0">
        <div className="flex items-start gap-2.5">
          <MatchDot tone="warn" />
          <div className="min-w-0 flex-1">
            <div className="text-[13.5px] font-semibold">
              {item.nome}
              <MatchTag tone="warn">sugerido</MatchTag>
            </div>
            <div className="mt-0.5 text-xs text-muted-foreground">NF: {item.nf}</div>
            {!readonly && (
              <>
                <div className="mt-2 rounded-[10px] bg-warn-soft px-2.5 py-2 text-[12.5px]">
                  sugerido: <b className="font-bold text-warn">{item.sugestao}</b>
                </div>
                <div className="mt-2 flex flex-wrap items-center gap-2">
                  <button type="button" onClick={() => onAcao('confirmar')} className="rounded-lg border border-pos bg-pos-soft px-2.5 py-1 text-xs font-semibold text-pos hover:brightness-95">
                    ✓ confirmar
                  </button>
                  <button type="button" onClick={() => onAcao('outro')} className="rounded-lg border border-border bg-card px-2.5 py-1 text-xs font-semibold hover:bg-surface-2">
                    outro produto
                  </button>
                  <button type="button" onClick={() => onAcao('criar')} className="rounded-lg border border-border bg-card px-2.5 py-1 text-xs font-semibold hover:bg-surface-2">
                    criar produto
                  </button>
                </div>
                <div className="mt-1.5 text-xs text-muted-foreground">
                  fator: 1 CX = <input type="text" defaultValue={fatorSugeridoNumero(item.fatorSugerido)} className="w-16 rounded-md border border-border px-1.5 py-0.5 text-xs" /> kg
                </div>
              </>
            )}
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="border-b border-border/60 px-[18px] py-2.5 last:border-b-0">
      <div className="flex items-start gap-2.5">
        <MatchDot tone="crit" />
        <div className="min-w-0 flex-1">
          <div className="text-[13.5px] font-semibold">
            {item.nome}
            <MatchTag tone="crit">sem match</MatchTag>
          </div>
          <div className="mt-0.5 text-xs text-muted-foreground">NF: {item.nf}</div>
          {!readonly && (
            <div className="mt-2 flex flex-wrap items-center gap-2">
              <button type="button" onClick={() => onAcao('vincular')} className="rounded-lg border border-pos bg-pos-soft px-2.5 py-1 text-xs font-semibold text-pos hover:brightness-95">
                vincular produto
              </button>
              <button type="button" onClick={() => onAcao('criar')} className="rounded-lg border border-border bg-card px-2.5 py-1 text-xs font-semibold hover:bg-surface-2">
                criar produto
              </button>
              <button type="button" onClick={() => onAcao('ignorar')} className="rounded-lg border border-border bg-card px-2.5 py-1 text-xs font-semibold hover:bg-surface-2">
                ignorar
              </button>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

function ItemResolvido({ item }: { item: ItemNotaPadrao }) {
  const ignorado = item.match === 'ignorado';
  return (
    <div className={`border-b border-border/60 px-[18px] py-2.5 last:border-b-0 ${ignorado ? 'opacity-55' : ''}`}>
      <div className="flex items-start gap-2.5">
        <MatchDot tone={ignorado ? 'faint' : 'pos'} />
        <div className="min-w-0 flex-1">
          <div className="text-[13.5px] font-semibold">
            {item.nome}
            <MatchTag tone={ignorado ? 'faint' : 'pos'}>{ignorado ? 'ignorado' : 'auto'}</MatchTag>
          </div>
          <div className="mt-0.5 text-xs text-muted-foreground">NF: {item.nf}</div>
          {!ignorado && (
            <div className="mt-0.5 flex items-baseline gap-2 text-[12.5px]">
              <span className="num font-bold">{item.custoUnitCentavos != null ? <MoneyValue centavos={item.custoUnitCentavos} /> : '—'}/{item.unidade}</span>
              <DeltaBadgeView pct={item.deltaPct} />
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
