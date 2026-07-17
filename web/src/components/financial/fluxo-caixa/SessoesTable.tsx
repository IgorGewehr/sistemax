import { useMemo } from 'react';

import { SectionCard, StatusChip } from '@/components/shared';
import { cn } from '@/lib/utils';

import { diaLabel, diferencaCentavos, esperadoCentavos } from './calc';
import { MoneyWhole } from './MoneyWhole';
import type { SessaoCaixa } from './types';

interface SessoesTableProps {
  todasAsSessoes: SessaoCaixa[];
  onSelectDay: (dia: number) => void;
}

const THEAD_CLASS = 'border-b border-border px-4 py-3 text-left text-[11px] font-semibold uppercase tracking-wide text-muted-foreground';

/** A tabela-mãe: toda sessão do mês, uma linha por dia, clicável — leva direto pro drill da
 * `AnaliseInterativa` acima. */
export function SessoesTable({ todasAsSessoes, onSelectDay }: SessoesTableProps) {
  const ordenados = useMemo(() => [...todasAsSessoes].sort((a, b) => a.dia - b.dia), [todasAsSessoes]);
  const fechadas = ordenados.filter((s) => s.status === 'fechado').length;
  const abertas = ordenados.length - fechadas;

  return (
    <SectionCard title="Sessões" hint={`${fechadas} sessões fechadas · ${abertas} em aberto hoje`} className="mb-4" bodyClassName="overflow-x-auto">
      {ordenados.length === 0 ? (
        <p className="px-4 py-6 text-sm text-muted-foreground">Nenhuma sessão de caixa neste mês.</p>
      ) : (
        <table className="w-full min-w-[680px] border-collapse">
          <thead>
            <tr>
              <th className={THEAD_CLASS}>Dia</th>
              <th className={THEAD_CLASS}>Operador</th>
              <th className={THEAD_CLASS}>Abertura</th>
              <th className={THEAD_CLASS}>Fechamento</th>
              <th className={cn(THEAD_CLASS, 'text-right')}>Esperado</th>
              <th className={cn(THEAD_CLASS, 'text-right')}>Contado</th>
              <th className={cn(THEAD_CLASS, 'text-right')}>Diferença</th>
            </tr>
          </thead>
          <tbody>
            {ordenados.map((sessao) => (
              <SessaoRow key={sessao.dia} sessao={sessao} onSelect={onSelectDay} />
            ))}
          </tbody>
        </table>
      )}
    </SectionCard>
  );
}

function SessaoRow({ sessao, onSelect }: { sessao: SessaoCaixa; onSelect: (dia: number) => void }) {
  return (
    <tr
      onClick={() => onSelect(sessao.dia)}
      className="cursor-pointer border-b border-border/60 text-[13.5px] transition-colors last:border-0 hover:bg-surface-2/60"
    >
      <td className="num px-4 py-3.5">{diaLabel(sessao.dia)}</td>
      <td className="px-4 py-3.5">{sessao.operador}</td>
      <td className="num px-4 py-3.5">{sessao.horaAbertura}</td>
      <td className="num px-4 py-3.5">{sessao.horaFechamento ?? '—'}</td>
      <td className="px-4 py-3.5 text-right">
        <MoneyWhole centavos={esperadoCentavos(sessao)} className="font-semibold" />
      </td>
      <td className="px-4 py-3.5 text-right">
        {sessao.contadoCentavos !== null ? <MoneyWhole centavos={sessao.contadoCentavos} className="font-semibold" /> : '—'}
      </td>
      <td className="px-4 py-3.5 text-right">
        <DiferencaCell sessao={sessao} />
      </td>
    </tr>
  );
}

function DiferencaCell({ sessao }: { sessao: SessaoCaixa }) {
  if (sessao.status === 'aberto') return <StatusChip tone="aberto">Em aberto</StatusChip>;

  const diff = diferencaCentavos(sessao) ?? 0;
  if (diff === 0) return <StatusChip tone="bateu">Bateu certinho</StatusChip>;

  return (
    <span className="inline-flex items-center justify-end gap-1.5">
      <MoneyWhole centavos={diff} signed tone={diff > 0 ? 'pos' : 'crit'} />
      <StatusChip tone={diff > 0 ? 'sobra' : 'falta'}>{diff > 0 ? 'Sobra' : 'Falta'}</StatusChip>
    </span>
  );
}
