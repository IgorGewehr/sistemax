import { MoneyValue } from '@/components/shared';

import { fornById, NOTA_STATUS_LABEL, NOTA_STATUS_TONE } from './calc';
import { Chip } from './chips';
import { Td, Th } from './TableCells';
import type { Fornecedor, NotaEntrada } from './types';

interface NotasTableProps {
  notas: NotaEntrada[];
  fornecedores: Fornecedor[];
  onAbrirNota: (notaId: string) => void;
}

/** Tabela "Notas de entrada" da segmentada da Home. */
export function NotasTable({ notas, fornecedores, onAbrirNota }: NotasTableProps) {
  if (notas.length === 0) {
    return <p className="px-4 py-6 text-center text-sm text-muted-foreground">Nenhuma nota encontrada.</p>;
  }

  return (
    <table className="w-full min-w-[640px] border-collapse">
      <thead>
        <tr>
          <Th>NF-e</Th>
          <Th>Fornecedor</Th>
          <Th>Emissão</Th>
          <Th>Itens</Th>
          <Th>Status</Th>
          <Th align="right">Total</Th>
        </tr>
      </thead>
      <tbody>
        {notas.map((n) => {
          const fornecedor = fornById(fornecedores, n.fornecedorId);
          const itensCount = n.itensCount ?? n.itens.length;
          return (
            <tr key={n.id} className="cursor-pointer hover:bg-surface-2/60" onClick={() => onAbrirNota(n.id)}>
              <Td>
                <span className="font-semibold">{n.numero}</span>
              </Td>
              <Td>{fornecedor?.nome ?? '—'}</Td>
              <Td mono>{n.emissao}</Td>
              <Td mono>{itensCount}</Td>
              <Td>
                <Chip tone={NOTA_STATUS_TONE[n.status]}>{NOTA_STATUS_LABEL[n.status]}</Chip>
              </Td>
              <Td align="right" mono className="font-semibold">
                <MoneyValue centavos={n.totalCentavos} />
              </Td>
            </tr>
          );
        })}
      </tbody>
    </table>
  );
}
