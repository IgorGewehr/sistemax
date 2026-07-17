import { useMemo } from 'react';

/**
 * RBAC do SistemaX — fonte única de verdade de "quem pode ver/fazer o quê" PRETENDIDA pro app
 * inteiro. HOJE só o módulo Configurações (seção "Usuários & Permissões", via
 * `useSessaoPermissoes`/`usuarioPodeVer`) de fato consome este arquivo. `components/layout/
 * Sidebar.tsx` ainda filtra a nav só pela flag estática `live` (coming-soon), sem RBAC nenhum, e
 * `components/dashboard/usePermissoesDashboard.ts` é um hook paralelo e desconectado (5 flags fixas
 * em `true`) — nenhum dos dois lê `modulosVisiveisDe`/`usuarioPodeVer` daqui ainda. Migrar os dois
 * pra cá é pendência conhecida (não decida visibilidade de menu por comparação de string solta
 * — `usuario.papel === 'admin'` — quando for fazer essa migração; passe pelas funções daqui).
 *
 * MODELO
 * ──────
 * `Papel` tem uma hierarquia numérica (convenção do saas-erp irmão): founder(100) > admin(80) >
 * manager(60) > operator(40) > viewer(20). A hierarquia NÃO decide acesso a módulo — ela só resolve
 * "quem administra quem" (`podeAdministrarUsuarios`, `ehIntocavel`). Acesso a módulo é SEMPRE por
 * permissão granular: um par (`Modulo`, `Acao`), nunca por `ROLE_HIERARCHY[papel] >= X`.
 *
 * A maioria dos módulos só tem as ações `ver`/`editar`. Um módulo ganha uma ação extra só onde há
 * uma operação distinta e sensível o bastante pra merecer o próprio toggle (`pdv.operarCaixa`,
 * `fiscal.emitirFiscal`, `configuracoes.gerenciarUsuarios`) — ver `MODULO_ACOES`. Não inventamos
 * ações "porque um dia pode precisar" (YAGNI): cada ação extra existe porque alguma tela real já
 * checa por ela.
 *
 * Cada `Papel` tem um conjunto PADRÃO de permissões (`PAPEL_PERMISSOES_PADRAO`). Por cima do
 * padrão, um usuário pode ter OVERRIDES individuais: `conceder` (liga algo que o papel não dá) ou
 * `revogar` (desliga algo que o papel daria). Note que o override é sempre um DIFF contra o padrão
 * do papel, nunca o conjunto final persistido — assim um usuário continua herdando automaticamente
 * qualquer permissão nova que o papel ganhar no futuro, sem precisar re-migrar dados existentes.
 * `permissoesEfetivas()` resolve padrão + overrides → o conjunto final; é esse conjunto que
 * qualquer tela consulta (via `usuarioPode`/`usuarioPodeVer`), nunca o padrão do papel isolado.
 *
 * SEAM PRO BACKEND
 * ────────────────
 * `useSessaoPermissoes()` é HOJE um mock local (usuário fixo, sem rede, sem login real). Quando
 * existir sessão de verdade (backend RBAC autenticado), troca-se só o CORPO deste hook — a
 * assinatura de retorno (`usuarioAtual`, `pode`, `podeVer`, `modulosVisiveis`,
 * `podeAdministrarUsuarios`) é o contrato que o módulo Configurações já consome hoje; Sidebar e
 * Dashboard ainda não migraram pra cá (ver nota no topo do arquivo) — quando migrarem, também não
 * precisarão mudar nada além de ler este mesmo hook.
 */

// ───────────────────────── Papéis ─────────────────────────

export type Papel = 'founder' | 'admin' | 'manager' | 'operator' | 'viewer';

/** Hierarquia numérica — maior número = mais permissão. Usada só p/ regras de "quem administra
 *  quem" (nunca p/ decidir acesso a módulo — isso é sempre permissão granular, ver `usuarioPode`). */
export const ROLE_HIERARCHY: Record<Papel, number> = {
  founder: 100,
  admin: 80,
  manager: 60,
  operator: 40,
  viewer: 20,
};

