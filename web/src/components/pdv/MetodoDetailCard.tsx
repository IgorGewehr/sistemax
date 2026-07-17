import { Button } from '@/components/ui/Button';
import { formatCentavos } from '@/lib/money';

import type { PdvVm } from './usePdv';

const SUGESTOES_DINHEIRO = [
  { label: 'exato', centavos: null },
  { label: 'R$ 50', centavos: 5000 },
  { label: 'R$ 100', centavos: 10000 },
  { label: 'R$ 200', centavos: 20000 },
];

interface MetodoDetailCardProps {
  vm: PdvVm;
}

/** `.metodo-detail` do mockup — campo de valor (+ recebido/troco quando Dinheiro) da forma escolhida. */
export function MetodoDetailCard({ vm }: MetodoDetailCardProps) {
  if (!vm.metodoSelecionado) {
    return (
      <div className="surface flex flex-1 items-center justify-center rounded-2xl p-4">
        <p className="text-sm text-faint">Selecione uma forma de pagamento acima.</p>
      </div>
    );
  }

  return (
    <div className="surface flex flex-1 flex-col gap-3 overflow-y-auto rounded-2xl p-4">
      <div>
        <div className="mb-1.5 text-[11.5px] font-bold uppercase tracking-wide text-muted-foreground">Valor do pagamento</div>
        <div className="flex items-center gap-2.5 border-b-2 border-border pb-1 focus-within:border-primary">
          <span className="num text-[22px] font-bold text-faint">R$</span>
          <input
            type="text"
            inputMode="decimal"
            value={vm.valorCampoInput}
            onChange={(e) => vm.setValorCampoInput(e.target.value)}
            className="num w-full border-0 bg-transparent text-[26px] font-bold text-foreground outline-none"
          />
        </div>
      </div>

      {vm.metodoSelecionado === 'Dinheiro' && (
        <>
          <div>
            <div className="mb-1.5 text-[11.5px] font-bold uppercase tracking-wide text-muted-foreground">Valor recebido</div>
            <div className="flex items-center gap-2.5 border-b-2 border-border pb-1 focus-within:border-primary">
              <span className="num text-[22px] font-bold text-faint">R$</span>
              <input
                type="text"
                inputMode="decimal"
                value={vm.recebidoCampoInput}
                onChange={(e) => vm.setRecebidoCampoInput(e.target.value)}
                className="num w-full border-0 bg-transparent text-[26px] font-bold text-foreground outline-none"
              />
            </div>
          </div>

          <div className="flex flex-wrap gap-2">
            {SUGESTOES_DINHEIRO.map((s) => (
              <button
                key={s.label}
                type="button"
                onClick={() => vm.aplicarSugestaoRecebido(s.centavos ?? vm.valorCampoCentavos)}
                className="num rounded-[9px] border border-border bg-surface-2 px-3 py-1.5 text-[12.5px] font-bold text-foreground hover:bg-primary-soft hover:text-primary"
              >
                {s.label}
              </button>
            ))}
          </div>

          <div className="flex items-baseline justify-between rounded-xl bg-pos-soft px-3.5 py-3">
            <span className="text-xs font-bold uppercase tracking-wide text-pos">Troco</span>
            <span className="num text-2xl font-extrabold text-pos">{formatCentavos(vm.trocoPreview)}</span>
          </div>
        </>
      )}

      <Button
        variant="primary"
        size="touch"
        className="mt-auto w-full"
        disabled={vm.processandoPagamento || vm.valorCampoCentavos <= 0}
        onClick={() => void vm.confirmarPagamento()}
      >
        {vm.processandoPagamento ? 'Registrando…' : `Adicionar ${formatCentavos(vm.valorCampoCentavos)}`}
      </Button>
    </div>
  );
}
