import { useEffect, useState } from 'react';

import { Button } from '@/components/ui/Button';
import { INPUT_CLASS } from '@/components/ui/Input';
import { Modal } from '@/components/ui/Modal';
import type { Usuario } from '@/lib/permissions';

import { emailValido } from './calc';
import type { PerfilFormValues } from './types';


interface PerfilEditModalProps {
  open: boolean;
  usuario: Usuario;
  onClose: () => void;
  onSalvar: (valores: PerfilFormValues) => void;
}

/** Edita nome/email/telefone do próprio usuário logado — papel e status ficam de fora: isso só
 *  quem administra usuários muda, na seção "Usuários & Permissões", nunca na própria edição de
 *  perfil (evita o usuário comum se auto-promover). */
export function PerfilEditModal({ open, usuario, onClose, onSalvar }: PerfilEditModalProps) {
  const [valores, setValores] = useState<PerfilFormValues>({ nome: usuario.nome, email: usuario.email, telefone: usuario.telefone ?? '' });

  useEffect(() => {
    if (open) setValores({ nome: usuario.nome, email: usuario.email, telefone: usuario.telefone ?? '' });
  }, [open, usuario]);

  const podeSalvar = valores.nome.trim().length > 0 && emailValido(valores.email);

  return (
    <Modal open={open} onClose={onClose} title="Editar perfil">
      <div className="mb-3.5">
        <label htmlFor="peNome" className="mb-1.5 block text-xs font-semibold text-muted-foreground">
          Nome
        </label>
        <input
          id="peNome"
          type="text"
          value={valores.nome}
          onChange={(e) => setValores((v) => ({ ...v, nome: e.target.value }))}
          className={INPUT_CLASS}
          autoFocus
        />
      </div>
      <div className="mb-3.5">
        <label htmlFor="peEmail" className="mb-1.5 block text-xs font-semibold text-muted-foreground">
          Email
        </label>
        <input
          id="peEmail"
          type="email"
          value={valores.email}
          onChange={(e) => setValores((v) => ({ ...v, email: e.target.value }))}
          className={INPUT_CLASS}
        />
      </div>
      <div className="mb-4">
        <label htmlFor="peTelefone" className="mb-1.5 block text-xs font-semibold text-muted-foreground">
          Telefone
        </label>
        <input
          id="peTelefone"
          type="text"
          value={valores.telefone}
          onChange={(e) => setValores((v) => ({ ...v, telefone: e.target.value }))}
          className={INPUT_CLASS}
        />
      </div>
      <div className="flex justify-end gap-2.5">
        <Button variant="outline" size="sm" onClick={onClose}>
          Cancelar
        </Button>
        <Button variant="primary" size="sm" disabled={!podeSalvar} onClick={() => onSalvar(valores)}>
          Salvar
        </Button>
      </div>
    </Modal>
  );
}
