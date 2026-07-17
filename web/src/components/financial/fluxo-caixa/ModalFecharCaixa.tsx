import { useEffect, useState } from 'react';

import { Button } from '@/components/ui/Button';
import { reais, type Centavos } from '@/lib/money';
import { cn } from '@/lib/utils';

import { esperadoCentavos } from './calc';
import { FluxoModal } from './FluxoModal';
import { formatCentavosWhole, MoneyWhole } from './MoneyWhole';
import type { SessaoCaixa } from './types';

interface ModalFecharCaixaProps {
  open: boolean;
  sessaoHoje: SessaoCaixa;
  onClose: () => void;
  onConfirmar: (contadoCentavos: Centavos) => void;
}

const INPUT_CLASS =
  'w-full rounded-xl border border-border bg-surface-2 px-3 py-2.5 font-mono text-sm text-foreground outline-none focus:ring-2 focus:ring-ring';

/** Fechamento cego: o operador digita o que contou ANTES de ver o esperado do sistema — só depois
 * de "Ver diferença" a comparação aparece, pra contagem não ser contaminada pelo número esperado. */
export function ModalFecharCaixa({ open, sessaoHoje, onClose, onConfirmar }: ModalFecharCaixaProps) {
  const [valor, setValor] = useState('');
  const [revelado, setRevelado] = useState(false);

  useEffect(() => {
    if (open) {
      setValor('');
      setRevelado(false);
    }
  }, [open]);

  useEffect(() => {
    if (!open) return;
    function handleKeyDown(e: KeyboardEvent) {
      if (e.key === 'Escape') onClose();
    }
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [open, onClose]);

  const numero = valor === '' ? null : Number(valor);
  const contadoCentavos = numero !== null && !Number.isNaN(numero) ? reais(numero) : null;
  const esperado = esperadoCentavos(sessaoHoje);
  const diff = contadoCentavos !== null ? contadoCentavos - esperado : null;
  const podeConfirmar = contadoCentavos !== null && contadoCentavos >= 0;

  function handleConfirmar() {
    if (!podeConfirmar || contadoCentavos === null) return;
    if (!revelado) {
      setRevelado(true);
      return;
    }
    onConfirmar(contadoCentavos);
  }

  return (
    <FluxoModal open={open} onClose={onClose} eyebrow="Fechamento cego" title="Quanto tem na gaveta?">
      <p className="mb-4 text-[13px] leading-relaxed text-muted-foreground">
        Conte o dinheiro físico agora e digite o total. Você só vê o valor esperado <b className="font-bold text-foreground">depois</b> de
        confirmar a contagem — isso evita que o esperado influencie a contagem.
      </p>

      {!revelado ? (
        <div className="mb-4">
          <label htmlFor="mfContado" className="mb-1.5 block text-xs font-semibold text-muted-foreground">
            Total contado na gaveta
          </label>
          <input
            id="mfContado"
            type="number"
            inputMode="decimal"
            min={0}
            step={1}
            placeholder="0"
            value={valor}
            onChange={(e) => setValor(e.target.value)}
            className={INPUT_CLASS}
            autoFocus
          />
        </div>
      ) : (
        <div className="mb-4 flex flex-col gap-2 rounded-xl bg-surface-2 p-3.5">
          <div className="flex items-center justify-between text-[13.5px]">
            <span className="text-muted-foreground">Esperado (sistema)</span>
            <MoneyWhole centavos={esperado} className="font-bold" />
          </div>
          <div className="flex items-center justify-between text-[13.5px]">
            <span className="text-muted-foreground">Você contou</span>
            <MoneyWhole centavos={contadoCentavos} className="font-bold" />
          </div>
          <div className="flex items-center justify-between border-t border-dashed border-border pt-2 text-[14.5px] font-bold">
            <span>Diferença</span>
            <span className={cn(diff === 0 ? 'text-muted-foreground' : diff !== null && diff > 0 ? 'text-pos' : 'text-crit')}>
              {diff === 0
                ? 'Bateu certinho'
                : diff !== null && diff > 0
                  ? `Sobra de ${formatCentavosWhole(diff)}`
                  : `Falta de ${formatCentavosWhole(Math.abs(diff ?? 0))}`}
            </span>
          </div>
        </div>
      )}

      <div className="flex justify-end gap-2.5">
        <Button variant="outline" size="sm" onClick={onClose}>
          Cancelar
        </Button>
        <Button variant="primary" size="sm" disabled={!podeConfirmar} onClick={handleConfirmar}>
          {revelado ? 'Confirmar fechamento' : 'Ver diferença'}
        </Button>
      </div>
    </FluxoModal>
  );
}
