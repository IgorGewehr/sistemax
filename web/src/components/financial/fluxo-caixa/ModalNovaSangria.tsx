import { useEffect, useState } from 'react';

import { Button } from '@/components/ui/Button';
import { INPUT_CLASS } from '@/components/ui/Input';
import { reais, type Centavos } from '@/lib/money';
import { cn } from '@/lib/utils';

import { FluxoModal } from './FluxoModal';

interface ModalNovaSangriaProps {
  open: boolean;
  destinos: string[];
  onClose: () => void;
  onConfirmar: (valorCentavos: Centavos, destino: string) => void;
}

/** Registrar uma retirada da gaveta — reduz o esperado da próxima conferência (ver fórmula da
 * sessão de hoje). */
export function ModalNovaSangria({ open, destinos, onClose, onConfirmar }: ModalNovaSangriaProps) {
  const [valor, setValor] = useState('');
  const [destino, setDestino] = useState(destinos[0] ?? '');

  useEffect(() => {
    if (open) {
      setValor('');
      setDestino(destinos[0] ?? '');
    }
  }, [open, destinos]);

  useEffect(() => {
    if (!open) return;
    function handleKeyDown(e: KeyboardEvent) {
      if (e.key === 'Escape') onClose();
    }
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [open, onClose]);

  const numero = valor === '' ? null : Number(valor);
  const podeConfirmar = numero !== null && !Number.isNaN(numero) && numero > 0;

  function handleConfirmar() {
    if (!podeConfirmar || numero === null) return;
    onConfirmar(reais(numero), destino);
  }

  return (
    <FluxoModal open={open} onClose={onClose} eyebrow="Retirada da gaveta" title="Registrar sangria">
      <p className="mb-4 text-[13px] leading-relaxed text-muted-foreground">
        Tira dinheiro da gaveta e manda pro cofre ou pro banco. Isso reduz o valor esperado na próxima conferência.
      </p>

      <div className="mb-3.5">
        <label htmlFor="msValor" className="mb-1.5 block text-xs font-semibold text-muted-foreground">
          Valor da sangria
        </label>
        <input
          id="msValor"
          type="number"
          inputMode="decimal"
          min={0}
          step={1}
          placeholder="0"
          value={valor}
          onChange={(e) => setValor(e.target.value)}
          className={cn(INPUT_CLASS, 'font-mono')}
          autoFocus
        />
      </div>

      <div className="mb-4">
        <label htmlFor="msDestino" className="mb-1.5 block text-xs font-semibold text-muted-foreground">
          Foi para onde
        </label>
        <select id="msDestino" value={destino} onChange={(e) => setDestino(e.target.value)} className={INPUT_CLASS}>
          {destinos.map((d) => (
            <option key={d} value={d}>
              {d}
            </option>
          ))}
        </select>
      </div>

      <div className="flex justify-end gap-2.5">
        <Button variant="outline" size="sm" onClick={onClose}>
          Cancelar
        </Button>
        <Button variant="primary" size="sm" disabled={!podeConfirmar} onClick={handleConfirmar}>
          Registrar sangria
        </Button>
      </div>
    </FluxoModal>
  );
}
