import { useSessaoPermissoes } from '@/lib/permissions';

/**
 * Flags de visibilidade do Dashboard, uma por módulo — PERMISSION-AWARE de propósito: cada seção
 * (KPI, item de "precisa de atenção", o Consultor) condiciona sua própria renderização à flag
 * correspondente. É a MESMA tela pra todos; esconde o que o usuário não pode ver em vez de mostrar
 * card vazio.
 *
 * DERIVA do RBAC real (`lib/permissions.ts`) — fonte única de verdade de permissão. Não duplica
 * modelo: mapeia os 5 recortes do Dashboard para os `Modulo`s equivalentes via `sessao.podeVer`.
 * Quando o backend de sessão existir, só o corpo de `useSessaoPermissoes` muda; este hook não.
 */
export interface PermissoesDashboard {
  podeVerFinanceiro: boolean;
  podeVerVendas: boolean;
  podeVerEstoque: boolean;
  podeVerCompras: boolean;
  podeVerOs: boolean;
}

export function usePermissoesDashboard(): PermissoesDashboard {
  const sessao = useSessaoPermissoes();
  return {
    podeVerFinanceiro: sessao.podeVer('financeiro'),
    podeVerVendas: sessao.podeVer('vendas'),
    podeVerEstoque: sessao.podeVer('estoque'),
    podeVerCompras: sessao.podeVer('compras'),
    podeVerOs: sessao.podeVer('ordens'),
  };
}
