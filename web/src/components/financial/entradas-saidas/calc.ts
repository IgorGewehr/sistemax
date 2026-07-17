import { todayIso } from '@/lib/date';
import { formatDateShort } from '@/lib/format';
import { reais, type Centavos } from '@/lib/money';

import type {
  Atrasados30DiasResumo,
  CategoriaBarra,
  CategoriaDespesaResumo,
  CategoriaDrillStats,
  CategoriaId,
  CorCategoria,
  FiltroAtivo,
  LancamentoRow,
  LiderAlta,
  SegFiltro,
  TimelineEntry,
} from './types';

/** "Hoje" real (relógio de parede) — a Linha do tempo agora vem do backend real
 * (`GET /financeiro/extrato`), não mais do cenário fixo de exemplo. */
export const TODAY_ISO = todayIso();

export const CATEGORIA_LABEL: Record<CategoriaId, string> = {
  folha: 'Folha',
  fornecedores: 'Fornecedores',
  aluguel: 'Aluguel',
  impostos: 'Impostos',
  software: 'Software',
  marketing: 'Marketing',
  servicos: 'Serviços',
};

/** Mapa categoria (label do form) → `CategoriaId` — mesmo `catMap` do lançamento rápido do mockup. */
export const CATEGORIA_MAP_LANCAMENTO_RAPIDO: Record<string, CategoriaId> = {
  Serviços: 'servicos',
  Produtos: 'servicos',
  'Outra receita': 'servicos',
  Folha: 'folha',
  Fornecedores: 'fornecedores',
  Aluguel: 'aluguel',
  Impostos: 'impostos',
  Software: 'software',
  Marketing: 'marketing',
  'Outra despesa': 'marketing',
};

/** Categoria só "estoura" o padrão a partir de R$ 1.000 no mês — abaixo disso, variação não conta. */
const ANOMALIA_MIN_CENTAVOS: Centavos = reais(1000);
/** Variação mínima vs a média de 5 meses p/ virar badge de alerta (▲ x%) na lista de categorias. */
const ANOMALIA_MIN_VARIACAO_PCT = 15;

const CATEGORIA_COR_CSS: Record<CorCategoria, string> = {
  primary: 'hsl(var(--primary))',
  'fg-62': 'hsl(var(--foreground) / 0.62)',
  'fg-48': 'hsl(var(--foreground) / 0.48)',
  'fg-36': 'hsl(var(--foreground) / 0.36)',
  'fg-26': 'hsl(var(--foreground) / 0.26)',
  'fg-18': 'hsl(var(--foreground) / 0.18)',
};

/** Cor de preenchimento (dot/barra/coluna) da categoria — sempre via CSS var existente, nunca HSL cru. */
export function categoriaCorCss(cor: CorCategoria): string {
  return CATEGORIA_COR_CSS[cor];
}

/** "cmv-fornecedor" → "Cmv Fornecedor" — fallback de rótulo pra `categoriaId` que o domínio real
 * devolve (`GET /financeiro/extrato`) e não está no catálogo ilustrativo de `CATEGORIA_LABEL`
 * (slugs reais como `cmv-fornecedor`/`despesa-com-pessoal` não são 1:1 com folha/aluguel/etc. —
 * ver nota em `types.ts`). Nunca indexa `CATEGORIA_LABEL` à força com um `string` livre. */
export function categoriaLabel(id: string): string {
  const conhecida = CATEGORIA_LABEL[id as CategoriaId];
  if (conhecida) return conhecida;
  return id
    .split(/[-_]/)
    .filter(Boolean)
    .map((parte) => parte.charAt(0).toUpperCase() + parte.slice(1))
    .join(' ');
}

/** Média dos meses anteriores ao corrente (assume que o último item do histórico é o mês corrente). */
export function mediaHistoricoAnterior(historicoCentavos: Centavos[]): Centavos {
  const anteriores = historicoCentavos.slice(0, -1);
  if (anteriores.length === 0) return 0;
  return anteriores.reduce((a, b) => a + b, 0) / anteriores.length;
}

/** Variação do mês corrente vs a média dos meses anteriores, em %. */
export function variacaoPct(categoria: CategoriaDespesaResumo): number {
  const media = mediaHistoricoAnterior(categoria.historicoCentavos);
  if (media === 0) return 0;
  return ((categoria.totalCentavos - media) / media) * 100;
}

export function isAnomalia(categoria: CategoriaDespesaResumo): boolean {
  return categoria.totalCentavos >= ANOMALIA_MIN_CENTAVOS && variacaoPct(categoria) > ANOMALIA_MIN_VARIACAO_PCT;
}

export function totalDespesasCentavos(categorias: CategoriaDespesaResumo[]): Centavos {
  return categorias.reduce((a, c) => a + c.totalCentavos, 0);
}

function maxCategoriaTotalCentavos(categorias: CategoriaDespesaResumo[]): Centavos {
  return Math.max(...categorias.map((c) => c.totalCentavos));
}

/** Barras de "Para onde foi o dinheiro" — largura relativa à maior categoria, % relativo ao total do mês. */
export function buildBarras(categorias: CategoriaDespesaResumo[]): CategoriaBarra[] {
  const total = totalDespesasCentavos(categorias);
  const max = maxCategoriaTotalCentavos(categorias);
  return categorias.map((categoria) => ({
    categoria,
    widthPct: max > 0 ? Math.round((categoria.totalCentavos / max) * 100) : 0,
    pctDoTotal: total > 0 ? Math.round((categoria.totalCentavos / total) * 100) : 0,
    anomalia: isAnomalia(categoria),
    variacaoPct: Math.round(variacaoPct(categoria)),
  }));
}

