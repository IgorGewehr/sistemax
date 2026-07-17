import { Boxes } from 'lucide-react';

import { EstoqueTabs } from '@/components/estoque/EstoqueTabs';
import { InventariosView } from '@/components/estoque/InventariosView';
import { MovimentacoesView } from '@/components/estoque/MovimentacoesView';
import { ProdutosView } from '@/components/estoque/ProdutosView';
import { RelatoriosView } from '@/components/estoque/RelatoriosView';
import { useEstoque } from '@/components/estoque/useEstoque';
import { VisaoGeralView } from '@/components/estoque/VisaoGeralView';
import { EmptyState } from '@/components/ui/EmptyState';
import { Skeleton } from '@/components/ui/Skeleton';

/**
 * Estoque — upgrade visual 1:1 com `docs/ui/mockups/estoque.html` (5 abas), preservando o wiring
 * real ao Bridge: `estoqueApi.listarProdutos()` + `listarSaldos()` (dado, F1c) e `criarProduto()`
 * (ação). Página fina: todo o estado vive em `useEstoque`; ver `components/estoque/README.md`
 * para o que é real vs. o que ainda não tem API (Movimentações, Inventários, 6 dos 7 relatórios).
 */
export function Estoque() {
  const vm = useEstoque();

  return (
    <div className="mx-auto max-w-6xl px-4 py-6 sm:px-6 lg:py-8">
      <EstoqueTabs ativa={vm.tabAtiva} onChange={vm.irParaTab} />

      {vm.erroCarregamento ? (
        <div className="pt-2">
          <EmptyState icon={<Boxes className="h-5 w-5" />} title="Não deu para carregar" description={vm.erroCarregamento} />
        </div>
      ) : vm.carregando ? (
        <div className="space-y-3 pt-2">
          {[0, 1, 2, 3].map((i) => (
            <Skeleton key={i} className="h-16 w-full" />
          ))}
        </div>
      ) : (
        <>
          {vm.tabAtiva === 'geral' && <VisaoGeralView vm={vm} />}
          {vm.tabAtiva === 'produtos' && <ProdutosView vm={vm} />}
          {vm.tabAtiva === 'mov' && <MovimentacoesView />}
          {vm.tabAtiva === 'inv' && <InventariosView />}
          {vm.tabAtiva === 'rel' && <RelatoriosView vm={vm} />}
        </>
      )}
    </div>
  );
}
