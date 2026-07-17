import { Search } from 'lucide-react';
import { useRef, type KeyboardEvent } from 'react';

import { MoneyValue } from '@/components/shared';

import type { PdvVm } from './usePdv';

interface ScanCardProps {
  vm: PdvVm;
}

/** Card de busca do modo Caixa (`.scan-card` + `.dropdown` do mockup) — typeahead por nome/SKU. */
export function ScanCard({ vm }: ScanCardProps) {
  const inputRef = useRef<HTMLInputElement>(null);
  const aberto = vm.busca.trim().length > 0;

  function onKeyDown(e: KeyboardEvent<HTMLInputElement>) {
    if (!aberto) return;
    if (e.key === 'ArrowDown') {
      e.preventDefault();
      vm.moverSelecaoBusca(1);
    } else if (e.key === 'ArrowUp') {
      e.preventDefault();
      vm.moverSelecaoBusca(-1);
    } else if (e.key === 'Enter') {
      e.preventDefault();
      vm.confirmarBusca();
    } else if (e.key === 'Escape') {
      vm.onChangeBusca('');
    }
  }

  return (
    <div className="surface relative flex-none rounded-2xl p-3.5">
      <div className="flex items-center gap-2.5">
        <Search className="h-[19px] w-[19px] shrink-0 text-faint" />
        <input
          ref={inputRef}
          type="text"
          autoComplete="off"
          autoFocus
          value={vm.busca}
          onChange={(e) => vm.onChangeBusca(e.target.value)}
          onKeyDown={onKeyDown}
          placeholder="Digite o nome ou o SKU do produto…"
          className="flex-1 bg-transparent text-base text-foreground outline-none placeholder:text-faint"
        />
      </div>

      {aberto && (
        <div className="surface absolute inset-x-3.5 top-[calc(100%-2px)] z-20 overflow-hidden rounded-[13px] py-1 shadow-lg">
          {vm.resultadosBusca.length === 0 ? (
            <div className="px-3 py-3.5 text-sm text-muted-foreground">Nenhum produto encontrado.</div>
          ) : (
            vm.resultadosBusca.map((p, i) => (
              <button
                key={p.id}
                type="button"
                onClick={() => void vm.adicionarProduto(p)}
                className={`flex w-full items-center gap-2.5 px-3 py-2.5 text-left transition-colors ${
                  i === vm.buscaSelecionada ? 'bg-primary-soft' : 'hover:bg-primary-soft'
                }`}
              >
                <span className="flex h-8 w-8 shrink-0 items-center justify-center rounded-[9px] bg-surface-2 text-[13px] font-extrabold text-primary">
                  {(p.categoria ?? p.nome).charAt(0).toUpperCase()}
                </span>
                <span className="min-w-0 flex-1">
                  <span className="block truncate text-[13px] font-semibold text-foreground">{p.nome}</span>
                  <span className="block text-[11.5px] text-muted-foreground">
                    {p.categoria ?? 'Sem categoria'} · {p.sku}
                  </span>
                </span>
                <MoneyValue centavos={p.precoVenda.centavos} className="shrink-0 text-[13px] font-bold text-foreground" />
              </button>
            ))
          )}
        </div>
      )}
    </div>
  );
}
