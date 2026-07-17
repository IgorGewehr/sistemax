import { ArrowLeft } from 'lucide-react';

import { MoneyValue } from '@/components/shared';
import { Button } from '@/components/ui/Button';
import { Kbd } from '@/components/ui/Kbd';

import { CartPanel } from './CartPanel';
import { MetodoDetailCard } from './MetodoDetailCard';
import { MetodoPagamentoRow } from './MetodoPagamentoRow';
import { PagamentosList } from './PagamentosList';
import type { PdvVm } from './usePdv';

interface PagamentoScreenProps {
  vm: PdvVm;
}

/** Tela 2 — pagamento (`#screenPagamento` do mockup): restante + formas de pagamento à esquerda, resumo da venda + pagamentos já registrados à direita. */
export function PagamentoScreen({ vm }: PagamentoScreenProps) {
  if (!vm.venda) return null;
  const restanteZerado = vm.restanteCentavos <= 0;
  const podeFinalizar = restanteZerado && vm.venda.itens.length > 0;

  return (
    <div className="grid min-h-0 flex-1 grid-cols-1 gap-4 lg:grid-cols-[1.55fr_1fr]">
      <div className="flex min-h-0 flex-col gap-4">
        <div className="flex flex-none items-center gap-2.5">
          <button
            type="button"
            onClick={vm.voltarParaVenda}
            title="Voltar (Esc)"
            className="flex h-7 w-7 items-center justify-center rounded-[9px] bg-surface-2 text-foreground transition-colors hover:bg-primary-soft hover:text-primary"
          >
            <ArrowLeft className="h-4 w-4" />
          </button>
          <h2 className="text-base font-bold text-foreground">Pagamento</h2>
        </div>

        <div className="surface flex-none rounded-2xl px-6 py-5 text-center">
          <div className="text-xs font-bold uppercase tracking-wide text-muted-foreground">Restante</div>
          <MoneyValue
            centavos={vm.restanteCentavos}
            className={`mt-1.5 block text-[44px] font-extrabold tracking-tight ${restanteZerado ? 'text-pos' : 'text-foreground'}`}
          />
        </div>

        <MetodoPagamentoRow vm={vm} />
        <MetodoDetailCard vm={vm} />
      </div>

      <div className="flex min-h-0 flex-col gap-3.5 overflow-y-auto">
        <CartPanel venda={vm.venda} readonly />
        <PagamentosList pagamentos={vm.venda.pagamentos} />

        <Button
          variant="primary"
          size="touch"
          className="mt-auto w-full justify-between"
          disabled={!podeFinalizar || vm.finalizando}
          onClick={() => void vm.finalizarVenda()}
        >
          {vm.finalizando ? 'Concluindo…' : 'Finalizar venda'}
          {!vm.finalizando && <Kbd className="bg-white/15 text-white">F12</Kbd>}
        </Button>
      </div>
    </div>
  );
}