export const PAPEL_LABEL: Record<Papel, string> = {
  founder: 'Fundador',
  admin: 'Administrador',
  manager: 'Gerente',
  operator: 'Operador',
  viewer: 'Visualizador',
};

/** Ordem de exibição (seletor de papel do modal, legendas) — do mais ao menos permissivo. */
export const PAPEIS: Papel[] = ['founder', 'admin', 'manager', 'operator', 'viewer'];

// ───────────────────────── Módulos ─────────────────────────

export type Modulo =
  | 'dashboard'
  | 'vendas'
  | 'pdv'
  | 'financeiro'
  | 'estoque'
  | 'compras'
  | 'ordens'
  | 'clientes'
  | 'agenda'
  | 'fiscal'
  | 'configuracoes';

/** Ordem canônica de exibição — espelha `components/layout/Sidebar.tsx`. */
export const MODULOS: Modulo[] = [
  'dashboard',
  'vendas',
  'pdv',
  'financeiro',
  'estoque',
  'compras',
  'ordens',
  'clientes',
  'agenda',
  'fiscal',
  'configuracoes',
];

export const MODULO_LABEL: Record<Modulo, string> = {
  dashboard: 'Dashboard',
  vendas: 'Vendas',
  pdv: 'PDV',
  financeiro: 'Financeiro',
  estoque: 'Estoque',
  compras: 'Compras',
  ordens: 'Ordens',
  clientes: 'Clientes',
  agenda: 'Agenda',
  fiscal: 'Fiscal',
  configuracoes: 'Configurações',
};

// ───────────────────────── Ações ─────────────────────────

export type Acao = 'ver' | 'editar' | 'operarCaixa' | 'emitirFiscal' | 'gerenciarUsuarios';

export const ACAO_LABEL: Record<Acao, string> = {
  ver: 'Ver',
  editar: 'Editar',
  operarCaixa: 'Abrir/fechar caixa',
  emitirFiscal: 'Emitir documento fiscal',
  gerenciarUsuarios: 'Gerenciar usuários e permissões',
};

/** Quais ações existem em cada módulo — dirige o grid de toggles do modal de usuário
 *  (`PermissoesGrid`). A maioria só tem `ver`/`editar`; ações extras só onde fazem sentido real. */
export const MODULO_ACOES: Record<Modulo, Acao[]> = {
  dashboard: ['ver'],
  vendas: ['ver', 'editar'],
  pdv: ['ver', 'editar', 'operarCaixa'],
  financeiro: ['ver', 'editar'],
  estoque: ['ver', 'editar'],
  compras: ['ver', 'editar'],
  ordens: ['ver', 'editar'],
  clientes: ['ver', 'editar'],
  agenda: ['ver', 'editar'],
  fiscal: ['ver', 'editar', 'emitirFiscal'],
  configuracoes: ['ver', 'editar', 'gerenciarUsuarios'],
};

/** Permissão granular = `"modulo:acao"` (ex.: `"financeiro:editar"`). Só as combinações listadas em
 *  `MODULO_ACOES` fazem sentido de fato — sempre construa com `construirPermissao`, nunca escreva a
 *  string à mão (evita erro de digitação silencioso). */
export type Permissao = `${Modulo}:${Acao}`;

export function construirPermissao(modulo: Modulo, acao: Acao): Permissao {
  return `${modulo}:${acao}`;
}

/** Todas as permissões válidas do sistema — base do papel `founder`/`admin` (acesso total) e do
 *  grid completo de `PermissoesGrid`. */
export function todasPermissoes(): Permissao[] {
  return MODULOS.flatMap((modulo) => MODULO_ACOES[modulo].map((acao) => construirPermissao(modulo, acao)));
}

// ───────────────────────── Padrão por papel ─────────────────────────

function permissoesDe(mapa: Partial<Record<Modulo, Acao[]>>): Permissao[] {
  return Object.entries(mapa).flatMap(([modulo, acoes]) =>
    (acoes ?? []).map((acao) => construirPermissao(modulo as Modulo, acao)),
  );
}

