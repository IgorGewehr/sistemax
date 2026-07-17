import { MoneyValue } from '@/components/shared';

import { NOTA_STATUS_LABEL, NOTA_STATUS_TONE } from './calc';
import { Chip } from './chips';
import { Td, Th } from './TableCells';
import type { NotaEntrada } from './types';

interface NotasFornecedorTableProps {
  notas: NotaEntrada[];
  onAbrirNota: (notaId: string) => void;
}

/** "Notas deste fornecedor" (drill de fornecedor, Tela 9.5). */
export function NotasFornecedorTable({ notas, onAbrirNota }: NotasFornecedorTableProps) {
  return (
    <table className="w-full min-w-[420px] border-collapse">
      <thead>
        <tr>
          <Th>NF-e</Th>
          <Th>Emissão</Th>
          <Th>Status</Th>
          <Th align="right">Total</Th>
        </tr>
      </thead>
      <tbody>
        {notas.map((n) => (
          <tr key={n.id} className="cursor-pointer hover:bg-surface-2/60" onClick={() => onAbrirNota(n.id)}>
            <Td>
              <span className="font-semibold">{n.numero}</span>
            </Td>
            <Td mono>{n.emissao}</Td>
            <Td>
              <Chip tone={NOTA_STATUS_TONE[n.status]}>{NOTA_STATUS_LABEL[n.status]}</Chip>
            </Td>
            <Td align="right" mono className="font-semibold">
              <MoneyValue centavos={n.totalCentavos} />
            </Td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}
