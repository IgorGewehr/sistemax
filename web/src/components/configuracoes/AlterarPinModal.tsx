import { useEffect, useState } from 'react';

import { Button } from '@/components/ui/Button';
import { INPUT_CLASS as INPUT_BASE } from '@/components/ui/Input';
import { Modal } from '@/components/ui/Modal';

import { pinValido } from './calc';
import type { AlterarPinFormValues } from './types';


const INPUT_CLASS = `${INPUT_BASE} tracking-[0.3em]`;

const VAZIO: AlterarPinFormValues = { pinAtual: '', pinNovo: '', pinConfirmacao: '' };

interface AlterarPinModalProps {
  open: boolean;
  onClose: () => void;
  onSalvar: (valores: AlterarPinFormValues) => void;
}

/** Troca o PIN de acesso (login do Bridge local — 4 a 6 dígitos, mesma convenção de `lib/auth.tsx`).
 *  "PIN atual confere" é responsabilidade do backend; aqui só validamos formato e a confirmação
 *  baterem, pra não deixar o operador enviar algo obviamente errado. */
export function AlterarPinModal({ open, onClose, onSalvar }: AlterarPinModalProps) {
  const [valores, setValores] = useState<AlterarPinFormValues>(VAZIO);
  const [erro, setErro] = useState<string | null>(null);

  useEffect(() => {
    if (open) {
      setValores(VAZIO);
      setErro(null);
    }
  }, [open]);

  function handleSalvar() {
    if (!pinValido(valores.pinNovo)) {
      setErro('O novo PIN deve ter de 4 a 6 dígitos.');
      return;
    }
    if (valores.pinNovo !== valores.pinConfirmacao) {
      setErro('A confirmação não bate com o novo PIN.');
      return;
    }
    onSalvar(valores);
  }

  const podeSalvar = pinValido(valores.pinAtual) && valores.pinNovo.length > 0 && valores.pinConfirmacao.length > 0;

  return (
    <Modal open={open} onClose={onClose} title="Alterar PIN">
      <div className="mb-3.5">
        <label htmlFor="apAtual" className="mb-1.5 block text-xs font-semibold text-muted-foreground">
          PIN atual
        </label>
        <input
          id="apAtual"
          type="password"
          inputMode="numeric"
          maxLength={6}
          value={valores.pinAtual}
          onChange={(e) => setValores((v) => ({ ...v, pinAtual: e.target.value.replace(/\D/g, '') }))}
          className={INPUT_CLASS}
          autoFocus
        />
      </div>
      <div className="mb-3.5">
        <label htmlFor="apNovo" className="mb-1.5 block text-xs font-semibold text-muted-foreground">
          Novo PIN
        </label>
        <input
          id="apNovo"
          type="password"
          inputMode="numeric"
          maxLength={6}
          value={valores.pinNovo}
          onChange={(e) => {
            setValores((v) => ({ ...v, pinNovo: e.target.value.replace(/\D/g, '') }));
            setErro(null);
          }}
          className={INPUT_CLASS}
        />
      </div>
      <div className="mb-2">
        <label htmlFor="apConfirmacao" className="mb-1.5 block text-xs font-semibold text-muted-foreground">
          Confirmar novo PIN
        </label>
        <input
          id="apConfirmacao"
          type="password"
          inputMode="numeric"
          maxLength={6}
          value={valores.pinConfirmacao}
          onChange={(e) => {
            setValores((v) => ({ ...v, pinConfirmacao: e.target.value.replace(/\D/g, '') }));
            setErro(null);
          }}
          className={INPUT_CLASS}
        />
      </div>
      {erro && <p className="mb-2 text-xs text-crit">{erro}</p>}
      <div className="mt-2 flex justify-end gap-2.5">
        <Button variant="outline" size="sm" onClick={onClose}>
          Cancelar
        </Button>
        <Button variant="primary" size="sm" disabled={!podeSalvar} onClick={handleSalvar}>
          Salvar
        </Button>
      </div>
    </Modal>
  );
}
