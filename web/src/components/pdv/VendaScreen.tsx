import { LayoutGrid, ListFilter } from 'lucide-react';

import { BalcaoGrid } from './BalcaoGrid';
import { CartPanel } from './CartPanel';
import { ScanCard } from './ScanCard';
import { UltimoItemCard } from './UltimoItemCard';
import type { PdvVm } from './usePdv';

interface VendaScreenProps {
  vm: PdvVm;
}

/**
 * Tela 1 — montagem do carrinho (`#screenVenda` do mockup): coluna esquerda alterna entre busca
 * (Caixa) e grade por categoria (Balcão); coluna direita é o carrinho, sempre visível.
 */
export function VendaScreen({ vm }: VendaScreenProps) {
  if (!vm.venda) return null;

  return (
    <div className="grid min-h-0 flex-1 grid-cols-1 gap-4 lg:grid-cols-[1.55fr_1fr]">
      <div className="flex min-h-0 flex-col gap-3.5">
        <div className="inline-flex w-fit flex-none gap-0.5 rounded-[11px] border border-border bg-surface-2 p-[3px]">
          <button
            type="button"
            onClick={() => vm.setTerminalMode('caixa')}
            className={`flex items-center gap-1.5 rounded-lg px-3 py-1.5 text-[12.5px] font-semibold transition-colors ${
              vm.terminalMode === 'caixa' ? 'bg-card text-foreground shadow-sm' : 'text-muted-foreground hover:text-foreground'
            }`}
          >
            <ListFilter className="h-3.5 w-3.5" /> Caixa
          </button>
          <button
            type="button"
            onClick={() => vm.setTerminalMode('balcao')}
            className={`flex items-center gap-1.5 rounded-lg px-3 py-1.5 text-[12.5px] font-semibold transition-colors ${
              vm.terminalMode === 'balcao' ? 'bg-card text-foreground shadow-sm' : 'text-muted-foreground hover:text-foreground'
            }`}
          >
            <LayoutGrid className="h-3.5 w-3.5" /> Balcão
          </button>
        </div>

        {vm.terminalMode === 'caixa' ? (
          <>
            <ScanCard vm={vm} />
            <UltimoItemCard item={vm.ultimoItem} />
          </>
        ) : (
          <BalcaoGrid vm={vm} />
        )}
      </div>

      <CartPanel venda={vm.venda} onIrParaPagamento={vm.irParaPagamento} />
    </div>
  );
}
