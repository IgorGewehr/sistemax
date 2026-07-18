import { Plus } from 'lucide-react';

import { MockBadge, SectionCard, StatusChip } from '@/components/shared';
import { formatDateShort } from '@/lib/format';
import { cn } from '@/lib/utils';

import { categoriaLabel } from './calc';
import { ModalDarBaixa } from './ModalDarBaixa';
import { ModalDetalhe } from './ModalDetalhe';
import { ModalLancamento } from './ModalLancamento';
import { formatCentavosWhole } from './money';
import { MoneyValue } from './MoneyValue';
import type {
  CategoriasLancamentoRapido,
  ContaDisponivel,
  FiltroAtivo,
  LancamentoRow,
  NovoLancamentoInput,
  ResumoPdvMes,
  TimelineEntry,
} from './types';

interface LinhaDoTempoProps {
  entries: TimelineEntry[];
  hint: string;
  filtroAtivo: FiltroAtivo | null;
  onLimparFiltro: () => void;
  cobradosIds: ReadonlySet<string>;
  onDarBaixa: (rowId: string) => void;
  onCobrar: (rowId: string) => void;
  onAbrirDetalhe: (rowId: string) => void;
  onVerExtratoCompleto: () => void;
  resumoPdvMes: ResumoPdvMes;

  modalBaixa: { aberto: boolean; row: LancamentoRow | null };
  onFecharBaixa: () => void;
  onConfirmarBaixa: (rowId: string, conta: string) => void;
  contasDisponiveis: ContaDisponivel[];

  modalLancarAberto: boolean;
  onAbrirLancar: () => void;
  onFecharLancar: () => void;
  onSalvarLancamento: (input: NovoLancamentoInput) => void;
  categoriasLancamentoRapido: CategoriasLancamentoRapido;
  vencimentoPadrao: string;

  modalDetalhe: { aberto: boolean; row: LancamentoRow | null };
  onFecharDetalhe: () => void;
}

const COLUNAS = ['Data', 'Descrição', 'Categoria', 'Status', 'Valor', 'Ação'];

