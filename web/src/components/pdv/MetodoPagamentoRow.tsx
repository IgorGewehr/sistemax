import { Banknote, CreditCard, MoreHorizontal, QrCode, Wallet } from 'lucide-react';

import type { MetodoOpcao } from './types';
import type { PdvVm } from './usePdv';

const METODOS: MetodoOpcao[] = [
  { key: 'Dinheiro', label: 'Dinheiro', icon: Banknote },
  { key: 'Debito', label: 'Débito', icon: CreditCard },
  { key: 'Credito', label: 'Crédito', icon: Wallet },
  { key: 'Pix', label: 'Pix', icon: QrCode },
  { key: 'Outro', label: 'Outros', icon: MoreHorizontal },
];

interface MetodoPagamentoRowProps {
  vm: PdvVm;
}

/** `.metodo-row` do mockup — grade das 5 formas de pagamento aceitas pelo contrato (`MetodoPagamento`). */
export function MetodoPagamentoRow({ vm }: MetodoPagamentoRowProps) {
  return (
    <div className="grid flex-none grid-cols-5 gap-2.5">
      {METODOS.map((m) => (
        <button
          key={m.key}
          type="button"
          onClick={() => vm.selecionarMetodo(m.key)}
          disabled={vm.restanteCentavos <= 0}
          className={`surface flex flex-col items-center gap-1.5 rounded-[13px] px-2 py-3 transition-colors disabled:cursor-not-allowed disabled:opacity-50 ${
            vm.metodoSelecionado === m.key ? 'border-primary bg-primary-soft' : 'hover:border-primary/50'
          }`}
        >
          <m.icon className={`h-[19px] w-[19px] ${vm.metodoSelecionado === m.key ? 'text-primary' : 'text-muted-foreground'}`} />
          <span className="text-xs font-bold text-foreground">{m.label}</span>
        </button>
      ))}
    </div>
  );
}
