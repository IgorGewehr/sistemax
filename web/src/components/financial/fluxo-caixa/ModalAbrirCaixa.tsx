import { useEffect, useState } from 'react';

import { Button } from '@/components/ui/Button';
import { INPUT_CLASS } from '@/components/ui/Input';
import { reais, type Centavos } from '@/lib/money';
import { cn } from '@/lib/utils';

import { FluxoModal } from './FluxoModal';

interface ModalAbrirCaixaProps {
  open: boolean;
  onClose: () => void;
  onConfirmar: (aberturaCentavos: Centavos, operadorNome: string) => void;
  enviando?: boolean;
}

/**
 * Abre a gaveta com o fundo de troco — ação nova desta rodada (não existia UI nenhuma pra
 * `POST /financeiro/caixa/abrir` antes). Pede o nome de quem está abrindo porque o Bridge local
 * (`lib/api/client.ts`) não expõe identidade do usuário logado na sessão (token opaco, sem claim
 * de nome) — ver `useFluxoCaixa.ts` sobre o limite disso.
 */
export function ModalAbrirCaixa({ open, onClose, onConfirmar, enviando = false }: ModalAbrirCaixaProps) {
  const [valor, setValor] = useState('');
  const [operador, setOperador] = useState('');

  useEffect(() => {
    if (open) {
      setValor('');
      setOperador('');
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
  const podeConfirmar = numero !== null && !Number.isNaN(numero) && numero >= 0 && operador.trim().length > 0 && !enviando;

  function handleConfirmar() {
    if (!podeConfirmar || numero === null) return;
    onConfirmar(reais(numero), operador.trim());
  }

  return (
    <FluxoModal open={open} onClose={onClose} eyebrow="Abertura de turno" title="Abrir caixa">
      <p className="mb-4 text-[13px] leading-relaxed text-muted-foreground">
        Conte o fundo de troco que vai pra gaveta e informe quem está abrindo o turno.
      </p>

      <div className="mb-3.5">
        <label htmlFor="macValor" className="mb-1.5 block text-xs font-semibold text-muted-foreground">
          Fundo de troco
        </label>
        <input
          id="macValor"
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
        <label htmlFor="macOperador" className="mb-1.5 block text-xs font-semibold text-muted-foreground">
          Operador
        </label>
        <input
          id="macOperador"
          type="text"
          placeholder="Seu nome"
          value={operador}
          onChange={(e) => setOperador(e.target.value)}
          className={INPUT_CLASS}
        />
      </div>

      <div className="flex justify-end gap-2.5">
        <Button variant="outline" size="sm" onClick={onClose}>
          Cancelar
        </Button>
        <Button variant="primary" size="sm" disabled={!podeConfirmar} onClick={handleConfirmar}>
          {enviando ? 'Abrindo…' : 'Abrir caixa'}
        </Button>
      </div>
    </FluxoModal>
  );
}
