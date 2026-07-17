/**
 * "Todas as assinaturas" — tabela nominal REAL (`GET /financeiro/recorrentes/detalhe`), mockup
 * `docs/ui/mockups/financeiro-assinaturas.html` linhas 369-441 (`#tabelaAssinaturas`, nunca antes
 * portado — a UI só tinha o resumo agregado + o painel MRR/retenção ilustrativos). Colunas 1:1 do
 * mockup, exceto "Status" (aqui é o enum bruto do domínio, sem "atrasada Nd"/"risco de churn" —
 * não modelado no backend hoje).
 */
import type { ReactNode } from 'react';

import { SectionCard, StatusChip, type ChipTone } from '@/components/shared';
import type { AssinaturaDetalheReal } from '@/lib/api/adapters/financeiro/recorrentes';
import { formatCentavosWhole } from '@/lib/money';
import { cn } from '@/lib/utils';

interface AssinaturasTabelaRealProps {
  itens: AssinaturaDetalheReal[];
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

function statusTone(status: string): ChipTone {
  const s = status.toLowerCase();
  if (s.includes('cancel')) return 'falta';
  if (s.includes('pausa') || s.includes('atras')) return 'aberto';
  if (s.includes('ativ')) return 'sobra';
  return 'neutro';
}

export function AssinaturasTabelaReal({ itens }: AssinaturasTabelaRealProps) {
  return (
    <SectionCard title="Todas as assinaturas" hint={`${itens.length} ativas`} bodyClassName="overflow-x-auto pb-1" className="mb-4">
      {itens.length === 0 ? (
        <p className="px-4 py-6 text-sm text-muted-foreground">Nenhuma assinatura ativa no momento.</p>
      ) : (
        <table className="w-full min-w-[680px] border-collapse">
          <thead>
            <tr>
              <Th>Serviço</Th>
              <Th>Cliente</Th>
              <Th align="right">Valor/mês</Th>
              <Th>Ciclo</Th>
              <Th>Próx. cobrança</Th>
              <Th>Status</Th>
            </tr>
          </thead>
          <tbody>
            {itens.map((it) => (
              <tr key={it.id} className="border-b border-border/60 last:border-0 hover:bg-surface-2/60">
                <Td className="font-semibold text-foreground">{it.servicoNome}</Td>
                <Td className="text-muted-foreground">{it.clienteNome}</Td>
                <Td align="right" className="num font-semibold">
                  {formatCentavosWhole(it.valorPorCicloCentavos)}
                </Td>
                <Td>{it.ciclo}</Td>
                <Td className="num">{it.proximaCobrancaLabel}</Td>
                <Td>
                  <StatusChip tone={statusTone(it.status)}>{it.status}</StatusChip>
                </Td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </SectionCard>
  );
}
