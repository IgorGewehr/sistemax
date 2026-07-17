import { ShoppingCart } from 'lucide-react';

import { PagamentoScreen } from '@/components/pdv/PagamentoScreen';
import { SucessoScreen } from '@/components/pdv/SucessoScreen';
import { usePdv } from '@/components/pdv/usePdv';
import { VendaScreen } from '@/components/pdv/VendaScreen';
import { PageHeader } from '@/components/shared';
import { EmptyState } from '@/components/ui/EmptyState';
import { Skeleton } from '@/components/ui/Skeleton';

/**
 * PDV — reprodução fiel de `docs/ui/mockups/pdv.html`, com o wiring real do `Pdv.tsx` anterior
 * preservado à risca: `vendasApi.abrir()` ao iniciar → `adicionarItem` ao bipar/tocar um produto
 * → `registrarPagamento` por forma → `concluir` ao finalizar. Página fina — todo o estado/lógica
 * vive em `usePdv` (hook); aqui só se decide qual das 3 telas (Venda · Pagamento · Sucesso)
 * mostrar, mesmo contrato de `Compras`/`OrdemServico`. Ver `components/pdv/README.md` pro mapa
 * de decisões e pro que ficou fora de escopo (nada no contrato de API sustenta).
 */
export function Pdv() {
  const vm = usePdv();

  if (vm.erro) {
    return (
      <div className="mx-auto max-w-6xl px-4 py-6 sm:px-6 lg:py-8">
        <EmptyState icon={<ShoppingCart className="h-5 w-5" />} title="PDV indisponível" description={vm.erro} />
      </div>
    );
  }

  if (vm.carregando || !vm.venda) {
    return (
      <div className="mx-auto flex h-[calc(100vh-260px)] min-h-[560px] max-w-[1600px] flex-col gap-4 px-4 py-6 sm:px-6 lg:py-8">
        <Skeleton className="h-8 w-56" />
        <div className="grid flex-1 grid-cols-1 gap-4 lg:grid-cols-[1.55fr_1fr]">
          <Skeleton className="h-full w-full" />
          <Skeleton className="h-full w-full" />
        </div>
      </div>
    );
  }

  return (
    <div className="mx-auto flex h-[calc(100vh-260px)] min-h-[560px] max-w-[1600px] flex-col px-4 py-6 sm:px-6 lg:py-8">
      <PageHeader subtitle="Bipe, monte o carrinho e receba o pagamento — tudo direto no Bridge local." />

      {vm.screen === 'venda' && <VendaScreen vm={vm} />}
      {vm.screen === 'pagamento' && <PagamentoScreen vm={vm} />}
      {vm.screen === 'sucesso' && <SucessoScreen venda={vm.venda} onNovaVenda={() => void vm.novaVenda()} carregando={vm.carregando} />}
    </div>
  );
}
