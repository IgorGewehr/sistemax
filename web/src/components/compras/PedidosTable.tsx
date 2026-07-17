import { MoneyValue } from '@/components/shared';
import { cn } from '@/lib/utils';

import { fornById, PEDIDO_STATUS_LABEL, PEDIDO_STATUS_TONE } from './calc';
import { Chip } from './chips';
import { Td, Th } from './TableCells';
import type { Fornecedor, Pedido } from './types';

interface PedidosTableProps {
  pedidos: Pedido[];
  fornecedores: Fornecedor[];
  /** Só pedidos com nota vinculada (`notaId`) abrem a conferência — pedidos ainda sem nota não têm o que mostrar. */
  onAbrirNota: (notaId: string) => void;
}

/** Tabela "Pedidos" da segmentada da Home. */
export function PedidosTable({ pedidos, fornecedores, onAbrirNota }: PedidosTableProps) {
  if (pedidos.length === 0) {
    return <p className="px-4 py-6 text-center text-sm text-muted-foreground">Nenhum pedido encontrado.</p>;
  }

  return (
    <table className="w-full min-w-[640px] border-collapse">
      <thead>
        <tr>
          <Th>Pedido</Th>
          <Th>Fornecedor</Th>
          <Th>Enviado</Th>
          <Th>Previsto</Th>
          <Th align="right">Itens</Th>
          <Th>Status</Th>
          <Th align="right">Total</Th>
        </tr>
      </thead>
      <tbody>
        {pedidos.map((p) => {
          const fornecedor = fornById(fornecedores, p.fornecedorId);
          const clicavel = p.notaId !== null;
          return (
            <tr
              key={p.id}
              className={cn(clicavel && 'cursor-pointer hover:bg-surface-2/60')}
              onClick={clicavel ? () => onAbrirNota(p.notaId as string) : undefined}
            >
              <Td>
                <span className="font-semibold">{p.numero}</span>
              </Td>
              <Td>{fornecedor?.nome ?? '—'}</Td>
              <Td mono>{p.enviado ?? '—'}</Td>
              <Td mono>{p.previsto ?? '—'}</Td>
              <Td align="right" mono>
                {p.itensQtd}
              </Td>
              <Td>
                <Chip tone={PEDIDO_STATUS_TONE[p.status]}>{PEDIDO_STATUS_LABEL[p.status]}</Chip>
              </Td>
              <Td align="right" mono className="font-semibold">
                <MoneyValue centavos={p.totalCentavos} />
              </Td>
            </tr>
          );
        })}
      </tbody>
    </table>
  );
}
