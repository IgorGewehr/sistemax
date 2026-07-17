import type { Centavos } from '@/lib/money';

import type {
  CategoriaCusto,
  Fornecedor,
  FornecedorStatus,
  HistoricoCustoSerie,
  ItemNota,
  ItemNotaPadrao,
  ItemNotaPedido,
  MatchKind,
  NotaEntrada,
  NotaStatus,
  Pedido,
  PedidoStatus,
} from './types';

/**
 * Derivações puras de "Compras" — mesma matemática de `docs/ui/mockups/compras.html`, só que
 * tipada e testável isoladamente da árvore de componentes (nada de `useState`/JSX aqui).
 */

export function fornById(fornecedores: Fornecedor[], id: string): Fornecedor | undefined {
  return fornecedores.find((f) => f.id === id);
}

export function notaById(notas: NotaEntrada[], id: string): NotaEntrada | undefined {
  return notas.find((n) => n.id === id);
}

/** % com 1 casa e vírgula pt-BR — mesmo `pct1()` do mockup (`toLocaleString` com vírgula, não ponto). */
export function formatPct1(value: number): string {
  return value.toLocaleString('pt-BR', { minimumFractionDigits: 1, maximumFractionDigits: 1 });
}

// ───────────────────────── Tons semânticos (rótulo/estado → cor reservada) ─────────────────────────

/** Tom de estado — `info` cobre "aguardando ação" (o mockup usa `--info`, um azul neutro sem token global equivalente ainda). */
export type Tone = 'info' | 'warn' | 'pos' | 'crit' | 'faint';

export const NOTA_STATUS_LABEL: Record<NotaStatus, string> = {
  conferir: 'a conferir',
  divergente: 'divergente',
  recebida: 'recebida',
  estornada: 'estornada',
};
export const NOTA_STATUS_TONE: Record<NotaStatus, Tone> = {
  conferir: 'info',
  divergente: 'warn',
  recebida: 'pos',
  estornada: 'faint',
};

export const PEDIDO_STATUS_LABEL: Record<PedidoStatus, string> = {
  rascunho: 'Rascunho',
  enviado: 'Enviado',
  parcial: 'Parcialmente recebido',
  recebido: 'Recebido',
  cancelado: 'Cancelado',
};
export const PEDIDO_STATUS_TONE: Record<PedidoStatus, Tone> = {
  rascunho: 'faint',
  enviado: 'info',
  parcial: 'warn',
  recebido: 'pos',
  cancelado: 'faint',
};

export const FORNECEDOR_STATUS_LABEL: Record<FornecedorStatus, string> = {
  ativo: 'Ativo',
  bloqueado: 'Bloqueado',
  inativo: 'Inativo',
};
export const FORNECEDOR_STATUS_TONE: Record<FornecedorStatus, Tone> = {
  ativo: 'pos',
  bloqueado: 'crit',
  inativo: 'faint',
};

export const MATCH_LABEL: Record<MatchKind, string> = {
  auto: 'auto',
  sugerido: 'sugerido',
  semmatch: 'sem match',
  ignorado: 'ignorado',
};
export const MATCH_TONE: Record<MatchKind, Tone> = {
  auto: 'pos',
  sugerido: 'warn',
  semmatch: 'crit',
  ignorado: 'faint',
};

// ───────────────────────── Delta de custo (badge "▲ +14,0% vs últ.") ─────────────────────────

export type DeltaBadgeKind = 'novo' | 'flat' | 'up-bad' | 'up-crit' | 'down-good';

export interface DeltaBadge {
  kind: DeltaBadgeKind;
  label: string;
}

/**
 * Classifica a variação de custo vs a compra anterior do mesmo produto×fornecedor.
 * `>= 12%` de alta vira "crítica" (cor mais forte) — mesmo corte do mockup.
 */
export function deltaBadge(pct: number | null | undefined): DeltaBadge {
  if (pct === null || pct === undefined) return { kind: 'novo', label: '1ª compra' };
  if (pct === 0) return { kind: 'flat', label: '▬ estável' };
  if (pct > 0) {
    return { kind: pct >= 12 ? 'up-crit' : 'up-bad', label: `▲ +${formatPct1(pct)}% vs últ.` };
  }
  return { kind: 'down-good', label: `▼ ${formatPct1(pct)}% vs últ.` };
}

// ───────────────────────── Itens de nota: padrão × pedido ─────────────────────────

/** Discrimina o item de three-way match (Pedido × Nota × Físico) do item de conferência padrão. */
export function isItemPedido(item: ItemNota): item is ItemNotaPedido {
  return 'pedidoQtd' in item;
}

