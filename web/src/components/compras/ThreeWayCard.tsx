import { Surface } from '@/components/ui/Surface';
import { formatCentavos } from '@/lib/money';

import { DeltaBadgeView } from './DeltaBadgeView';
import type { ItemNotaPedido, NotaEntradaPedido } from './types';

interface ThreeWayCardProps {
  nota: NotaEntradaPedido;
  readonly: boolean;
  onDivergenciaChange: (nItem: number, chave: string) => void;
  onFisicoChange: (nItem: number, valor: number | null) => void;
}

/** Conferência contra pedido (Pedido × Nota × Físico) — Tela 9.4, `renderThreeWay()` do mockup. */
export function ThreeWayCard({ nota, readonly, onDivergenciaChange, onFisicoChange }: ThreeWayCardProps) {
  const pendentes = nota.itens.filter((it) => it.divergencia && !it.divergenciaResolucao).length;

  return (
    <Surface padding="none" className="overflow-hidden">
      <div className="flex flex-wrap items-center justify-between gap-2 px-[18px] pt-3.5">
        <div className="text-[13px] font-bold">Pedido {nota.pedido.numero}</div>
        <div className="text-[12.5px] text-muted-foreground">
          enviado {nota.pedido.enviado} · previsto {nota.pedido.previsto}
        </div>
      </div>

      <div className="px-[18px] pb-1 pt-1">
        {nota.itens.map((it) => (
          <ThreeWayRow key={it.nItem} item={it} readonly={readonly} onDivergenciaChange={onDivergenciaChange} onFisicoChange={onFisicoChange} />
        ))}
      </div>

      <div className="flex flex-wrap items-center justify-between gap-2 border-t border-border px-[18px] py-3 text-[13px]">
        <span className={pendentes > 0 ? 'font-bold text-warn' : 'font-bold text-pos'}>
          {pendentes > 0 ? `${pendentes} divergência${pendentes > 1 ? 's' : ''} pendente${pendentes > 1 ? 's' : ''} — resolva para confirmar` : 'Todas as divergências resolvidas'}
        </span>
        <span>
          Pedido ficará: <b className="font-bold">{pendentes > 0 ? 'PARCIALMENTE RECEBIDO' : 'RECEBIDO'}</b>
        </span>
      </div>
    </Surface>
  );
}

interface ThreeWayRowProps {
  item: ItemNotaPedido;
  readonly: boolean;
  onDivergenciaChange: (nItem: number, chave: string) => void;
  onFisicoChange: (nItem: number, valor: number | null) => void;
}

function ThreeWayRow({ item, readonly, onDivergenciaChange, onFisicoChange }: ThreeWayRowProps) {
  const bateu = item.fisicoQtd != null && item.notaQtd != null ? item.fisicoQtd === item.notaQtd : null;

  return (
    <div className="border-b border-border/60 py-3.5 last:border-b-0">
      <div className="mb-1.5 text-[13.5px] font-bold">{item.nome}</div>
      <div className="grid grid-cols-2 items-center gap-2.5 text-[13px] lg:grid-cols-[1fr_1fr_1fr_auto]">
        <div>
          <div className="text-[10.5px] font-bold uppercase tracking-wide text-muted-foreground">Pedido</div>
          <div className="num font-semibold">
            {item.pedidoQtd} {item.unidade} × {formatCentavos(item.pedidoPrecoCentavos)}
          </div>
        </div>
        <div>
          <div className="text-[10.5px] font-bold uppercase tracking-wide text-muted-foreground">Nota</div>
          <div className={`num font-semibold ${item.notaQtd == null ? 'text-faint' : ''}`}>
            {item.notaQtd != null ? `${item.notaQtd} ${item.unidade} × ${formatCentavos(item.notaPrecoCentavos)}` : '—'}
          </div>
        </div>
        <div>
          <div className="text-[10.5px] font-bold uppercase tracking-wide text-muted-foreground">Físico</div>
          <div className="num font-semibold">
            {readonly || item.notaQtd == null ? (
              item.fisicoQtd != null ? (
                `${item.fisicoQtd} ${item.unidade}`
              ) : (
                '—'
              )
            ) : (
              <input
                type="number"
                defaultValue={item.fisicoQtd ?? ''}
                onChange={(e) => onFisicoChange(item.nItem, e.target.value === '' ? null : parseFloat(e.target.value))}
                className="w-[72px] rounded-lg border border-border px-1.5 py-1 text-[13px]"
              />
            )}
            {bateu === true && <span className="ml-1 text-base text-pos">✓</span>}
            {bateu === false && <span className="ml-1 text-base text-crit">✗</span>}
          </div>
        </div>
        <div className="text-right">{item.deltaPct ? <DeltaBadgeView pct={item.deltaPct} /> : null}</div>
      </div>

      {item.divergencia && (
        <div className={`mt-2.5 rounded-[11px] px-3 py-2.5 text-[12.5px] ${item.divergencia.severidade === 'w' ? 'border border-warn/25 bg-warn-soft' : 'border border-crit/25 bg-crit-soft'}`}>
          <div className={`mb-1.5 font-bold ${item.divergencia.severidade === 'w' ? 'text-warn' : 'text-crit'}`}>⚠ {item.divergencia.msg}</div>
          {!readonly ? (
            <div className="flex flex-wrap gap-3.5">
              {item.divergencia.opcoes.map((o) => (
                <label key={o.chave} className="inline-flex cursor-pointer items-center gap-1.5 font-medium">
                  <input
                    type="radio"
                    name={`div-${item.nItem}`}
                    value={o.chave}
                    checked={item.divergenciaResolucao === o.chave}
                    onChange={() => onDivergenciaChange(item.nItem, o.chave)}
                    className="accent-primary-600"
                  />
                  {o.label}
                </label>
              ))}
            </div>
          ) : (
            item.divergenciaResolucao && (
              <div className="mt-1.5 font-bold text-pos">✓ resolvida: {item.divergencia.opcoes.find((o) => o.chave === item.divergenciaResolucao)?.label}</div>
            )
          )}
        </div>
      )}
    </div>
  );
}
