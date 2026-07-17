import { Search } from 'lucide-react';

import { MoneyValue } from '@/components/shared';
import { Surface } from '@/components/ui/Surface';
import { formatDateShort } from '@/lib/format';
import { cn } from '@/lib/utils';

import { addDias, atrasada, centavosOuTraco, diaSemana, orcamentoVencido, valorAtual } from './calc';
import { OsStatusChip } from './OsStatusChip';
import type { AcaoPrimaria, FiltroFila, OrdemServico } from './types';

interface FilaSectionProps {
  filtroFila: FiltroFila;
  onFiltroChange: (f: FiltroFila) => void;
  buscaFila: string;
  onBuscaChange: (v: string) => void;
  itens: OrdemServico[];
  totalCount: number;
  ativasCount: number;
  encerradasCount: number;
  acaoPrimaria: (os: OrdemServico) => AcaoPrimaria | null;
  onRowClick: (numero: string) => void;
}

const SEG: { key: FiltroFila; label: string }[] = [
  { key: 'ativas', label: 'Ativas' },
  { key: 'todas', label: 'Todas' },
  { key: 'encerradas', label: 'Encerradas' },
];

/** Fila (tabela) da lista — segmentado (ativas/todas/encerradas) + busca + a tabela em si. */
export function FilaSection({
  filtroFila,
  onFiltroChange,
  buscaFila,
  onBuscaChange,
  itens,
  totalCount,
  ativasCount,
  encerradasCount,
  acaoPrimaria,
  onRowClick,
}: FilaSectionProps) {
  const contagem: Record<FiltroFila, number> = { ativas: ativasCount, todas: totalCount, encerradas: encerradasCount };

  return (
    <>
      <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
        <div className="inline-flex items-center gap-0.5 rounded-[11px] border border-border bg-surface-2 p-[3px]">
          {SEG.map((s) => (
            <button
              key={s.key}
              type="button"
              onClick={() => onFiltroChange(s.key)}
              className={cn(
                'rounded-lg px-3.5 py-1.5 text-[13px] font-semibold transition-colors active:brightness-95',
                filtroFila === s.key ? 'bg-card text-foreground shadow-sm' : 'text-muted-foreground hover:text-foreground',
              )}
            >
              {s.label} <span className="opacity-60">· {contagem[s.key]}</span>
            </button>
          ))}
        </div>

        <div className="flex items-center gap-1.5 rounded-[10px] border border-border bg-card px-3 py-2 text-faint">
          <Search className="h-3.5 w-3.5" />
          <input
            type="text"
            value={buscaFila}
            onChange={(e) => onBuscaChange(e.target.value)}
            placeholder="Buscar OS, cliente, equipamento…"
            className="w-40 bg-transparent text-sm text-foreground outline-none placeholder:text-faint"
          />
        </div>
      </div>

      <Surface padding="none" className="overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full min-w-[640px] border-collapse text-left">
            <thead>
              <tr>
                {['OS', 'Cliente', 'Equipamento', 'Status', 'Valor', 'Prazo', 'Ação'].map((col, i) => (
                  <th
                    key={col}
                    className={cn(
                      'border-b border-border px-4 py-3 text-[11px] font-semibold uppercase tracking-wide text-muted-foreground',
                      i >= 4 && 'text-right',
                    )}
                  >
                    {col}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {itens.length === 0 && (
                <tr>
                  <td colSpan={7} className="p-7 text-center text-muted-foreground">
                    Nenhuma OS encontrada.
                  </td>
                </tr>
              )}
              {itens.map((o) => (
                <FilaRow key={o.numero} os={o} acao={acaoPrimaria(o)} onRowClick={onRowClick} />
              ))}
            </tbody>
          </table>
        </div>
      </Surface>
    </>
  );
}

function FilaRow({ os, acao, onRowClick }: { os: OrdemServico; acao: AcaoPrimaria | null; onRowClick: (numero: string) => void }) {
  const cancelada = os.status === 'Cancelada';
  const prazoAtrasado = atrasada(os);
  const cellBase = 'border-b border-border/60 px-4 py-3.5 align-top text-[13.5px]';
  const strike = cancelada ? 'text-faint line-through decoration-faint/50' : '';

  return (
    <tr className="cursor-pointer transition-colors hover:bg-surface-2/60" onClick={() => onRowClick(os.numero)}>
      <td className={cn(cellBase, strike)}>
        <b className="num">{os.numero}</b>
      </td>
      <td className={cn(cellBase, strike)}>
        {os.cliente}
        <div className="text-xs text-muted-foreground">{os.telefone}</div>
      </td>
      <td className={cn(cellBase, strike)}>
        {os.equipamento} <span className="text-xs text-muted-foreground">{os.marca}</span>
      </td>
      <td className={cellBase}>
        <OsStatusChip status={os.status} atrasada={prazoAtrasado} />
        {os.status === 'AguardandoAprovacao' && orcamentoVencido(os) && (
          <div className="mt-1 text-xs font-semibold text-warn">⚠ orçamento vencido</div>
        )}
        {os.status === 'AguardandoAprovacao' && !orcamentoVencido(os) && (
          <div className="mt-1 text-xs text-muted-foreground">
            vence {diaSemana(addDias(os.orcamento!.enviadoEm, os.orcamento!.validadeDias)).toLowerCase()},{' '}
            {formatDateShort(addDias(os.orcamento!.enviadoEm, os.orcamento!.validadeDias))}
          </div>
        )}
      </td>
      <td className={cn(cellBase, 'text-right font-semibold', strike)}>
        <MoneyValue centavos={centavosOuTraco(valorAtual(os))} />
      </td>
      <td className={cn(cellBase, 'text-right', strike)}>
        {os.prazo ? prazoAtrasado ? <span className="font-bold text-crit">atrasada</span> : formatDateShort(os.prazo) : '—'}
      </td>
      <td className={cn(cellBase, 'text-right')} onClick={(e) => e.stopPropagation()}>
        {acao ? (
          acao.nota ? (
            <>
              <span className="text-xs text-faint">{acao.label}</span>
              <br />
              <button type="button" onClick={acao.onClick} className="text-xs font-semibold text-primary-600 hover:underline">
                Ver peças →
              </button>
            </>
          ) : (
            <button type="button" onClick={acao.onClick} className="text-xs font-semibold text-primary-600 hover:underline">
              {acao.label}
            </button>
          )
        ) : (
          <span className="text-xs text-faint">—</span>
        )}
      </td>
    </tr>
  );
}