/** Linha do tempo (ExtratoUnificado) — tabela + filtro ativo + FAB de lançamento rápido + os 3 modais da tela. */
export function LinhaDoTempo({
  entries,
  hint,
  filtroAtivo,
  onLimparFiltro,
  cobradosIds,
  onDarBaixa,
  onCobrar,
  onAbrirDetalhe,
  onVerExtratoCompleto,
  resumoPdvMes,
  modalBaixa,
  onFecharBaixa,
  onConfirmarBaixa,
  contasDisponiveis,
  modalLancarAberto,
  onAbrirLancar,
  onFecharLancar,
  onSalvarLancamento,
  categoriasLancamentoRapido,
  vencimentoPadrao,
  modalDetalhe,
  onFecharDetalhe,
}: LinhaDoTempoProps) {
  return (
    <>
      <SectionCard title="Linha do tempo" hint={hint}>
        <div className="min-h-[26px] px-[18px] pb-2.5">
          {filtroAtivo && (
            <div className="flex items-center gap-2 text-xs text-muted-foreground">
              <span>Filtrando por:</span>
              <span className="inline-flex items-center gap-1.5 rounded-full bg-primary-soft py-1 pl-3 pr-1.5 text-xs font-bold text-primary-600">
                {filtroAtivo.label}
                <button
                  type="button"
                  onClick={onLimparFiltro}
                  aria-label="Limpar filtro"
                  className="rounded-full px-1 text-[15px] leading-none hover:bg-primary-600/10 active:brightness-95"
                >
                  ✕
                </button>
              </span>
            </div>
          )}
        </div>

        {entries.some((entry) => entry.kind === 'row') ? (
          <div className="overflow-x-auto">
            <table className="w-full min-w-[720px] border-collapse">
              <thead>
                <tr>
                  {COLUNAS.map((col, i) => (
                    <th
                      key={col}
                      className={cn(
                        'border-b border-border px-4 py-3 text-left text-[11px] font-semibold uppercase tracking-wide text-muted-foreground',
                        i >= 4 && 'text-right',
                      )}
                    >
                      {col}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {entries.map((entry, i) => {
                  if (entry.kind === 'divider') return <DividerRow key={`divider-${i}`} label={entry.label} />;
                  if (entry.kind === 'summary') {
                    return <SummaryRow key="summary" resumoPdvMes={resumoPdvMes} onVerExtratoCompleto={onVerExtratoCompleto} />;
                  }
                  return (
                    <RowLine
                      key={entry.row.id}
                      row={entry.row}
                      cobrado={cobradosIds.has(entry.row.id)}
                      onDarBaixa={onDarBaixa}
                      onCobrar={onCobrar}
                      onAbrirDetalhe={onAbrirDetalhe}
                    />
                  );
                })}
              </tbody>
            </table>
          </div>
        ) : (
          <p className="px-4 py-6 text-sm text-muted-foreground">
            {filtroAtivo ? 'Nenhum lançamento para este filtro.' : 'Nenhum lançamento no período.'}
          </p>
        )}
      </SectionCard>

      <button
        type="button"
        onClick={onAbrirLancar}
        aria-label="Lançar"
        title="Lançar"
        className="fixed bottom-6 right-6 z-30 grid h-[54px] w-[54px] place-items-center rounded-full bg-primary-600 text-white shadow-red transition-transform hover:-translate-y-0.5 hover:brightness-105 active:brightness-95"
      >
        <Plus className="h-6 w-6" strokeWidth={2.4} />
      </button>

      <ModalDarBaixa open={modalBaixa.aberto} row={modalBaixa.row} contas={contasDisponiveis} onClose={onFecharBaixa} onConfirmar={onConfirmarBaixa} />
      <ModalLancamento
        open={modalLancarAberto}
        categorias={categoriasLancamentoRapido}
        vencimentoPadrao={vencimentoPadrao}
        onClose={onFecharLancar}
        onSalvar={onSalvarLancamento}
      />
      <ModalDetalhe open={modalDetalhe.aberto} row={modalDetalhe.row} onClose={onFecharDetalhe} />
    </>
  );
}

function DividerRow({ label }: { label: string }) {
  return (
    <tr className="bg-surface-2/40">
      <td colSpan={6} className="px-4 py-1.5">
        <div className="flex items-center gap-2.5">
          <span className="h-px flex-1 bg-border" />
          <span className="whitespace-nowrap text-[11px] font-bold uppercase tracking-wide text-primary-600">{label}</span>
          <span className="h-px flex-1 bg-border" />
        </div>
      </td>
    </tr>
  );
}

function SummaryRow({ resumoPdvMes, onVerExtratoCompleto }: { resumoPdvMes: ResumoPdvMes; onVerExtratoCompleto: () => void }) {
  return (
    <tr>
      <td colSpan={6} className="px-4 py-3.5 text-center text-[12.5px] text-muted-foreground">
        <MockBadge
          className="mr-2 align-middle"
          titulo="Resumo do PDV do mês ainda não é decomposto do extrato — número de exemplo."
        />
        + {resumoPdvMes.qtdVendas} vendas menores no PDV este mês ({formatCentavosWhole(resumoPdvMes.totalCentavos)}) ·{' '}
        <a
          href="#"
          onClick={(e) => {
            e.preventDefault();
            onVerExtratoCompleto();
          }}
          className="font-bold text-primary-600 hover:underline"
        >
          Ver extrato completo →
        </a>
      </td>
    </tr>
  );
}

interface RowLineProps {
  row: LancamentoRow;
  cobrado: boolean;
  onDarBaixa: (id: string) => void;
  onCobrar: (id: string) => void;
  onAbrirDetalhe: (id: string) => void;
}

function RowLine({ row, cobrado, onDarBaixa, onCobrar, onAbrirDetalhe }: RowLineProps) {
  const sinal = row.tipo === 'entrada' ? 1 : -1;
  const clicavel = row.status === 'pago';

  return (
    <tr
      className={cn('border-b border-border/60 last:border-0', clicavel && 'cursor-pointer hover:bg-surface-2/60')}
      onClick={clicavel ? () => onAbrirDetalhe(row.id) : undefined}
    >
      <td className="num px-4 py-3 text-[13.5px]">{formatDateShort(row.data)}</td>
      <td className="px-4 py-3 text-[13.5px]">
        <b className="font-semibold">{row.desc}</b>
        {row.sub && <small className="mt-0.5 block text-[11.5px] font-medium text-muted-foreground">{row.sub}</small>}
      </td>
      <td className="px-4 py-3 text-[13px] text-muted-foreground">{categoriaLabel(row.categoria)}</td>
      <td className="px-4 py-3">
        <StatusCell row={row} />
      </td>
      <td className="px-4 py-3 text-right">
        <MoneyValue centavos={row.valorCentavos * sinal} signed tone={row.tipo === 'entrada' ? 'pos' : 'crit'} className="font-bold" />
      </td>
      <td className="px-4 py-3 text-right">
        <AcaoCell row={row} cobrado={cobrado} onDarBaixa={onDarBaixa} onCobrar={onCobrar} />
      </td>
    </tr>
  );
}

function StatusCell({ row }: { row: LancamentoRow }) {
  if (row.status === 'pago') {
    return (
      <div>
        <StatusChip tone="sobra">Pago</StatusChip>
        <small className="mt-0.5 block text-[11px] text-muted-foreground">· {row.conta}</small>
      </div>
    );
  }
  if (row.status === 'atrasado') {
    return <StatusChip tone="falta">Atrasado {row.diasAtraso}d</StatusChip>;
  }
  return <StatusChip tone="neutro">Previsto</StatusChip>;
}

interface AcaoCellProps {
  row: LancamentoRow;
  cobrado: boolean;
  onDarBaixa: (id: string) => void;
  onCobrar: (id: string) => void;
}

function AcaoCell({ row, cobrado, onDarBaixa, onCobrar }: AcaoCellProps) {
  if (row.status === 'previsto') {
    return (
      <button
        type="button"
        onClick={(e) => {
          e.stopPropagation();
          onDarBaixa(row.id);
        }}
        className="rounded-lg border border-primary-600/35 px-2.5 py-1 text-[11.5px] font-bold text-primary-600 transition-colors hover:bg-primary-soft active:brightness-95"
      >
        Dar baixa
      </button>
    );
  }
  if (row.status === 'atrasado') {
    return (
      <button
        type="button"
        disabled={cobrado}
        onClick={(e) => {
          e.stopPropagation();
          onCobrar(row.id);
        }}
        className={cn(
          'rounded-lg border px-2.5 py-1 text-[11.5px] font-bold transition-colors',
          cobrado ? 'cursor-default border-border text-muted-foreground opacity-50' : 'border-crit/35 text-crit hover:bg-crit-soft active:brightness-95',
        )}
      >
        {cobrado ? 'Enviada ✓' : 'Cobrar'}
      </button>
    );
  }
  return null;
}
