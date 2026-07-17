import { useEffect, useState } from 'react';

import { Button } from '@/components/ui/Button';
import { INPUT_CLASS } from '@/components/ui/Input';
import { Modal } from '@/components/ui/Modal';
import { ehIntocavel, PAPEIS, PAPEL_LABEL, type Acao, type Modulo, type Papel, type Usuario } from '@/lib/permissions';
import { cn } from '@/lib/utils';

import { alternarCelula } from './calc';
import { PermissoesGrid } from './PermissoesGrid';
import type { UsuarioFormValues } from './types';


const VAZIO: UsuarioFormValues = { nome: '', email: '', telefone: '', papel: 'operator', overrides: [] };

function usuarioParaFormValues(usuario: Usuario): UsuarioFormValues {
  return { nome: usuario.nome, email: usuario.email, telefone: usuario.telefone ?? '', papel: usuario.papel, overrides: usuario.overrides };
}

interface UsuarioFormModalProps {
  open: boolean;
  modo: 'criar' | 'editar';
  /** Só relevante em `modo === 'editar'` — pré-preenche o formulário. */
  usuarioEmEdicao: Usuario | undefined;
  /** Papel de quem está administrando (sessão efetiva, já refletindo auto-edição salva) — decide
   *  se `founder` aparece como opção do seletor e se o papel do usuário em edição pode ser trocado. */
  sessaoPapel: Papel;
  onClose: () => void;
  onSalvar: (valores: UsuarioFormValues) => void;
}

/**
 * Modal de criar/editar usuário — único formulário pros dois modos, acionado por "+ Novo usuário"
 * e pelo ícone de editar da tabela. O papel escolhido define o padrão de permissões; o grid abaixo
 * mostra o EFETIVO (padrão + overrides) e cada toggle grava só o diff (`alternarCelula`, `calc.ts`).
 * Trocar de papel reseta os overrides: eles foram desenhados contra o padrão do papel anterior —
 * mantê-los "soltos" ao trocar de papel ligaria/desligaria permissões sem o operador perceber a
 * origem.
 *
 * Founder é intocável (`ehIntocavel`, `lib/permissions.ts`) — só outro founder concede ou remove
 * esse papel: quem não é founder (i) não vê "Fundador" como opção do seletor (não minta founders
 * nem se autopromove) e (ii) não consegue mexer no seletor de papel do usuário que JÁ é founder
 * (`papelTravado`). O backstop real é `papelResolvidoParaSalvar` em `useConfiguracoes.ts` — este
 * bloqueio de UI é só pra não deixar o operador tentar algo que vai ser revertido sem aviso.
 */
export function UsuarioFormModal({ open, modo, usuarioEmEdicao, sessaoPapel, onClose, onSalvar }: UsuarioFormModalProps) {
  const [valores, setValores] = useState<UsuarioFormValues>(VAZIO);

  useEffect(() => {
    if (!open) return;
    setValores(modo === 'editar' && usuarioEmEdicao ? usuarioParaFormValues(usuarioEmEdicao) : VAZIO);
  }, [open, modo, usuarioEmEdicao]);

  function onTrocarPapel(papel: Papel) {
    setValores((v) => ({ ...v, papel, overrides: [] }));
  }

  function onToggleCelula(modulo: Modulo, acao: Acao) {
    setValores((v) => ({ ...v, overrides: alternarCelula(v.papel, v.overrides, modulo, acao) }));
  }

  const alvoIntocavel = modo === 'editar' && usuarioEmEdicao !== undefined && ehIntocavel(usuarioEmEdicao);
  const papelTravado = alvoIntocavel && sessaoPapel !== 'founder';
  // `founder` só some da lista quando o ator não é founder E o valor atual do form não é founder —
  // se o alvo já é founder, mantém a opção presente (o seletor está `disabled`, mas o valor
  // selecionado precisa continuar existindo entre as opções).
  const papeisDisponiveis = PAPEIS.filter((p) => p !== 'founder' || sessaoPapel === 'founder' || valores.papel === 'founder');

  const podeSalvar = valores.nome.trim().length > 0 && valores.email.trim().length > 0;

  return (
    <Modal open={open} onClose={onClose} title={modo === 'editar' ? 'Editar usuário' : 'Novo usuário'} className="max-w-xl">
      <div className="max-h-[72vh] overflow-y-auto pr-1 -mr-1">
        <div className="mb-3.5 grid grid-cols-1 gap-3 sm:grid-cols-2">
          <div>
            <label htmlFor="ufNome" className="mb-1.5 block text-xs font-semibold text-muted-foreground">
              Nome
            </label>
            <input
              id="ufNome"
              type="text"
              placeholder="Nome completo"
              value={valores.nome}
              onChange={(e) => setValores((v) => ({ ...v, nome: e.target.value }))}
              className={INPUT_CLASS}
              autoFocus
            />
          </div>
          <div>
            <label htmlFor="ufEmail" className="mb-1.5 block text-xs font-semibold text-muted-foreground">
              Email
            </label>
            <input
              id="ufEmail"
              type="email"
              placeholder="nome@empresa.com"
              value={valores.email}
              onChange={(e) => setValores((v) => ({ ...v, email: e.target.value }))}
              className={INPUT_CLASS}
            />
          </div>
        </div>

        <div className="mb-3.5 grid grid-cols-1 gap-3 sm:grid-cols-2">
          <div>
            <label htmlFor="ufTelefone" className="mb-1.5 block text-xs font-semibold text-muted-foreground">
              Telefone
            </label>
            <input
              id="ufTelefone"
              type="text"
              placeholder="(11) 90000-0000"
              value={valores.telefone}
              onChange={(e) => setValores((v) => ({ ...v, telefone: e.target.value }))}
              className={INPUT_CLASS}
            />
          </div>
          <div>
            <label htmlFor="ufPapel" className="mb-1.5 block text-xs font-semibold text-muted-foreground">
              Papel
            </label>
            <select
              id="ufPapel"
              value={valores.papel}
              disabled={papelTravado}
              onChange={(e) => onTrocarPapel(e.target.value as Papel)}
              className={cn(INPUT_CLASS, 'appearance-none', papelTravado && 'cursor-not-allowed opacity-60')}
            >
              {papeisDisponiveis.map((papel) => (
                <option key={papel} value={papel}>
                  {PAPEL_LABEL[papel]}
                </option>
              ))}
            </select>
            {papelTravado && <p className="mt-1 text-2xs text-muted-foreground">Só o fundador pode alterar o papel do fundador.</p>}
          </div>
        </div>

        <div className="mb-1.5 flex items-center justify-between">
          <span className="text-xs font-semibold text-muted-foreground">Permissões por módulo</span>
          {valores.overrides.length > 0 && (
            <span className="text-2xs font-semibold text-primary-600">{valores.overrides.length} personalizada(s)</span>
          )}
        </div>
        <PermissoesGrid papel={valores.papel} overrides={valores.overrides} onToggle={onToggleCelula} />
      </div>

      <div className="mt-4 flex justify-end gap-2.5">
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
