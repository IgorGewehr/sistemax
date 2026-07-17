import { useEffect, useState } from 'react';

import { Button } from '@/components/ui/Button';
import { INPUT_CLASS } from '@/components/ui/Input';
import { Modal } from '@/components/ui/Modal';

import { formatCnpj } from './calc';
import type { Empresa, EmpresaFormValues } from './types';


interface EmpresaFormModalProps {
  open: boolean;
  empresa: Empresa;
  onClose: () => void;
  onSalvar: (valores: EmpresaFormValues) => void;
}

/** Edita os dados cadastrais da empresa — mesma origem que a seção Fiscal (futura) e o cabeçalho
 *  de documentos vão usar. Hoje é só cadastro, sem validação de Receita Federal (fora do escopo do
 *  front — isso é responsabilidade do backend/gateway fiscal). */
export function EmpresaFormModal({ open, empresa, onClose, onSalvar }: EmpresaFormModalProps) {
  const [valores, setValores] = useState<EmpresaFormValues>(empresa);

  useEffect(() => {
    if (open) setValores(empresa);
  }, [open, empresa]);

  const podeSalvar = valores.nome.trim().length > 0 && valores.cnpj.replace(/\D/g, '').length === 14;

  return (
    <Modal open={open} onClose={onClose} title="Editar empresa" className="max-w-lg">
      <div className="max-h-[72vh] overflow-y-auto pr-1 -mr-1">
        <div className="mb-3.5 grid grid-cols-1 gap-3 sm:grid-cols-2">
          <div>
            <label htmlFor="efNomeFantasia" className="mb-1.5 block text-xs font-semibold text-muted-foreground">
              Nome fantasia
            </label>
            <input
              id="efNomeFantasia"
              type="text"
              value={valores.nomeFantasia}
              onChange={(e) => setValores((v) => ({ ...v, nomeFantasia: e.target.value }))}
              className={INPUT_CLASS}
              autoFocus
            />
          </div>
          <div>
            <label htmlFor="efCnpj" className="mb-1.5 block text-xs font-semibold text-muted-foreground">
              CNPJ
            </label>
            <input
              id="efCnpj"
              type="text"
              value={valores.cnpj}
              maxLength={18}
              onChange={(e) => setValores((v) => ({ ...v, cnpj: formatCnpj(e.target.value) }))}
              className={INPUT_CLASS}
            />
          </div>
        </div>

        <div className="mb-3.5">
          <label htmlFor="efNome" className="mb-1.5 block text-xs font-semibold text-muted-foreground">
            Razão social
          </label>
          <input
            id="efNome"
            type="text"
            value={valores.nome}
            onChange={(e) => setValores((v) => ({ ...v, nome: e.target.value }))}
            className={INPUT_CLASS}
          />
        </div>

        <div className="mb-3.5 grid grid-cols-1 gap-3 sm:grid-cols-2">
          <div>
            <label htmlFor="efTelefone" className="mb-1.5 block text-xs font-semibold text-muted-foreground">
              Telefone
            </label>
            <input
              id="efTelefone"
              type="text"
              value={valores.telefone}
              onChange={(e) => setValores((v) => ({ ...v, telefone: e.target.value }))}
              className={INPUT_CLASS}
            />
          </div>
          <div>
            <label htmlFor="efEmail" className="mb-1.5 block text-xs font-semibold text-muted-foreground">
              Email
            </label>
            <input
              id="efEmail"
              type="email"
              value={valores.email}
              onChange={(e) => setValores((v) => ({ ...v, email: e.target.value }))}
              className={INPUT_CLASS}
            />
          </div>
        </div>

        <div className="mb-3.5">
          <label htmlFor="efLogradouro" className="mb-1.5 block text-xs font-semibold text-muted-foreground">
            Endereço
          </label>
          <input
            id="efLogradouro"
            type="text"
            placeholder="Rua, número"
            value={valores.endereco.logradouro}
            onChange={(e) => setValores((v) => ({ ...v, endereco: { ...v.endereco, logradouro: e.target.value } }))}
            className={INPUT_CLASS}
          />
        </div>

        <div className="mb-4 grid grid-cols-2 gap-3 sm:grid-cols-4">
          <input
            type="text"
            placeholder="Bairro"
            value={valores.endereco.bairro}
            onChange={(e) => setValores((v) => ({ ...v, endereco: { ...v.endereco, bairro: e.target.value } }))}
            className={INPUT_CLASS}
          />
          <input
            type="text"
            placeholder="Cidade"
            value={valores.endereco.cidade}
            onChange={(e) => setValores((v) => ({ ...v, endereco: { ...v.endereco, cidade: e.target.value } }))}
            className={INPUT_CLASS}
          />
          <input
            type="text"
            placeholder="UF"
            maxLength={2}
            value={valores.endereco.uf}
            onChange={(e) => setValores((v) => ({ ...v, endereco: { ...v.endereco, uf: e.target.value.toUpperCase() } }))}
            className={INPUT_CLASS}
          />
          <input
            type="text"
            placeholder="CEP"
            value={valores.endereco.cep}
            onChange={(e) => setValores((v) => ({ ...v, endereco: { ...v.endereco, cep: e.target.value } }))}
            className={INPUT_CLASS}
          />
        </div>
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
