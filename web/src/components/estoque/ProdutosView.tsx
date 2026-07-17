import { Plus } from 'lucide-react';

import { PageHeader } from '@/components/shared';
import { Button } from '@/components/ui/Button';
import { Surface } from '@/components/ui/Surface';

import { categoriasNomesDe } from './calc';
import { NovoProdutoModal } from './NovoProdutoModal';
import { ProdutoFichaView } from './ProdutoFichaView';
import { ProdutosFiltrosBar } from './ProdutosFiltrosBar';
import { ProdutosTable } from './ProdutosTable';
import type { EstoqueVm } from './useEstoque';

interface ProdutosViewProps {
  vm: EstoqueVm;
}

/** Aba "Produtos" — lista real (com filtros client-side) ou a ficha do produto em drill. Ação
 * real: `+ Novo produto` → `estoqueApi.criarProduto()`. Sem o botão "Exportar" do mockup (não há
 * ação real por trás dele nesta versão). */
export function ProdutosView({ vm }: ProdutosViewProps) {
  if (vm.produtoFicha) {
    return <ProdutoFichaView item={vm.produtoFicha} onVoltar={vm.fecharProduto} />;
  }

  return (
    <div>
      <PageHeader
        subtitle="O catálogo, o saldo e o estado de cada item."
        actions={
          <Button size="sm" icon={<Plus className="h-4 w-4" />} onClick={vm.abrirModal}>
            Novo produto
          </Button>
        }
      />

      <ProdutosFiltrosBar filtro={vm.filtro} categorias={categoriasNomesDe(vm.itens)} onChange={vm.onChangeFiltro} />

      <Surface padding="none" className="overflow-hidden">
        <ProdutosTable itens={vm.produtosFiltrados} total={vm.totalProdutos} onAbrir={vm.abrirProduto} />
      </Surface>

      <NovoProdutoModal open={vm.modalAberto} onClose={vm.fecharModal} onCriado={vm.onCriado} />
    </div>
  );
}
