import { Search } from 'lucide-react';

import { METODOS_PAGAMENTO } from '@/lib/api/vendas';

import type { Canal, FiltrosVendas } from './types';

interface FiltrosVendasBarProps {
  canais: Canal[];
  operadores: string[];
  filtros: FiltrosVendas;
  onChangeCanal: (canal: FiltrosVendas['canal']) => void;
  onChangeOperador: (operador: FiltrosVendas['operador']) => void;
  onChangeFormaPagamento: (forma: FiltrosVendas['formaPagamento']) => void;
  onChangeBusca: (busca: string) => void;
}

const SELECT_CLASS = 'cursor-pointer rounded-[10px] border border-border bg-card px-2.5 py-2 text-[13px] font-semibold text-foreground';

/**
 * Barra de filtros da tabela de Vendas — canal · operador · forma de pagamento · busca, todos
 * combináveis entre si (client-side sobre o array já carregado, ver `calc.ts`). Embutida no
 * header do `SectionCard` de `VendasTableSection`, mesmo lugar onde Compras embute sua busca.
 */
export function FiltrosVendasBar({
  canais,
  operadores,
  filtros,
  onChangeCanal,
  onChangeOperador,
  onChangeFormaPagamento,
  onChangeBusca,
}: FiltrosVendasBarProps) {
  return (
    <div className="flex flex-wrap items-center gap-2.5">
      <div className="flex items-center gap-1.5 rounded-[10px] border border-border bg-card px-3 py-2">
        <Search className="h-[15px] w-[15px] flex-none text-faint" />
        <input
          type="text"
          placeholder="buscar…"
          value={filtros.busca}
          onChange={(e) => onChangeBusca(e.target.value)}
          className="w-full min-w-[120px] border-0 bg-transparent text-sm text-foreground outline-none placeholder:text-faint"
        />
      </div>

      <select value={filtros.canal} onChange={(e) => onChangeCanal(e.target.value)} className={SELECT_CLASS}>
        <option value="todos">Todos os canais</option>
        {canais.map((c) => (
          <option key={c} value={c}>
            {c}
          </option>
        ))}
      </select>

      <select value={filtros.operador} onChange={(e) => onChangeOperador(e.target.value)} className={SELECT_CLASS}>
        <option value="todos">Todos os operadores</option>
        {operadores.map((o) => (
          <option key={o} value={o}>
            {o}
          </option>
        ))}
      </select>

      <select
        value={filtros.formaPagamento}
        onChange={(e) => onChangeFormaPagamento(e.target.value as FiltrosVendas['formaPagamento'])}
        className={SELECT_CLASS}
      >
        <option value="todas">Todas as formas</option>
        {METODOS_PAGAMENTO.map((m) => (
          <option key={m} value={m}>
            {m}
          </option>
        ))}
      </select>
    </div>
  );
}
