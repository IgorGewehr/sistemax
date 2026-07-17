import { Search } from 'lucide-react';

import { Surface } from '@/components/ui/Surface';
import { cn } from '@/lib/utils';

import { ClientesTable } from './ClientesTable';
import type { Cliente, FiltroClientes } from './types';

const FILTRO_OPCOES: { valor: FiltroClientes; label: string }[] = [
  { valor: 'todos', label: 'Todos' },
  { valor: 'aniversariantes', label: 'Aniversariantes' },
  { valor: 'semComprar90d', label: 'Sem comprar 90d+' },
];

interface ClientesTableSectionProps {
  filtro: FiltroClientes;
  onToggleFiltro: (filtro: FiltroClientes) => void;
  buscaTexto: string;
  onChangeBusca: (v: string) => void;
  clientesFiltrados: Cliente[];
  hojeLabel: string;
  onAbrirCliente: (clienteId: string) => void;
}

/** Card da tabela de clientes da Home: chips de filtro exclusivo + busca + tabela clicável. */
export function ClientesTableSection({
  filtro,
  onToggleFiltro,
  buscaTexto,
  onChangeBusca,
  clientesFiltrados,
  hojeLabel,
  onAbrirCliente,
}: ClientesTableSectionProps) {
  return (
    <Surface padding="none" className="overflow-hidden">
      <div className="flex flex-wrap items-center justify-between gap-3 px-[18px] pt-[15px]">
        <div className="inline-flex flex-wrap gap-0.5 rounded-[11px] border border-border bg-surface-2 p-[3px]">
          {FILTRO_OPCOES.map((o) => (
            <button
              key={o.valor}
              type="button"
              onClick={() => onToggleFiltro(o.valor)}
              className={cn(
                'rounded-lg px-3.5 py-1.5 text-[13px] font-semibold transition-colors active:brightness-95',
                filtro === o.valor ? 'bg-card text-foreground shadow-sm' : 'text-muted-foreground hover:text-foreground',
              )}
            >
              {o.label}
            </button>
          ))}
        </div>

        <div className="flex items-center gap-1.5 rounded-[10px] border border-border bg-card px-3 py-2">
          <Search className="h-[15px] w-[15px] flex-none text-faint" />
          <input
            type="text"
            placeholder="buscar por nome, telefone ou email…"
            value={buscaTexto}
            onChange={(e) => onChangeBusca(e.target.value)}
            className="w-full min-w-[200px] border-0 bg-transparent text-sm text-foreground outline-none placeholder:text-faint"
          />
        </div>
      </div>

      <div className="mt-3 overflow-x-auto">
        <ClientesTable clientes={clientesFiltrados} hojeLabel={hojeLabel} onAbrirCliente={onAbrirCliente} />
      </div>
    </Surface>
  );
}
