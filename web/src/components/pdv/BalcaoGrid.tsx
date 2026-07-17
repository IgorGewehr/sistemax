import { Package } from 'lucide-react';

import { MoneyValue } from '@/components/shared';
import { EmptyState } from '@/components/ui/EmptyState';

import type { PdvVm } from './usePdv';

interface BalcaoGridProps {
  vm: PdvVm;
}

/** Modo Balcão (`.balcao-toolbar` + `.balcao-grid` do mockup) — grade de produtos por categoria, pro operador tocar direto. */
export function BalcaoGrid({ vm }: BalcaoGridProps) {
  return (
    <div className="flex min-h-0 flex-1 flex-col gap-3">
      <div className="flex flex-none gap-2 overflow-x-auto pb-0.5">
        {vm.categorias.map((c) => (
          <button
            key={c}
            type="button"
            onClick={() => vm.setBalcaoCategoria(c)}
            className={`flex-none whitespace-nowrap rounded-full border px-3.5 py-1.5 text-[12.5px] font-semibold transition-colors ${
              vm.balcaoCategoria === c
                ? 'border-transparent bg-primary text-white'
                : 'border-border bg-card text-muted-foreground hover:bg-surface-2'
            }`}
          >
            {c}
          </button>
        ))}
      </div>

      {vm.produtosBalcao.length === 0 ? (
        <EmptyState
          icon={<Package className="h-5 w-5" />}
          title="Nenhum produto nesta categoria"
          description="Cadastre produtos em Estoque para vender no PDV."
        />
      ) : (
        <div className="grid flex-1 auto-rows-min grid-cols-2 gap-2.5 overflow-y-auto pr-0.5 sm:grid-cols-3 md:grid-cols-4">
          {vm.produtosBalcao.map((p) => {
            const qtd = vm.qtdNoCarrinhoPorProduto.get(p.id) ?? 0;
            return (
              <button
                key={p.id}
                type="button"
                onClick={() => void vm.adicionarProduto(p)}
                disabled={vm.adicionando}
                className="surface relative flex flex-col items-start gap-2 rounded-2xl p-3 text-left transition-transform hover:-translate-y-0.5 hover:border-primary/40 disabled:opacity-60"
              >
                {qtd > 0 && (
                  <span className="absolute right-2 top-2 flex h-5 min-w-[20px] items-center justify-center rounded-full bg-primary px-1.5 text-[11px] font-extrabold text-white">
                    {qtd}
                  </span>
                )}
                <span className="flex h-[38px] w-[38px] items-center justify-center rounded-[10px] bg-surface-2 text-sm font-extrabold text-primary">
                  {(p.categoria ?? p.nome).charAt(0).toUpperCase()}
                </span>
                <span className="line-clamp-2 text-[12.5px] font-semibold leading-tight text-foreground">{p.nome}</span>
                <MoneyValue centavos={p.precoVenda.centavos} className="text-sm font-bold text-foreground" />
                <span className="text-[11px] text-muted-foreground">{p.categoria ?? 'Sem categoria'}</span>
              </button>
            );
          })}
        </div>
      )}
    </div>
  );
}