export function fixoVariavelPct(categorias: CategoriaDespesaResumo[]): { fixoPct: number; varPct: number } {
  const total = totalDespesasCentavos(categorias);
  const fixo = categorias.filter((c) => c.fixo).reduce((a, c) => a + c.totalCentavos, 0);
  const fixoPct = total > 0 ? Math.round((fixo / total) * 100) : 0;
  return { fixoPct, varPct: 100 - fixoPct };
}

/** Categoria com maior alta vs a própria média — só entram categorias com gasto >= R$1.000/mês. */
export function quemMaisSubiu(categorias: CategoriaDespesaResumo[]): LiderAlta | null {
  let lider: LiderAlta | null = null;
  for (const categoria of categorias) {
    if (categoria.totalCentavos < ANOMALIA_MIN_CENTAVOS) continue;
    const delta = variacaoPct(categoria);
    if (!lider || delta > lider.deltaPct) lider = { categoria, deltaPct: Math.round(delta) };
  }
  return lider;
}

/** Estatísticas do drill de categoria (card direito quando uma categoria está selecionada). */
export function categoriaDrillStats(categoria: CategoriaDespesaResumo, totalDespesas: Centavos): CategoriaDrillStats {
  return {
    avg5Centavos: mediaHistoricoAnterior(categoria.historicoCentavos),
    pctDoTotal: totalDespesas > 0 ? Math.round((categoria.totalCentavos / totalDespesas) * 100) : 0,
    variacaoPct: Math.round(variacaoPct(categoria)),
    isAnomalia: isAnomalia(categoria),
  };
}

/** Recebíveis atrasados há mais de 30 dias — mesmo recorte do tile "Raio-X do mês". */
export function atrasados30MaisDias(rows: LancamentoRow[]): Atrasados30DiasResumo {
  const atrasados = rows.filter((r) => r.status === 'atrasado' && (r.diasAtraso ?? 0) > 30);
  return {
    totalCentavos: atrasados.reduce((a, r) => a + r.valorCentavos, 0),
    qtdClientes: atrasados.length,
  };
}

function passaFiltro(row: LancamentoRow, segFiltro: SegFiltro, filtro: FiltroAtivo | null): boolean {
  if (segFiltro === 'receber' && row.tipo !== 'entrada') return false;
  if (segFiltro === 'pagar' && row.tipo !== 'saida') return false;
  if (filtro) {
    if (filtro.type === 'categoria' && row.categoria !== filtro.value) return false;
    if (filtro.type === 'status' && row.status !== 'atrasado') return false;
  }
  return true;
}

/**
 * Monta a Linha do tempo: divisor "Hoje" antes do 1º lançamento já vencido/realizado, e o resumo
 * do PDV logo após o lançamento `r19` — mesma posição fixa do mockup. A âncora é o id (não o
 * índice) porque um novo lançamento pode ser inserido antes dele.
 */
export function buildTimeline(rows: LancamentoRow[], segFiltro: SegFiltro, filtro: FiltroAtivo | null): TimelineEntry[] {
  const entries: TimelineEntry[] = [];
  let dividerInserido = false;
  for (const row of rows) {
    if (!dividerInserido && row.data <= TODAY_ISO) {
      entries.push({ kind: 'divider', label: `Hoje · ${formatDateShort(TODAY_ISO)}` });
      dividerInserido = true;
    }
    if (passaFiltro(row, segFiltro, filtro)) entries.push({ kind: 'row', row });
    if (row.id === 'r19' && segFiltro !== 'pagar' && !filtro) entries.push({ kind: 'summary' });
  }
  return entries;
}

/** Insere mantendo a ordem decrescente por data (futuro/mais recente primeiro) — mesma `insertSorted` do mockup. */
export function insertLancamentoOrdenado(rows: LancamentoRow[], novo: LancamentoRow): LancamentoRow[] {
  const idx = rows.findIndex((r) => r.data < novo.data);
  if (idx === -1) return [...rows, novo];
  return [...rows.slice(0, idx), novo, ...rows.slice(idx)];
}

// ---------- Geometria do gráfico de colunas (drill de categoria) — mesma matemática do `svgColunas` do mockup ----------
export const COLUNAS_BASELINE = 106;
export const COLUNAS_X0 = 20;
export const COLUNAS_X1 = 326;
export const COLUNAS_LABEL_Y = 122;
const COLUNAS_BAR_WIDTH = 26;
const COLUNAS_ALTURA_MAX = 82;

export interface ColunaGeometria {
  x: number;
  y: number;
  width: number;
  height: number;
}

export function buildColunasGeometry(
  historicoCentavos: Centavos[],
  mediaCentavos: Centavos,
): { bars: ColunaGeometria[]; avgY: number } {
  const slot = (COLUNAS_X1 - COLUNAS_X0) / historicoCentavos.length;
  const max = Math.max(...historicoCentavos) * 1.18;
  const f = max > 0 ? COLUNAS_ALTURA_MAX / max : 0;
  const bars = historicoCentavos.map((valor, i) => {
    const height = Math.max(valor * f, 2);
    return {
      x: COLUNAS_X0 + slot * i + (slot - COLUNAS_BAR_WIDTH) / 2,
      y: COLUNAS_BASELINE - height,
      width: COLUNAS_BAR_WIDTH,
      height,
    };
  });
  return { bars, avgY: COLUNAS_BASELINE - mediaCentavos * f };
}
