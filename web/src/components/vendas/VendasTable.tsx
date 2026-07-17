import { MoneyValue } from '@/components/shared';

import { formatFormasPagamento, isPagamentoDividido, VENDA_STATUS_LABEL, VENDA_STATUS_TONE } from './calc';
import { Chip } from './chips';
import { Td, Th } from './TableCells';
import type { VendaRow } from './types';

interface VendasTableProps {
  vendas: VendaRow[];
  onAbrirVenda: (vendaId: string) => void;
}

/**
 * Tabela única de Vendas — diferente de Compras, não há segmentação em abas aqui (só um tipo de
 * linha: a venda). Linha inteira é clicável → abre `VendaDetalheModal` (drill, sem fetch extra:
 * a linha já carrega tudo que o modal precisa).
 */
export function VendasTable({ vendas, onAbrirVenda }: VendasTableProps) {
  if (vendas.length === 0) {
    return <p className="px-4 py-6 text-center text-sm text-muted-foreground">Nenhuma venda encontrada.</p>;
  }

  return (
    <table className="w-full min-w-[860px] border-collapse">
      <thead>
        <tr>
          <Th>Venda</Th>
          <Th>Data/hora</Th>
          <Th>Canal</Th>
          <Th>Operador</Th>
          <Th>Cliente</Th>
          <Th>Pagamento</Th>
          <Th>Status</Th>
          <Th align="right">Total</Th>
        </tr>
      </thead>
      <tbody>
        {vendas.map((v) => (
          <tr key={v.id} className="cursor-pointer hover:bg-surface-2/60" onClick={() => onAbrirVenda(v.id)}>
            <Td>
              <span className="font-semibold">{v.numero}</span>
            </Td>
            <Td mono>{v.dataHoraLabel}</Td>
            <Td>{v.canal}</Td>
            <Td>{v.operador}</Td>
            <Td>{v.clienteNome ?? <span className="text-faint">Consumidor final</span>}</Td>
            <Td>
              {formatFormasPagamento(v.formasPagamento)}
              {isPagamentoDividido(v.formasPagamento) && (
                <span className="ml-1.5 rounded-full bg-surface-2 px-1.5 py-0.5 text-[10.5px] font-semibold uppercase tracking-[0.03em] text-muted-foreground">
                  dividido
                </span>
              )}
            </Td>
            <Td>
              <Chip tone={VENDA_STATUS_TONE[v.status]}>{VENDA_STATUS_LABEL[v.status]}</Chip>
            </Td>
            <Td align="right" mono className="font-semibold">
              <MoneyValue centavos={v.totalCentavos} />
            </Td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}
