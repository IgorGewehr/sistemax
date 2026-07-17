import { MoneyValue } from '@/components/shared';

import { FORNECEDOR_STATUS_LABEL, FORNECEDOR_STATUS_TONE } from './calc';
import { Chip } from './chips';
import { Td, Th } from './TableCells';
import type { Fornecedor } from './types';

interface FornecedoresTableProps {
  fornecedores: Fornecedor[];
  onAbrirFornecedor: (fornecedorId: string) => void;
}

/** Tabela "Fornecedores" da segmentada da Home. */
export function FornecedoresTable({ fornecedores, onAbrirFornecedor }: FornecedoresTableProps) {
  if (fornecedores.length === 0) {
    return <p className="px-4 py-6 text-center text-sm text-muted-foreground">Nenhum fornecedor encontrado.</p>;
  }

  return (
    <table className="w-full min-w-[640px] border-collapse">
      <thead>
        <tr>
          <Th>Fornecedor</Th>
          <Th align="right">Comprado 12m</Th>
          <Th>Lead time real × prometido</Th>
          <Th>Divergência</Th>
          <Th>Status</Th>
        </tr>
      </thead>
      <tbody>
        {fornecedores.map((f) => (
          <tr key={f.id} className="cursor-pointer hover:bg-surface-2/60" onClick={() => onAbrirFornecedor(f.id)}>
            <Td>
              <span className="font-semibold">{f.nome}</span>
              {f.cnpj && <div className="text-xs text-muted-foreground">{f.cnpj}</div>}
            </Td>
            <Td align="right" mono className="font-semibold">
              <MoneyValue centavos={f.comprado12mCentavos} />
            </Td>
            <Td mono>
              {f.leadTimeRealDias.toFixed(1).replace('.', ',')}d <span className="text-muted-foreground">vs {f.leadTimePrometidoDias}d</span>
            </Td>
            <Td mono>
              {f.divergNotas} de {f.divergTotal}
            </Td>
            <Td>
              <Chip tone={FORNECEDOR_STATUS_TONE[f.status]}>{FORNECEDOR_STATUS_LABEL[f.status]}</Chip>
            </Td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}