/**
 * Conjunto padrão de permissões por papel. `founder` e `admin` recebem acesso total de propósito —
 * a diferença entre os dois não é "o que cada um pode fazer" (seria complexidade sem valor real),
 * é uma regra de hierarquia à parte: só `founder` é intocável (`ehIntocavel`) — ninguém desativa ou
 * rebaixa o fundador da conta, nem outro admin. Ajuste este mapa quando um papel precisar mudar de
 * capacidade — nunca dê um "jeitinho" com override em massa por fora daqui.
 */
export const PAPEL_PERMISSOES_PADRAO: Record<Papel, Permissao[]> = {
  founder: todasPermissoes(),
  admin: todasPermissoes(),
  manager: permissoesDe({
    dashboard: ['ver'],
    vendas: ['ver', 'editar'],
    pdv: ['ver', 'editar', 'operarCaixa'],
    financeiro: ['ver'],
    estoque: ['ver', 'editar'],
    compras: ['ver', 'editar'],
    ordens: ['ver', 'editar'],
    clientes: ['ver', 'editar'],
    agenda: ['ver', 'editar'],
    fiscal: ['ver'],
    configuracoes: ['ver'],
  }),
  operator: permissoesDe({
    dashboard: ['ver'],
    vendas: ['ver', 'editar'],
    pdv: ['ver', 'editar', 'operarCaixa'],
    estoque: ['ver'],
    ordens: ['ver', 'editar'],
    clientes: ['ver', 'editar'],
    agenda: ['ver', 'editar'],
  }),
  viewer: permissoesDe({
    dashboard: ['ver'],
    vendas: ['ver'],
    estoque: ['ver'],
    ordens: ['ver'],
    clientes: ['ver'],
    agenda: ['ver'],
  }),
};

// ───────────────────────── Usuário + overrides ─────────────────────────

/** Override individual — liga (`conceder`) ou desliga (`revogar`) UMA permissão específica por
 *  cima do padrão do papel do usuário. Sempre um DIFF contra o padrão (nunca o conjunto final). */
export interface PermissaoOverride {
  permissao: Permissao;
  efeito: 'conceder' | 'revogar';
}

export interface Usuario {
  id: string;
  nome: string;
  email: string;
  telefone: string | null;
  papel: Papel;
  status: 'ativo' | 'inativo';
  overrides: PermissaoOverride[];
  /** "14/03/2025" — pré-formatada (mesma convenção do resto do app: nunca ISO cru na UI). */
  criadoEm: string;
  /** `null` = nunca acessou. */
  ultimoAcessoEm: string | null;
}

/** Permissões efetivas de um usuário = padrão do papel + overrides aplicados por cima. */
export function permissoesEfetivas(usuario: Pick<Usuario, 'papel' | 'overrides'>): Set<Permissao> {
  const efetivas = new Set(PAPEL_PERMISSOES_PADRAO[usuario.papel]);
  for (const override of usuario.overrides) {
    if (override.efeito === 'conceder') efetivas.add(override.permissao);
    else efetivas.delete(override.permissao);
  }
  return efetivas;
}

/** `usuario` tem a permissão módulo+ação — considerando papel + overrides. */
export function usuarioPode(usuario: Pick<Usuario, 'papel' | 'overrides'>, modulo: Modulo, acao: Acao): boolean {
  return permissoesEfetivas(usuario).has(construirPermissao(modulo, acao));
}

/** Visibilidade de módulo = ter QUALQUER ação concedida naquele módulo. Quem pode editar/operar
 *  caixa/emitir fiscal necessariamente enxerga o módulo — a UI nunca exige marcar "ver" à parte de
 *  "editar" pra um módulo aparecer. */
export function usuarioPodeVer(usuario: Pick<Usuario, 'papel' | 'overrides'>, modulo: Modulo): boolean {
  const efetivas = permissoesEfetivas(usuario);
  return MODULO_ACOES[modulo].some((acao) => efetivas.has(construirPermissao(modulo, acao)));
}

