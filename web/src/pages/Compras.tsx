import { ComprasHome } from '@/components/compras/ComprasHome';
import { ConferenciaView } from '@/components/compras/ConferenciaView';
import { FornecedorView } from '@/components/compras/FornecedorView';
import { useCompras } from '@/components/compras/useCompras';

/**
 * Compras — reprodução 1:1 de `docs/ui/mockups/compras.html`. Página fina: todo o estado/lógica
 * vive em `useCompras` (hook); aqui só se decide qual das 3 "telas" do mockup renderizar
 * (Home · Conferência de nota · Drill de fornecedor — a mesma rota alterna entre elas, como no
 * mockup original, que troca `display` de 3 `<section>` em vez de navegar).
 */
export function Compras() {
  const vm = useCompras();

  if (vm.view.kind === 'conferencia') {
    return <ConferenciaView vm={vm} nota={vm.view.nota} fornecedor={vm.view.fornecedor} />;
  }
  if (vm.view.kind === 'fornecedor') {
    return <FornecedorView vm={vm} fornecedor={vm.view.fornecedor} />;
  }
  return (
    <div className="mx-auto max-w-6xl px-4 py-6 sm:px-6 lg:py-8">
      <ComprasHome vm={vm} />
    </div>
  );
}
