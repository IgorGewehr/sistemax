import { MoneyValue } from '@/components/shared';

import { estaSemComprar90d, STATUS_LABEL, STATUS_TONE } from './calc';
import { Chip } from './chips';
import { Td, Th } from './TableCells';
import type { Cliente } from './types';
import type { ClientesVm } from './useClientes';

interface ClientesTableProps {
  clientes: Cliente[];
  hojeLabel: ClientesVm['hojeLabel'];
  onAbrirCliente: (clienteId: string) => void;
}

/** Tabela de clientes da Home — linha clicável abre a Ficha (drill de 1 cliente). */
export function ClientesTable({ clientes, hojeLabel, onAbrirCliente }: ClientesTableProps) {
  if (clientes.length === 0) {
    return <p className="px-4 py-6 text-center text-sm text-muted-foreground">Nenhum cliente encontrado.</p>;
  }

  return (
    <table className="w-full min-w-[720px] border-collapse">
      <thead>
        <tr>
          <Th>Cliente</Th>
          <Th>Última visita</Th>
          <Th align="right">Compras</Th>
          <Th align="right">Total 12m</Th>
          <Th>Status</Th>
        </tr>
      </thead>
      <tbody>
        {clientes.map((c) => {
          const parado = estaSemComprar90d(c, hojeLabel);
          return (
            <tr key={c.id} className="cursor-pointer hover:bg-surface-2/60" onClick={() => onAbrirCliente(c.id)}>
              <Td>
                <span className="font-semibold">{c.nome}</span>
                {(c.telefone || c.email) && <div className="text-xs text-muted-foreground">{c.telefone ?? c.email}</div>}
              </Td>
              <Td mono>
                {c.ultimaVisita ?? '—'}
                {parado && <span className="ml-1.5 text-xs font-semibold text-warn">parado</span>}
              </Td>
              <Td align="right" mono>
                {c.comprasCount}
              </Td>
              <Td align="right" mono className="font-semibold">
                <MoneyValue centavos={c.totalGasto12mCentavos} whole />
              </Td>
              <Td>
                <Chip tone={STATUS_TONE[c.status]}>{STATUS_LABEL[c.status]}</Chip>
              </Td>
            </tr>
          );
        })}
      </tbody>
    </table>
  );
}