/** Módulos visíveis, na ordem canônica de `MODULOS` — é isso que o módulo Configurações consulta
 *  pra decidir o que renderizar (Sidebar/Dashboard ainda não migraram pra ler daqui, ver nota no
 *  topo do arquivo; quando migrarem, devem consultar esta função, nunca reimplementar a checagem). */
export function modulosVisiveisDe(usuario: Pick<Usuario, 'papel' | 'overrides'>): Modulo[] {
  return MODULOS.filter((modulo) => usuarioPodeVer(usuario, modulo));
}

/** Só founder/admin administram usuários (papel + permissões de terceiros) — regra de hierarquia,
 *  não de permissão granular: mudar o papel de alguém é sensível demais pra ser um toggle comum. */
export function podeAdministrarUsuarios(papel: Papel): boolean {
  return ROLE_HIERARCHY[papel] >= ROLE_HIERARCHY.admin;
}

/** O fundador nunca pode ser desativado/rebaixado por ninguém — nem por outro admin, nem por outro
 *  founder. Evita o cenário "empresa fica sem nenhum founder ativo". Usado pela tabela de Usuários
 *  pra desabilitar a ação de desativar/trocar papel nesta linha. */
export function ehIntocavel(usuario: Pick<Usuario, 'papel'>): boolean {
  return usuario.papel === 'founder';
}

// ───────────────────────── Seam de sessão (mock) ─────────────────────────

/** Usuário da sessão local — troque por sessão real (login/backend RBAC) quando existir. Papel
 *  `admin` de propósito (não `founder`): exercita o caminho comum de "administra usuários mas não é
 *  intocável", já que só founder tem a trava de `ehIntocavel`. */
const USUARIO_SESSAO_MOCK: Usuario = {
  id: 'u1',
  nome: 'Igor Gewehr',
  email: 'igor@sistemax.app',
  telefone: '(51) 99900-1122',
  papel: 'admin',
  status: 'ativo',
  overrides: [],
  criadoEm: '02/01/2024',
  ultimoAcessoEm: '17/07/2026',
};

export interface SessaoPermissoes {
  usuarioAtual: Usuario;
  papel: Papel;
  /** `pode('financeiro', 'editar')` — true/false pra um par módulo+ação específico. */
  pode: (modulo: Modulo, acao: Acao) => boolean;
  /** `podeVer('fiscal')` — true se QUALQUER ação daquele módulo estiver concedida. */
  podeVer: (modulo: Modulo) => boolean;
  /** Módulos visíveis, ordem canônica — hoje só o módulo Configurações consome isto pra se adaptar
   *  (Sidebar/Dashboard ainda não migraram, ver nota no topo do arquivo). */
  modulosVisiveis: Modulo[];
  podeAdministrarUsuarios: boolean;
}

/**
 * Sessão + RBAC do usuário logado. HOJE devolve um usuário mockado fixo (sem rede, sem login) —
 * este é o SEAM pro backend real: quando existir sessão de verdade, troca-se só o corpo (buscar o
 * usuário autenticado em vez de `USUARIO_SESSAO_MOCK`); `pode`/`podeVer`/`modulosVisiveis` continuam
 * derivados do jeito que já são, puro, a partir do `Usuario` retornado.
 *
 * Qualquer tela que precisa adaptar visibilidade/ações a permissão consome ESTE hook — nunca lê
 * `USUARIO_SESSAO_MOCK` diretamente (ele é um detalhe de implementação, não o contrato).
 */
export function useSessaoPermissoes(): SessaoPermissoes {
  return useMemo<SessaoPermissoes>(() => {
    const usuarioAtual = USUARIO_SESSAO_MOCK;
    return {
      usuarioAtual,
      papel: usuarioAtual.papel,
      pode: (modulo, acao) => usuarioPode(usuarioAtual, modulo, acao),
      podeVer: (modulo) => usuarioPodeVer(usuarioAtual, modulo),
      modulosVisiveis: modulosVisiveisDe(usuarioAtual),
      podeAdministrarUsuarios: podeAdministrarUsuarios(usuarioAtual.papel),
    };
  }, []);
}
