import {
  PAPEL_PERMISSOES_PADRAO,
  construirPermissao,
  permissoesEfetivas,
  type Acao,
  type Modulo,
  type Papel,
  type PermissaoOverride,
  type Usuario,
} from '@/lib/permissions';

/**
 * Derivações puras de "Configurações" — nada de `useState`/JSX aqui, testável isolado. Espelha
 * `components/clientes/calc.ts`/`components/compras/calc.ts`.
 */

export function usuarioById(usuarios: Usuario[], id: string): Usuario | undefined {
  return usuarios.find((u) => u.id === id);
}

/** Estado atual (papel + overrides em edição) tem a célula módulo×ação ligada? Usado pelo
 *  `PermissoesGrid` pra pintar cada toggle. */
export function celulaLigada(papel: Papel, overrides: PermissaoOverride[], modulo: Modulo, acao: Acao): boolean {
  return permissoesEfetivas({ papel, overrides }).has(construirPermissao(modulo, acao));
}

/**
 * Alterna 1 célula do grid de permissões. Sempre grava o DIFF contra o padrão do papel, nunca o
 * conjunto final: se o novo estado já é o que o papel dá de fábrica, o override é removido (volta
 * a herdar — inclusive ganha automaticamente qualquer permissão nova que o papel receber no
 * futuro); senão grava `conceder`/`revogar` conforme o sentido do clique.
 */
export function alternarCelula(papel: Papel, overrides: PermissaoOverride[], modulo: Modulo, acao: Acao): PermissaoOverride[] {
  const permissao = construirPermissao(modulo, acao);
  const ligadaAtual = celulaLigada(papel, overrides, modulo, acao);
  const semOverrideAntigo = overrides.filter((o) => o.permissao !== permissao);
  const padraoLiga = PAPEL_PERMISSOES_PADRAO[papel].includes(permissao);
  const novoEstado = !ligadaAtual;
  if (novoEstado === padraoLiga) return semOverrideAntigo;
  return [...semOverrideAntigo, { permissao, efeito: novoEstado ? 'conceder' : 'revogar' }];
}

/**
 * Aplica a trava de "founder é intocável" (`ehIntocavel`, `lib/permissions.ts`) a uma troca de
 * papel antes de persistir — chamada por `onSalvarUsuario`, nunca confiando só no que a UI já
 * bloqueou (defesa em profundidade: um `disabled` de `<select>` é só CSS/atributo, não uma garantia).
 * Só outro `founder` pode conceder ou remover o papel `founder` de alguém:
 * - Ator `founder` → sem restrição, `papelDesejado` vale como pedido.
 * - Alvo já é `founder` e ator não é → papel fica travado em `founder` (recusa rebaixar).
 * - Papel pedido é `founder` e ator não é → recusa a promoção; mantém `papelOriginal` (edição) ou
 *   cai pra `admin` (criação de usuário novo, quando não há papel anterior pra restaurar).
 */
export function papelResolvidoParaSalvar(papelDesejado: Papel, papelOriginal: Papel | undefined, atorPapel: Papel): Papel {
  if (atorPapel === 'founder') return papelDesejado;
  if (papelOriginal === 'founder') return papelOriginal;
  if (papelDesejado === 'founder') return papelOriginal ?? 'admin';
  return papelDesejado;
}

/** Iniciais (1 ou 2 letras) a partir de um nome — usado no avatar de Perfil e no fallback de logo
 *  da Empresa quando `logoUrl` é `null`. */
export function iniciais(nome: string): string {
  const partes = nome.trim().split(/\s+/).filter(Boolean);
  if (partes.length === 0) return '';
  const primeira = partes[0]?.[0] ?? '';
  const ultima = partes.length > 1 ? (partes[partes.length - 1]?.[0] ?? '') : '';
  return (primeira + ultima).toUpperCase();
}

/** Máscara de exibição "12.345.678/0001-90" a partir de dígitos crus — idempotente (aceita valor
 *  já mascarado como entrada, útil pra `onChange` incremental de input controlado). */
export function formatCnpj(valor: string): string {
  const digitos = valor.replace(/\D/g, '').slice(0, 14);
  const partes = [digitos.slice(0, 2), digitos.slice(2, 5), digitos.slice(5, 8), digitos.slice(8, 12), digitos.slice(12, 14)];
  let saida = partes[0] ?? '';
  if (partes[1]) saida += `.${partes[1]}`;
  if (partes[2]) saida += `.${partes[2]}`;
  if (partes[3]) saida += `/${partes[3]}`;
  if (partes[4]) saida += `-${partes[4]}`;
  return saida;
}

export function emailValido(email: string): boolean {
  return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email.trim());
}

/** PIN de acesso = 4 a 6 dígitos numéricos (mesma convenção do Bridge local — `lib/auth.tsx`). */
export function pinValido(pin: string): boolean {
  return /^\d{4,6}$/.test(pin);
}
