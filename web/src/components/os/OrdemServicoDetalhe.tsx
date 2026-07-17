import { DetalheHeader } from './DetalheHeader';
import { Timeline } from './Timeline';
import type { OrdemServico } from './types';
import type { UseOrdemServico } from './useOrdemServico';

interface OrdemServicoDetalheProps {
  os: OrdemServico;
  vm: UseOrdemServico;
}

/** Tela de detalhe — o cabeçalho da OS e a linha do tempo da FSM. Página fina: só compõe. */
export function OrdemServicoDetalhe({ os, vm }: OrdemServicoDetalheProps) {
  return (
    <div>
      <DetalheHeader os={os} onVoltar={vm.voltarParaLista} onCancelar={vm.cancelar} onImprimir={vm.imprimirViaRecibo} />
      <Timeline os={os} vm={vm} />
    </div>
  );
}