/**
 * Custo unitário de exibição do item. Itens conferidos contra pedido não carregam um custo de
 * entrada calculado nesta tela (mostram Pedido/Nota/Físico em vez disso) — cai pra 0, mesmo
 * fallback `it.custoUnit || 0` do mockup na lista "Itens com variação de custo".
 */
export function custoUnitCentavosOf(item: ItemNota): Centavos {
  if (isItemPedido(item)) return 0;
  return item.custoUnitCentavos ?? 0;
}

export function matchCounts(itens: ItemNotaPadrao[]): { auto: number; sugerido: number; semmatch: number } {
  const counts = { auto: 0, sugerido: 0, semmatch: 0 };
  for (const it of itens) {
    if (it.match === 'auto') counts.auto++;
    else if (it.match === 'sugerido') counts.sugerido++;
    else if (it.match === 'semmatch') counts.semmatch++;
  }
  return counts;
}

export function pendentesPadrao(itens: ItemNotaPadrao[]): ItemNotaPadrao[] {
  return itens.filter((it) => it.match === 'sugerido' || it.match === 'semmatch');
}

export function resolvidosOuIgnorados(itens: ItemNotaPadrao[]): ItemNotaPadrao[] {
  return itens.filter((it) => it.match === 'auto' || it.match === 'ignorado');
}

export interface ItemComVariacao {
  nota: NotaEntrada;
  item: ItemNota;
  fornecedorNome: string;
}

/** Todos os itens (de todas as notas) com Δ de custo ≠ 0/null, ordenado por |Δ| desc — alimenta o painel "Variação de custo". */
export function itensComVariacao(notas: NotaEntrada[], fornecedores: Fornecedor[]): ItemComVariacao[] {
  const flat: ItemComVariacao[] = [];
  for (const nota of notas) {
    const fornecedor = fornById(fornecedores, nota.fornecedorId);
    if (!fornecedor) continue;
    for (const item of nota.itens) {
      if (item.deltaPct !== null && item.deltaPct !== undefined && item.deltaPct !== 0) {
        flat.push({ nota, item, fornecedorNome: fornecedor.nome });
      }
    }
  }
  return [...flat].sort((a, b) => Math.abs(b.item.deltaPct ?? 0) - Math.abs(a.item.deltaPct ?? 0));
}

// ───────────────────────── KPIs da Home ─────────────────────────

export interface HomeKpis {
  compradoMesCentavos: Centavos;
  pedidosAbertos: Pedido[];
  pedidosAbertoTotalCentavos: Centavos;
  notasConferir: NotaEntrada[];
  notasComDivergencia: number;
  subiram: number;
  cairam: number;
}

/** Agrega os 4 KPIs do topo da Home. `compradoMes` soma `comprado90d` dos fornecedores — mesma conta do mockup. */
export function buildHomeKpis(notas: NotaEntrada[], pedidos: Pedido[], fornecedores: Fornecedor[]): HomeKpis {
  const compradoMesCentavos = fornecedores.reduce((acc, f) => acc + f.comprado90dCentavos, 0);
  const pedidosAbertos = pedidos.filter((p) => p.status === 'enviado' || p.status === 'parcial');
  const pedidosAbertoTotalCentavos = pedidosAbertos.reduce((acc, p) => acc + p.totalCentavos, 0);
  const notasConferir = notas.filter((n) => n.status === 'conferir' || n.status === 'divergente');
  const notasComDivergencia = notas.filter((n) => n.status === 'divergente').length;
  const variacao = itensComVariacao(notas, fornecedores);
  const subiram = variacao.filter((v) => (v.item.deltaPct ?? 0) > 0).length;
  const cairam = variacao.filter((v) => (v.item.deltaPct ?? 0) < 0).length;
  return { compradoMesCentavos, pedidosAbertos, pedidosAbertoTotalCentavos, notasConferir, notasComDivergencia, subiram, cairam };
}

// ───────────────────────── Ranking de fornecedores (drill "Compras por fornecedor") ─────────────────────────

export interface FornecedorBarra {
  fornecedor: Fornecedor;
  pct: number;
}

export interface FornecedorRanking {
  totalCentavos: Centavos;
  top3: FornecedorBarra[];
  restoCount: number;
  restoValorCentavos: Centavos;
  restoPct: number;
}

