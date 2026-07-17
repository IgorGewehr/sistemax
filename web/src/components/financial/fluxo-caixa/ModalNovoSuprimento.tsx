import { useEffect, useState } from 'react';

import { Button } from '@/components/ui/Button';
import { INPUT_CLASS } from '@/components/ui/Input';
import { reais, type Centavos } from '@/lib/money';
import { cn } from '@/lib/utils';

import { FluxoModal } from './FluxoModal';

interface ModalNovoSuprimentoProps {
  open: boolean;
  origens: string[];
  onClose: () => void;
  onConfirmar: (valorCentavos: Centavos, origem: string) => void;
}

/** Registrar um reforço na gaveta — aumenta o esperado da próxima conferência (contrapartida exata
 * da sangria, ver fórmula da sessão de hoje). */
export function ModalNovoSuprimento({ open, origens, onClose, onConfirmar }: ModalNovoSuprimentoProps) {
  const [valor, setValor] = useState('');
  const [origem, setOrigem] = useState(origens[0] ?? '');

  useEffect(() => {
    if (open) {
      setValor('');
      setOrigem(origens[0] ?? '');
    }
  }, [open, origens]);

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
    onConfirmar(reais(numero), origem);
  }

  return (
    <FluxoModal open={open} onClose={onClose} eyebrow="Reforço da gaveta" title="Registrar suprimento">
      <p className="mb-4 text-[13px] leading-relaxed text-muted-foreground">
        Coloca dinheiro extra na gaveta, vindo do cofre ou do banco. Isso aumenta o valor esperado na próxima conferência.
      </p>

      <div className="mb-3.5">
        <label htmlFor="msupValor" className="mb-1.5 block text-xs font-semibold text-muted-foreground">
          Valor do suprimento
        </label>
        <input
          id="msupValor"
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
        <label htmlFor="msupOrigem" className="mb-1.5 block text-xs font-semibold text-muted-foreground">
          De onde veio
        </label>
        <select id="msupOrigem" value={origem} onChange={(e) => setOrigem(e.target.value)} className={INPUT_CLASS}>
          {origens.map((o) => (
            <option key={o} value={o}>
              {o}
            </option>
          ))}
        </select>
      </div>

      <div className="flex justify-end gap-2.5">
        <Button variant="outline" size="sm" onClick={onClose}>
          Cancelar
        </Button>
        <Button variant="primary" size="sm" disabled={!podeConfirmar} onClick={handleConfirmar}>
          Registrar suprimento
        </Button>
      </div>
    </FluxoModal>
  );
}
