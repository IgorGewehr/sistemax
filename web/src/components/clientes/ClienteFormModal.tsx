import { useEffect, useState } from 'react';

import { Button } from '@/components/ui/Button';
import { INPUT_CLASS } from '@/components/ui/Input';
import { Modal } from '@/components/ui/Modal';
import { cn } from '@/lib/utils';

import { parseAniversario } from './calc';
import type { Cliente, ClienteFormValues } from './types';


const VAZIO: ClienteFormValues = {
  nome: '',
  telefone: '',
  email: '',
  aniversario: '',
  enderecoResumo: '',
  observacoes: '',
  tags: [],
};

function clienteParaFormValues(cliente: Cliente): ClienteFormValues {
  return {
    nome: cliente.nome,
    telefone: cliente.telefone ?? '',
    email: cliente.email ?? '',
    aniversario: cliente.aniversario ?? '',
    enderecoResumo: cliente.enderecoResumo ?? '',
    observacoes: cliente.observacoes ?? '',
    tags: cliente.tags,
  };
}

interface ClienteFormModalProps {
  open: boolean;
  modo: 'criar' | 'editar';
  /** Só relevante em `modo === 'editar'` — pré-preenche o formulário. */
  clienteEmEdicao: Cliente | undefined;
  onClose: () => void;
  onSalvar: (valores: ClienteFormValues) => void;
}

/** Modal de criar/editar cliente — único formulário pros dois modos, acionado por "+ Novo cliente"
 *  (Home) e "Editar" (Ficha). Campos derivados (compras, gastos, status) não aparecem aqui — são
 *  computados/servidor, nunca editáveis à mão. */
export function ClienteFormModal({ open, modo, clienteEmEdicao, onClose, onSalvar }: ClienteFormModalProps) {
  const [valores, setValores] = useState<ClienteFormValues>(VAZIO);
  const [tagsTexto, setTagsTexto] = useState('');
  const [erroAniversario, setErroAniversario] = useState(false);

  useEffect(() => {
    if (!open) return;
    const iniciais = modo === 'editar' && clienteEmEdicao ? clienteParaFormValues(clienteEmEdicao) : VAZIO;
    setValores(iniciais);
    setTagsTexto(iniciais.tags.join(', '));
    setErroAniversario(false);
  }, [open, modo, clienteEmEdicao]);

  function handleSalvar() {
    const aniversarioNormalizado = parseAniversario(valores.aniversario);
    if (aniversarioNormalizado === undefined) {
      setErroAniversario(true);
      return;
    }
    const tags = tagsTexto
      .split(',')
      .map((t) => t.trim())
      .filter(Boolean);
    onSalvar({ ...valores, aniversario: aniversarioNormalizado ?? '', tags });
  }

  const podeSalvar = valores.nome.trim().length > 0;

  return (
    <Modal open={open} onClose={onClose} title={modo === 'editar' ? 'Editar cliente' : 'Novo cliente'}>
      <div className="mb-3.5">
        <label htmlFor="cfNome" className="mb-1.5 block text-xs font-semibold text-muted-foreground">
          Nome
        </label>
        <input
          id="cfNome"
          type="text"
          placeholder="Nome completo"
          value={valores.nome}
          onChange={(e) => setValores((v) => ({ ...v, nome: e.target.value }))}
          className={INPUT_CLASS}
          autoFocus
        />
      </div>

      <div className="mb-3.5 grid grid-cols-1 gap-3 sm:grid-cols-2">
        <div>
          <label htmlFor="cfTelefone" className="mb-1.5 block text-xs font-semibold text-muted-foreground">
            Telefone
          </label>
          <input
            id="cfTelefone"
            type="text"
            placeholder="(11) 90000-0000"
            value={valores.telefone}
            onChange={(e) => setValores((v) => ({ ...v, telefone: e.target.value }))}
            className={INPUT_CLASS}
          />
        </div>
        <div>
          <label htmlFor="cfEmail" className="mb-1.5 block text-xs font-semibold text-muted-foreground">
            Email
          </label>
          <input
            id="cfEmail"
            type="email"
            placeholder="nome@email.com"
            value={valores.email}
            onChange={(e) => setValores((v) => ({ ...v, email: e.target.value }))}
            className={INPUT_CLASS}
          />
        </div>
      </div>

      <div className="mb-3.5">
        <label htmlFor="cfAniversario" className="mb-1.5 block text-xs font-semibold text-muted-foreground">
          Aniversário (DD/MM)
        </label>
        <input
          id="cfAniversario"
          type="text"
          placeholder="16/07"
          value={valores.aniversario}
          onChange={(e) => {
            setValores((v) => ({ ...v, aniversario: e.target.value }));
            setErroAniversario(false);
          }}
          className={cn(INPUT_CLASS, erroAniversario && 'ring-2 ring-crit')}
        />
        {erroAniversario && <p className="mt-1 text-xs text-crit">Data inválida — use o formato DD/MM (ex.: 16/07).</p>}
      </div>

      <div className="mb-3.5">
        <label htmlFor="cfEndereco" className="mb-1.5 block text-xs font-semibold text-muted-foreground">
          Endereço (resumo)
        </label>
        <input
          id="cfEndereco"
          type="text"
          placeholder="Bairro, cidade"
          value={valores.enderecoResumo}
          onChange={(e) => setValores((v) => ({ ...v, enderecoResumo: e.target.value }))}
          className={INPUT_CLASS}
        />
      </div>

      <div className="mb-3.5">
        <label htmlFor="cfTags" className="mb-1.5 block text-xs font-semibold text-muted-foreground">
          Tags (separadas por vírgula)
        </label>
        <input
          id="cfTags"
          type="text"
          placeholder="vip, atacado"
          value={tagsTexto}
          onChange={(e) => setTagsTexto(e.target.value)}
          className={INPUT_CLASS}
        />
      </div>

      <div className="mb-4">
        <label htmlFor="cfObservacoes" className="mb-1.5 block text-xs font-semibold text-muted-foreground">
          Observações
        </label>
        <textarea
          id="cfObservacoes"
          rows={3}
          placeholder="Preferências, histórico, contexto…"
          value={valores.observacoes}
          onChange={(e) => setValores((v) => ({ ...v, observacoes: e.target.value }))}
          className={cn(INPUT_CLASS, 'resize-none')}
        />
      </div>

      <div className="flex justify-end gap-2.5">
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
