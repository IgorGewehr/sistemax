import { useMemo, useState } from 'react';

import {
  ehIntocavel,
  modulosVisiveisDe,
  podeAdministrarUsuarios as papelPodeAdministrar,
  useSessaoPermissoes,
  usuarioPode,
  usuarioPodeVer,
  type Acao,
  type Modulo,
  type SessaoPermissoes,
  type Usuario,
} from '@/lib/permissions';
import { CONFIGURACOES_MOCK } from '@/mocks/configuracoes';

import { papelResolvidoParaSalvar, usuarioById } from './calc';
import type {
  AlterarPinFormValues,
  Empresa,
  EmpresaFormValues,
  PerfilFormValues,
  SecaoConfiguracoes,
  UsuarioFormValues,
} from './types';

type ModalUsuarioForm = { modo: 'criar' } | { modo: 'editar'; usuarioId: string };

/** "Hoje" fixo do cenário mock — mesma convenção de `DATA_HOJE`/`hojeLabel` em
 *  `useCompras.ts`/`useClientes.ts`. Carimba `criadoEm` de um usuário novo. */
const HOJE_LABEL = '17/07/2026';

/**
 * Todo o estado de "Configurações" vive aqui — `Configuracoes.tsx`/`ConfiguracoesHome.tsx`/seções
 * ficam finos, só compondo a partir do view-model que este hook devolve. Espelha `useClientes.ts`/
 * `useCompras.ts`.
 *
 * A visibilidade de SEÇÃO (`secoesVisiveis`) é o mesmo princípio de RBAC que Sidebar/Dashboard
 * (pretendem) aplicar a módulo inteiro (`lib/permissions.ts`), um nível abaixo: "Perfil" é sempre
 * visível (é o próprio usuário editando a si mesmo); as demais exigem `configuracoes:ver`; "Usuários
 * & Permissões" exige especificamente `configuracoes:gerenciarUsuarios`.
 */
