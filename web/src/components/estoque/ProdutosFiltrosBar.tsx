import { Search } from 'lucide-react';

import type { EstadoItemCode, ProdutosFiltro } from './types';

interface ProdutosFiltrosBarProps {
  filtro: ProdutosFiltro;
  categorias: string[];
  onChange: (filtro: ProdutosFiltro) => void;
}

const ESTADOS: { value: 'todos' | EstadoItemCode; label: string }[] = [
  { value: 'todos', label: 'Estado: todos' },
  { value: 'ok', label: 'OK' },
  { value: 'baixo', label: 'Baixo' },
  { value: 'zerado', label: 'Zerado' },
  { value: 'servico', label: 'Serviço' },
];

const selectClass =
  'rounded-[10px] border border-border bg-card px-2.5 py-2 text-[13px] font-semibold text-foreground outline-none focus-visible:ring-2 focus-visible:ring-ring';

/** Filtros da aba Produtos (`.filtros` do mockup) — client-side sobre a lista já carregada, sem
 * chamada de rede por filtro (mesma escolha de `components/compras`/`vendas`). */
export function ProdutosFiltrosBar({ filtro, categorias, onChange }: ProdutosFiltrosBarProps) {
  return (
    <div className="mb-3.5 flex flex-wrap items-center gap-2.5">
      <div className="flex min-w-[220px] flex-1 items-center gap-1.5 rounded-[10px] border border-border bg-card px-3 py-2">
        <Search className="h-[15px] w-[15px] flex-none text-faint" />
        <input
          type="text"
          placeholder="nome ou SKU…"
          value={filtro.busca}
          onChange={(e) => onChange({ ...filtro, busca: e.target.value })}
          className="w-full bg-transparent text-sm text-foreground outline-none placeholder:text-faint"
        />
      </div>

      <select
        value={filtro.categoria}
        onChange={(e) => onChange({ ...filtro, categoria: e.target.value })}
        className={selectClass}
      >
        <option value="todas">Categoria: todas</option>
        {categorias.map((c) => (
          <option key={c} value={c}>
            {c}
          </option>
        ))}
      </select>

      <select
        value={filtro.estado}
        onChange={(e) => onChange({ ...filtro, estado: e.target.value as ProdutosFiltro['estado'] })}
        className={selectClass}
      >
        {ESTADOS.map((e) => (
          <option key={e.value} value={e.value}>
            {e.label}
          </option>
        ))}
      </select>

      <label className="inline-flex cursor-pointer select-none items-center gap-2 whitespace-nowrap text-[13px] font-semibold text-muted-foreground">
        <input
          type="checkbox"
          checked={filtro.soProblema}
          onChange={(e) => onChange({ ...filtro, soProblema: e.target.checked })}
          className="h-4 w-4 rounded border-border accent-primary-600"
        />
        Só com problema
      </label>
    </div>
  );
}
