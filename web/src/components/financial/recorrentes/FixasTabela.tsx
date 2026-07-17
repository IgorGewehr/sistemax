/**
 * Tabela completa de contas fixas — mockup: `#tabelaFixas` (`renderTabelaFixas`).
 */
import type { ReactNode } from 'react';

import { SectionCard, StatusChip } from '@/components/shared';
import type { Centavos } from '@/lib/money';
import { cn } from '@/lib/utils';

import { formatCentavosWhole, formatPctSigned } from './calc';
import type { ContaFixaDerivada } from './types';

interface FixasTabelaProps {
  itens: ContaFixaDerivada[];
  totalAtual: Centavos;
}

function Th({ children, align }: { children: ReactNode; align?: 'right' }) {
  return (
    <th
      className={cn(
        'border-b border-border px-4 py-3 text-[11px] font-semibold uppercase tracking-[0.06em] text-muted-foreground',
        align === 'right' ? 'text-right' : 'text-left',
      )}
    >
      {children}
    </th>
  );
}

function Td({ children, align, className }: { children: ReactNode; align?: 'right'; className?: string }) {
  return <td className={cn('px-4 py-[13px] text-[13.5px]', align === 'right' && 'text-right', className)}>{children}</td>;
}

export function FixasTabela({ itens, totalAtual }: FixasTabelaProps) {
  const rows = [...itens].sort((a, b) => b.atual - a.atual);

  return (
    <SectionCard
      title="Todas as recorrências"
      hint={`${itens.length} ativas · custo total ${formatCentavosWhole(totalAtual)}/mês`}
      bodyClassName="overflow-x-auto pb-1"
    >
      <table className="w-full min-w-[700px] border-collapse">
        <thead>
          <tr>
            <Th>Nome</Th>
            <Th>Categoria</Th>
            <Th align="right">Valor/mês</Th>
            <Th>Dia venc.</Th>
            <Th>Próxima</Th>
            <Th align="right">Variação vs média</Th>
            <Th>Status</Th>
          </tr>
        </thead>
        <tbody>
          {rows.map((it) => (
            <tr key={it.id} className="border-b border-border/60 last:border-0 hover:bg-surface-2/60">
              <Td>
                <span className="inline-flex items-center gap-2 font-semibold text-foreground">
                  <span className={cn('h-2.5 w-2.5 shrink-0 rounded-sm', it.emAlerta ? 'bg-warn' : 'bg-foreground/40')} />
                  {it.nome}
                </span>
              </Td>
              <Td className="text-[12.5px] text-muted-foreground">{it.categoria}</Td>
              <Td align="right" className="num font-semibold">
                {formatCentavosWhole(it.atual)}
              </Td>
              <Td className="num">dia {it.diaVencimento}</Td>
              <Td className="num">{it.proximaLabel}</Td>
              <Td align="right" className={cn('num font-bold', it.emAlerta ? 'text-warn' : 'text-muted-foreground')}>
                {formatPctSigned(it.variacaoPct)}
              </Td>
              <Td>{it.emAlerta ? <StatusChip tone="aberto">Atenção</StatusChip> : <StatusChip tone="sobra">Estável</StatusChip>}</Td>
            </tr>
          ))}
        </tbody>
      </table>
    </SectionCard>
  );
}
