import type { MetodoPagamento, StatusVenda } from '@/lib/api/vendas';
import type { Centavos } from '@/lib/money';

import type { FiltrosVendas, VendaRow } from './types';

/**
 * Derivações puras de "Vendas" — mesmo espírito de `components/compras/calc.ts`: nada de
 * `useState`/JSX aqui, só matemática/filtro testável isoladamente da árvore de componentes.
 */

// ───────────────────────── Tons semânticos (status → cor reservada) ─────────────────────────

/** Vendas só precisa de 3 estados de cor — sem o `info`/`faint` que Compras usa (não há
 * "aguardando ação" aqui: uma venda está Em aberto, Concluída ou Estornada). */
export type Tone = 'pos' | 'warn' | 'crit';

export const VENDA_STATUS_LABEL: Record<StatusVenda, string> = {
  Aberta: 'Em aberto',
  Concluida: 'Concluída',
  Estornada: 'Estornada',
};

export const VENDA_STATUS_TONE: Record<StatusVenda, Tone> = {
  Aberta: 'warn',
  Concluida: 'pos',
  Estornada: 'crit',
};

/** % com 1 casa e vírgula pt-BR — mesmo `formatPct1` de `components/compras/calc.ts`. */
export function formatPct1(value: number): string {
  return value.toLocaleString('pt-BR', { minimumFractionDigits: 1, maximumFractionDigits: 1 });
}

/** Nesta tela todo delta é "quanto mais vendeu, melhor": cresceu (>=0) é bom (pos), caiu é ruim (crit). */
export function deltaTone(pct: number): Tone {
  return pct >= 0 ? 'pos' : 'crit';
}

// ───────────────────────── Forma de pagamento (join de exibição) ─────────────────────────

/** "Pix" ou "Pix + Dinheiro" quando dividido — mesma junção usada na tabela e no modal de detalhe. */
export function formatFormasPagamento(formas: MetodoPagamento[]): string {
  return formas.join(' + ');
}

export function isPagamentoDividido(formas: MetodoPagamento[]): boolean {
  return formas.length > 1;
}

// ───────────────────────── Busca (texto normalizado, sem acento) ─────────────────────────

/** Remove acentuação + normaliza caixa — permite buscar "joao" e achar "João". Mesmo espírito do
 * `buscaNormalizada` de Compras, com o passo extra de acento que o brief desta tela pede. */
export function normalizarBusca(texto: string): string {
  return texto
    .trim()
    .toLowerCase()
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '');
}

// ───────────────────────── Filtro da tabela (canal · operador · forma de pagamento · busca) ─────────────────────────

/** Filtra `vendas` client-side pelos critérios combináveis — sem chamada de rede por filtro. */
export function filtrarVendasTabela(vendas: VendaRow[], filtros: FiltrosVendas): VendaRow[] {
  const busca = normalizarBusca(filtros.busca);
  return vendas.filter((v) => {
    if (filtros.apenasEstornadas && v.status !== 'Estornada') return false;
    if (filtros.canal !== 'todos' && v.canal !== filtros.canal) return false;
    if (filtros.operador !== 'todos' && v.operador !== filtros.operador) return false;
    if (filtros.formaPagamento !== 'todas' && !v.formasPagamento.includes(filtros.formaPagamento)) return false;
    if (!busca) return true;
    const alvo = normalizarBusca(`${v.numero} ${v.clienteNome ?? ''} ${v.operador}`);
    return alvo.includes(busca);
  });
}

// ───────────────────────── Sparkline (KPI hero "Vendido no mês") ─────────────────────────

export interface SparklineGeometria {
  viewW: number;
  viewH: number;
  path: string;
  area: string;
  lastPoint: [number, number];
}

/** Mesma matemática do `buildSparkline` de `components/compras/calc.ts` — área + linha com ponto final. */
export function buildSparkline(valores: Centavos[]): SparklineGeometria {
  const viewW = 240;
  const viewH = 34;
  const n = valores.length;
  const max = Math.max(...valores);
  const min = Math.min(...valores);
  const span = Math.max(1, max - min);
  const pontos = valores.map((v, i): [number, number] => [
    n > 1 ? i * (viewW / (n - 1)) : 0,
    viewH - 4 - ((v - min) / span) * (viewH - 10),
  ]);
  const path = 'M' + pontos.map(([x, y]) => `${x.toFixed(1)},${y.toFixed(1)}`).join(' L');
  const area = `${path} L${viewW},${viewH} L0,${viewH} Z`;
  return { viewW, viewH, path, area, lastPoint: pontos[pontos.length - 1] };
}
