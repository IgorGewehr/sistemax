/**
 * View-model do bloco "Sobrevivência" (novo — não existe mockup próprio ainda). Composto pelas 4
 * leituras do motor quant da F1 (docs/financeiro/inteligencia-arquitetura.md/ADR-0005) que hoje
 * têm endpoint real e zero tela ligada: runway/bandas de caixa, ponto de equilíbrio, inadimplência
 * e Radar do Simples Nacional. Todo valor monetário é `Centavos` — nunca float de reais.
 */
import type { Centavos } from '@/lib/money';

export interface RunwayCardData {
  diasRunwayRealista: number | null;
  diasRunwayBruto: number | null;
  /** 0–1 (fração), não percentual — a UI multiplica por 100 na exibição. */
  probabilidadeSaldoNegativoEm30Dias: number;
  primeiroDiaP50NegativoLabel: string | null;
}

export interface BreakevenCardData {
  receitaNecessariaMensalCentavos: Centavos;
  receitaNecessariaDiariaCentavos: Centavos;
  receitaAcumuladaNoMesCentavos: Centavos;
  /** 0–1 (fração). */
  margemContribuicaoPercentual: number;
  diaDoEquilibrio: number | null;
  jaAtingiuNoMes: boolean;
  /** 0–100, já limitado (clamp) — para a barra de progresso. */
  progressoPercentual: number;
}

export interface FaixaInadimplenciaResumo {
  label: string;
  valorCentavos: Centavos;
  quantidade: number;
}

export interface InadimplenciaCardData {
  valorTotalEmAbertoCentavos: Centavos;
  provisaoEsperadaCentavos: Centavos;
  valorLiquidoEsperadoCentavos: Centavos;
  porFaixa: FaixaInadimplenciaResumo[];
}

export interface RadarSimplesCardData {
  rbt12Centavos: Centavos;
  faixaAtual: number;
  /** 0–1 (fração). */
  aliquotaEfetiva: number;
  distanciaAoProximoDegrauCentavos: Centavos;
  mesesProjetadosAteOProximoDegrau: number | null;
}