/** Top 3 fornecedores por comprado (90d) + "resto" agregado — mesmo recorte do card "Compras por fornecedor". */
export function buildFornecedorRanking(fornecedores: Fornecedor[]): FornecedorRanking {
  const totalCentavos = fornecedores.reduce((acc, f) => acc + f.comprado90dCentavos, 0);
  const ordenados = [...fornecedores].sort((a, b) => b.comprado90dCentavos - a.comprado90dCentavos);
  const top3 = ordenados.slice(0, 3).map((fornecedor) => ({
    fornecedor,
    pct: totalCentavos > 0 ? (fornecedor.comprado90dCentavos / totalCentavos) * 100 : 0,
  }));
  const restantes = ordenados.slice(3);
  const restoValorCentavos = restantes.reduce((acc, f) => acc + f.comprado90dCentavos, 0);
  return {
    totalCentavos,
    top3,
    restoCount: restantes.length,
    restoValorCentavos,
    restoPct: totalCentavos > 0 ? (restoValorCentavos / totalCentavos) * 100 : 0,
  };
}

/** Dias de atraso médio (lead time real − prometido). Positivo = atrasado; ≤0 = dentro do combinado. */
export function atrasoDias(fornecedor: Fornecedor): number {
  return fornecedor.leadTimeRealDias - fornecedor.leadTimePrometidoDias;
}

// ───────────────────────── Footer da conferência ─────────────────────────

export function parcelasResumoTxt(parcelas: { valorCentavos: Centavos }[], formatCentavos: (c: Centavos) => string): string {
  if (!parcelas.length) return 'à vista';
  return `${parcelas.length}× ${formatCentavos(parcelas[0].valorCentavos)}`;
}

/** Quantos itens realmente entram no estoque ao confirmar (tudo que não foi marcado como "ignorado"). */
export function itensQueEntram(itens: ItemNota[]): number {
  return itens.filter((it) => !('match' in it) || it.match !== 'ignorado').length;
}

// ───────────────────────── Geometria SVG: barras agrupadas (Custo por categoria) ─────────────────────────

const CATEGORIA_COR_CSS: Record<CategoriaCusto['cor'], string> = {
  primary: 'hsl(var(--primary))',
  'fg-50': 'hsl(var(--foreground) / 0.5)',
  'fg-30': 'hsl(var(--foreground) / 0.3)',
};

export function categoriaCorCss(cor: CategoriaCusto['cor']): string {
  return CATEGORIA_COR_CSS[cor];
}

export interface BarraGeometria {
  x: number;
  y: number;
  width: number;
  height: number;
  fill: string;
  titulo: string;
}

export interface GroupedBarsGeometry {
  viewW: number;
  viewH: number;
  zeroY: number;
  x0: number;
  x1: number;
  bars: BarraGeometria[];
  monthLabels: { x: number; label: string }[];
}

/** Mesma matemática do `groupedBarsSvg()` do mockup — grupos de barras por mês, uma barra por categoria. */
export function buildGroupedBars(meses: string[], categorias: CategoriaCusto[], formatCentavos: (c: Centavos) => string): GroupedBarsGeometry {
  const viewW = 340;
  const viewH = 148;
  const x0 = 16;
  const x1 = viewW - 10;
  const zeroY = viewH - 22;
  const top = 14;
  const maxV = Math.max(...categorias.flatMap((c) => c.valoresCentavos), 1);
  const slot = (x1 - x0) / meses.length;
  const bw = Math.min(16, slot / (categorias.length + 1.4));

  const bars: BarraGeometria[] = [];
  const monthLabels: { x: number; label: string }[] = [];
  meses.forEach((mes, mi) => {
    const groupX = x0 + slot * mi + slot / 2;
    categorias.forEach((cat, ci) => {
      const v = cat.valoresCentavos[mi] ?? 0;
      const hgt = (v / maxV) * (zeroY - top);
      const cx = groupX + (ci - (categorias.length - 1) / 2) * (bw + 3);
      bars.push({ x: cx - bw / 2, y: zeroY - hgt, width: bw, height: hgt, fill: categoriaCorCss(cat.cor), titulo: `${cat.nome} · ${mes} · ${formatCentavos(v)}` });
    });
    monthLabels.push({ x: groupX, label: mes });
  });

  return { viewW, viewH, zeroY, x0, x1, bars, monthLabels };
}

// ───────────────────────── Geometria SVG: linha (Histórico de custo do fornecedor) ─────────────────────────

const HISTORICO_COR_CSS: Record<HistoricoCustoSerie['cor'], string> = {
  primary: 'hsl(var(--primary))',
  'fg-50': 'hsl(var(--foreground) / 0.5)',
};

export interface LinePonto {
  x: number;
  y: number;
  label: string;
  valorCentavos: Centavos;
}

export interface LineSerieGeometria {
  nome: string;
  cor: string;
  path: string;
  pontos: LinePonto[];
}

export interface LineChartGeometry {
  viewW: number;
  viewH: number;
  series: LineSerieGeometria[];
  monthLabels: { x: number; label: string }[];
}

