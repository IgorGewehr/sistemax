import { Search } from 'lucide-react';

import { Surface } from '@/components/ui/Surface';

import type { FiltroStatusNota } from './calc';
import { FornecedoresTable } from './FornecedoresTable';
import { NotasTable } from './NotasTable';
import { PedidosTable } from './PedidosTable';
import { SegmentedTabs } from './SegmentedTabs';
import type { Fornecedor, NotaEntrada, Pedido } from './types';
import type { SegmentoTabela } from './useCompras';

const STATUS_OPCOES: { valor: FiltroStatusNota; label: string }[] = [
  { valor: 'todas', label: 'Todos os status' },
  { valor: 'conferir', label: 'A conferir' },
  { valor: 'divergente', label: 'Divergente' },
  { valor: 'recebida', label: 'Recebida' },
  { valor: 'estornada', label: 'Estornada' },
];

interface TabelaComprasSectionProps {
  segmentoAtivo: SegmentoTabela;
  onChangeSegmento: (seg: SegmentoTabela) => void;
  buscaTexto: string;
  onChangeBusca: (v: string) => void;
  filtroStatusNota: FiltroStatusNota;
  onChangeFiltroStatus: (v: FiltroStatusNota) => void;
  notasFiltradas: NotaEntrada[];
  pedidosFiltrados: Pedido[];
  fornecedoresFiltrados: Fornecedor[];
  fornecedores: Fornecedor[];
  onAbrirNota: (notaId: string) => void;
  onAbrirFornecedor: (fornecedorId: string) => void;
}

/** Card da tabela segmentada (Notas de entrada · Pedidos · Fornecedores) da Home. */
export function TabelaComprasSection({
  segmentoAtivo,
  onChangeSegmento,
  buscaTexto,
  onChangeBusca,
  filtroStatusNota,
  onChangeFiltroStatus,
  notasFiltradas,
  pedidosFiltrados,
  fornecedoresFiltrados,
  fornecedores,
  onAbrirNota,
  onAbrirFornecedor,
}: TabelaComprasSectionProps) {
  // O select nunca reflete "conferir_kpi" (só chega nesse estado via clique no KPI "Notas a conferir")
  // — mesmo comportamento do mockup, cujo <select> não tem essa opção e por isso mostra "Todos os status".
  const statusSelectValue = filtroStatusNota === 'conferir_kpi' ? 'todas' : filtroStatusNota;

  return (
    <Surface padding="none" className="overflow-hidden">
      <div className="flex flex-wrap items-center justify-between gap-3 px-[18px] pt-[15px]">
        <SegmentedTabs value={segmentoAtivo} onChange={onChangeSegmento} />

        <div className="flex items-center gap-2.5">
          <div className="flex items-center gap-1.5 rounded-[10px] border border-border bg-card px-3 py-2">
            <Search className="h-[15px] w-[15px] flex-none text-faint" />
            <input
              type="text"
              placeholder="buscar…"
              value={buscaTexto}
              onChange={(e) => onChangeBusca(e.target.value)}
              className="w-full min-w-[120px] border-0 bg-transparent text-sm text-foreground outline-none placeholder:text-faint"
            />
          </div>
          {segmentoAtivo === 'notas' && (
            <select
              value={statusSelectValue}
              onChange={(e) => onChangeFiltroStatus(e.target.value as FiltroStatusNota)}
              className="cursor-pointer rounded-[10px] border border-border bg-card px-2.5 py-2 text-[13px] font-semibold text-foreground"
            >
              {STATUS_OPCOES.map((o) => (
                <option key={o.valor} value={o.valor}>
                  {o.label}
                </option>
              ))}
            </select>
          )}
        </div>
      </div>

      <div className="mt-3 overflow-x-auto">
        {segmentoAtivo === 'notas' && <NotasTable notas={notasFiltradas} fornecedores={fornecedores} onAbrirNota={onAbrirNota} />}
        {segmentoAtivo === 'pedidos' && <PedidosTable pedidos={pedidosFiltrados} fornecedores={fornecedores} onAbrirNota={onAbrirNota} />}
        {segmentoAtivo === 'fornecedores' && <FornecedoresTable fornecedores={fornecedoresFiltrados} onAbrirFornecedor={onAbrirFornecedor} />}
      </div>
    </Surface>
  );
}
