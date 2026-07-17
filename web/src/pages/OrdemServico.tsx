import { OrdemServicoDetalhe } from '@/components/os/OrdemServicoDetalhe';
import { OrdemServicoLista } from '@/components/os/OrdemServicoLista';
import { useOrdemServico } from '@/components/os/useOrdemServico';

/**
 * Ordem de Serviço — reprodução 1:1 de `docs/ui/mockups/ordem-servico.html`. Página fina: só
 * decide entre a lista e o detalhe (a mesma troca de "tela" que o mockup faz via `telaAtual`);
 * todo o estado/derivação vive em `useOrdemServico`.
 */
export function OrdemServico() {
  const vm = useOrdemServico();

  return (
    <div className="mx-auto max-w-6xl px-4 py-6 sm:px-6 lg:py-8">
      {vm.tela === 'lista' || !vm.osSelecionada ? <OrdemServicoLista vm={vm} /> : <OrdemServicoDetalhe os={vm.osSelecionada} vm={vm} />}
    </div>
  );
}