export function useConfiguracoes() {
  const sessao = useSessaoPermissoes();

  const [secaoAtiva, setSecaoAtiva] = useState<SecaoConfiguracoes>('perfil');
  const [empresa, setEmpresa] = useState<Empresa>(CONFIGURACOES_MOCK.empresa);
  const [usuarios, setUsuarios] = useState<Usuario[]>(CONFIGURACOES_MOCK.usuarios);
  const [editandoEmpresa, setEditandoEmpresa] = useState(false);
  const [modalUsuarioForm, setModalUsuarioForm] = useState<ModalUsuarioForm | null>(null);
  const [confirmandoStatusId, setConfirmandoStatusId] = useState<string | null>(null);

  // "Usuário logado" pro Perfil = o mesmo da sessão (lib/permissions.ts), buscado na lista
  // completa pra refletir edições já salvas (a sessão mockada não muda de referência sozinha).
  const usuarioSessao = usuarioById(usuarios, sessao.usuarioAtual.id) ?? sessao.usuarioAtual;

  // Sessão EFETIVA: recalculada a partir de `usuarioSessao` (já reflete papel/overrides salvos),
  // nunca do `sessao` mockado cru. Sem isso, um admin que edita o PRÓPRIO papel pela tela de
  // Usuários veria o badge de "Meu perfil" mudar na hora, mas `secoesVisiveis`/`podeEditarEmpresa`/
  // `podeAdministrarUsuarios` (abaixo) ficariam presos ao papel/overrides ORIGINAIS até recarregar a
  // página — gate de UI e dado exibido dessincronizados na mesma sessão.
  const sessaoEfetiva = useMemo<SessaoPermissoes>(
    () => ({
      usuarioAtual: usuarioSessao,
      papel: usuarioSessao.papel,
      pode: (modulo: Modulo, acao: Acao) => usuarioPode(usuarioSessao, modulo, acao),
      podeVer: (modulo: Modulo) => usuarioPodeVer(usuarioSessao, modulo),
      modulosVisiveis: modulosVisiveisDe(usuarioSessao),
      podeAdministrarUsuarios: papelPodeAdministrar(usuarioSessao.papel),
    }),
    [usuarioSessao],
  );

  const secoesVisiveis = useMemo<SecaoConfiguracoes[]>(() => {
    const base: SecaoConfiguracoes[] = ['perfil'];
    if (sessaoEfetiva.pode('configuracoes', 'ver')) base.push('empresa', 'fiscal', 'integracoes');
    if (sessaoEfetiva.pode('configuracoes', 'gerenciarUsuarios')) base.push('usuarios');
    return base;
  }, [sessaoEfetiva]);

  // Se a seção ativa deixar de estar na lista visível (ex.: sessão trocasse de usuário), volta pro
  // Perfil — nunca deixa a tela "presa" numa seção que sumiu do menu.
  const secaoResolvida = secoesVisiveis.includes(secaoAtiva) ? secaoAtiva : 'perfil';

  function onSalvarPerfil(valores: PerfilFormValues) {
    setUsuarios((prev) => prev.map((u) => (u.id === usuarioSessao.id ? { ...u, ...valores } : u)));
  }

  function onAlterarPin(_valores: AlterarPinFormValues) {
    // MOCK — troca de PIN não persiste nada localmente (vai pro backend/Bridge real quando
    // existir). O único efeito hoje é o modal fechar (feito pelo chamador), pra não fingir uma
    // escrita que não existe.
  }

  function onSalvarEmpresa(valores: EmpresaFormValues) {
    setEmpresa(valores);
    setEditandoEmpresa(false);
  }

  const usuarioEmEdicao = modalUsuarioForm?.modo === 'editar' ? usuarioById(usuarios, modalUsuarioForm.usuarioId) : undefined;
  const usuarioEmConfirmacao = confirmandoStatusId ? usuarioById(usuarios, confirmandoStatusId) : undefined;

  function onSalvarUsuario(valores: UsuarioFormValues) {
    if (modalUsuarioForm?.modo === 'editar') {
      const usuarioId = modalUsuarioForm.usuarioId;
      const usuarioOriginal = usuarioById(usuarios, usuarioId);
      // Defesa em profundidade — não confia só no `<select>` de Papel travado na UI
      // (`UsuarioFormModal`): founder só troca de mãos entre founders (`papelResolvidoParaSalvar`).
      const papel = papelResolvidoParaSalvar(valores.papel, usuarioOriginal?.papel, sessaoEfetiva.papel);
      setUsuarios((prev) =>
        prev.map((u) => (u.id === usuarioId ? { ...u, ...valores, papel, telefone: valores.telefone || null } : u)),
      );
    } else {
      const papel = papelResolvidoParaSalvar(valores.papel, undefined, sessaoEfetiva.papel);
      const novo: Usuario = {
        id: `u${Date.now()}`,
        nome: valores.nome,
        email: valores.email,
        telefone: valores.telefone || null,
        papel,
        overrides: valores.overrides,
        status: 'ativo',
        criadoEm: HOJE_LABEL,
        ultimoAcessoEm: null,
      };
      setUsuarios((prev) => [novo, ...prev]);
    }
    setModalUsuarioForm(null);
  }

  function onConfirmarAlternarStatus(usuarioId: string) {
    const usuarioAlvo = usuarioById(usuarios, usuarioId);
    // Defesa em profundidade — não confia só no `disabled` do botão Power em `UsuariosTable`:
    // founder nunca é desativado por ninguém, e ninguém desativa a própria conta por esta tela
    // (evitaria um autobloqueio de acesso sem ter outro admin/founder ativo por perto).
    if (!usuarioAlvo || ehIntocavel(usuarioAlvo) || usuarioAlvo.id === usuarioSessao.id) {
      setConfirmandoStatusId(null);
      return;
    }
    setUsuarios((prev) => prev.map((u) => (u.id === usuarioId ? { ...u, status: u.status === 'ativo' ? 'inativo' : 'ativo' } : u)));
    setConfirmandoStatusId(null);
  }

  return {
    sessao: sessaoEfetiva,
    secaoAtiva: secaoResolvida,
    secoesVisiveis,
    onTrocarSecao: setSecaoAtiva,

    usuarioSessao,
    onSalvarPerfil,
    onAlterarPin,

    empresa,
    podeEditarEmpresa: sessaoEfetiva.pode('configuracoes', 'editar'),
    editandoEmpresa,
    onAbrirEditarEmpresa: () => setEditandoEmpresa(true),
    onFecharEditarEmpresa: () => setEditandoEmpresa(false),
    onSalvarEmpresa,

    usuarios,
    podeAdministrarUsuarios: sessaoEfetiva.pode('configuracoes', 'gerenciarUsuarios'),
    modalUsuarioForm,
    usuarioEmEdicao,
    onAbrirCriarUsuario: () => setModalUsuarioForm({ modo: 'criar' }),
    onAbrirEditarUsuario: (usuarioId: string) => setModalUsuarioForm({ modo: 'editar', usuarioId }),
    onFecharModalUsuario: () => setModalUsuarioForm(null),
    onSalvarUsuario,

    usuarioEmConfirmacao,
    onAbrirConfirmarStatus: setConfirmandoStatusId,
    onFecharConfirmarStatus: () => setConfirmandoStatusId(null),
    onConfirmarAlternarStatus,
  };
}

export type ConfiguracoesVm = ReturnType<typeof useConfiguracoes>;
