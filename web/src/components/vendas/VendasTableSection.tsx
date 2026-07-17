import { SectionCard } from '@/components/shared';

import { FiltrosVendasBar } from './FiltrosVendasBar';
import type { Canal, FiltrosVendas, VendaRow } from './types';
import { VendasTable } from './VendasTable';

interface VendasTableSectionProps {
  vendas: VendaRow[];
  canais: Canal[];
  operadores: string[];
  filtros: FiltrosVendas;
  onChangeCanal: (canal: FiltrosVendas['canal']) => void;
  onChangeOperador: (operador: FiltrosVendas['operador']) => void;
  onChangeFormaPagamento: (forma: FiltrosVendas['formaPagamento']) => void;
  onChangeBusca: (busca: string) => void;
  onAbrirVenda: (vendaId: string) => void;
}

/**
 * Único `SectionCard` da tela: barra de filtros no header + tabela de vendas. Sem segmentação em
 * abas (Compras tem Notas/Pedidos/Fornecedores; Vendas só tem um tipo de linha).
 *
 * O `id="vendas-tabela"` no wrapper é o alvo do scroll do drill do Super Consultor
 * (`useVendas.aplicarFiltroSabados`) — `SectionCard` não aceita `id` direto, por isso o wrapper.
 */
export function VendasTableSection({
  vendas,
  canais,
  operadores,
  filtros,
  onChangeCanal,
  onChangeOperador,
  onChangeFormaPagamento,
  onChangeBusca,
  onAbrirVenda,
}: VendasTableSectionProps) {
  return (
    <div id="vendas-tabela">
      <SectionCard
        title="Vendas"
        hint={`${vendas.length} no período`}
        actions={
          <FiltrosVendasBar
            canais={canais}
            operadores={operadores}
            filtros={filtros}
            onChangeCanal={onChangeCanal}
            onChangeOperador={onChangeOperador}
            onChangeFormaPagamento={onChangeFormaPagamento}
            onChangeBusca={onChangeBusca}
          />
        }
      >
        <div className="overflow-x-auto">
          <VendasTable vendas={vendas} onAbrirVenda={onAbrirVenda} />
        </div>
      </SectionCard>
    </div>
  );
}