/** Mesma matemática do `lineChartSvg()` do mockup — 1 linha por série, eixo Y auto-escalado (±12%/10%). */
export function buildLineChart(series: HistoricoCustoSerie[]): LineChartGeometry {
  const viewW = 340;
  const viewH = 150;
  const x0 = 30;
  const x1 = viewW - 12;
  const top = 14;
  const bottom = viewH - 24;
  const allY = series.flatMap((s) => s.pontos.map((p) => p.valorCentavos));
  const maxY = Math.max(...allY) * 1.12;
  const minY = Math.min(...allY) * 0.9;
  const n = series[0]?.pontos.length ?? 0;
  const slot = n > 1 ? (x1 - x0) / (n - 1) : 0;
  const yFor = (v: number) => bottom - ((v - minY) / (maxY - minY || 1)) * (bottom - top);

  const builtSeries: LineSerieGeometria[] = series.map((s) => {
    const pontos: LinePonto[] = s.pontos.map((p, i) => ({ x: x0 + slot * i, y: yFor(p.valorCentavos), label: p.label, valorCentavos: p.valorCentavos }));
    const path = pontos.map((p, i) => `${i === 0 ? 'M' : 'L'}${p.x.toFixed(1)},${p.y.toFixed(1)}`).join(' ');
    return { nome: s.nome, cor: HISTORICO_COR_CSS[s.cor], path, pontos };
  });

  const monthLabels = (series[0]?.pontos ?? []).map((p, i) => ({ x: x0 + slot * i, label: p.label }));

  return { viewW, viewH, series: builtSeries, monthLabels };
}

// ───────────────────────── Filtros da tabela segmentada (Notas · Pedidos · Fornecedores) ─────────────────────────

export type FiltroStatusNota = 'todas' | 'conferir' | 'divergente' | 'recebida' | 'estornada' | 'conferir_kpi';

/** Filtro de texto + status da tabela "Notas de entrada" — `busca` já normalizada (trim + lowercase). */
export function filtrarNotasTabela(notas: NotaEntrada[], fornecedores: Fornecedor[], busca: string, filtro: FiltroStatusNota): NotaEntrada[] {
  return notas.filter((n) => {
    const fornecedor = fornById(fornecedores, n.fornecedorId);
    const matchBusca = !busca || n.numero.toLowerCase().includes(busca) || (fornecedor?.nome.toLowerCase().includes(busca) ?? false);
    if (!matchBusca) return false;
    if (filtro === 'conferir_kpi') return n.status === 'conferir' || n.status === 'divergente';
    if (filtro === 'todas') return true;
    return n.status === filtro;
  });
}

export function filtrarPedidosTabela(pedidos: Pedido[], fornecedores: Fornecedor[], busca: string): Pedido[] {
  return pedidos.filter((p) => {
    const fornecedor = fornById(fornecedores, p.fornecedorId);
    return !busca || p.numero.toLowerCase().includes(busca) || (fornecedor?.nome.toLowerCase().includes(busca) ?? false);
  });
}

export function filtrarFornecedoresTabela(fornecedores: Fornecedor[], busca: string): Fornecedor[] {
  return fornecedores.filter((f) => !busca || f.nome.toLowerCase().includes(busca));
}

/**
 * Extrai o número do fator sugerido ("1 CX = 10,000 kg" → "10,000") pra pré-preencher o input
 * editável de conversão. Não usa regex ingênua de "só dígitos/vírgula" (isso concatenaria o "1"
 * de "1 CX" com "10,000", virando "110,000") — casa especificamente o que vem depois do "=".
 */
export function fatorSugeridoNumero(fatorSugerido: string | undefined): string {
  if (!fatorSugerido) return '10,000';
  const match = /=\s*([\d.,]+)/.exec(fatorSugerido);
  return match ? match[1] : '10,000';
}

// ───────────────────────── Sparkline (KPI hero "Comprado no mês") ─────────────────────────

export interface SparklineGeometria {
  viewW: number;
  viewH: number;
  path: string;
  area: string;
  lastPoint: [number, number];
}

/** Mesma matemática do `sparkline()` do mockup — área + linha com ponto final. */
export function buildSparkline(valores: Centavos[]): SparklineGeometria {
  const viewW = 240;
  const viewH = 34;
  const n = valores.length;
  const max = Math.max(...valores);
  const min = Math.min(...valores);
  const span = Math.max(1, max - min);
  const pontos = valores.map((v, i): [number, number] => [n > 1 ? i * (viewW / (n - 1)) : 0, viewH - 4 - ((v - min) / span) * (viewH - 10)]);
  const path = 'M' + pontos.map(([x, y]) => `${x.toFixed(1)},${y.toFixed(1)}`).join(' L');
  const area = `${path} L${viewW},${viewH} L0,${viewH} Z`;
  return { viewW, viewH, path, area, lastPoint: pontos[pontos.length - 1] };
}
