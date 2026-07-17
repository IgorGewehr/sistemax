import { MoneyValue } from '@/components/shared';

import { HISTORICO_TONE, statusHistoricoTone } from './calc';
import { Chip } from './chips';
import { Td, Th } from './TableCells';
import type { HistoricoItem } from './types';

interface HistoricoTableProps {
  itens: HistoricoItem[];
}

/** Tabela cronológica reversa de compras/OS da Ficha — `itens` já vem ordenado pelo mock (mais recente primeiro). */
export function HistoricoTable({ itens }: HistoricoTableProps) {
  if (itens.length === 0) {
    return <p className="px-4 py-6 text-center text-sm text-muted-foreground">Nenhuma compra ou OS registrada ainda.</p>;
  }

  return (
    <table className="w-full min-w-[560px] border-collapse">
      <thead>
        <tr>
          <Th>Data</Th>
          <Th>Tipo</Th>
          <Th>Descrição</Th>
          <Th align="right">Valor</Th>
        </tr>
      </thead>
      <tbody>
        {itens.map((it) => (
          <tr key={it.id}>
            <Td mono>{it.data}</Td>
            <Td>
              <Chip tone={it.tipo === 'os' ? statusHistoricoTone(it.statusLabel) : HISTORICO_TONE.venda}>
                {it.tipo === 'os' ? (it.statusLabel ?? 'OS') : 'Venda'}
              </Chip>
            </Td>
            <Td>{it.descricao}</Td>
            <Td align="right" mono className="font-semibold">
              <MoneyValue centavos={it.valorCentavos} />
            </Td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}
