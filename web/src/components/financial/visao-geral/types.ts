/**
 * View-model da tela Financeiro › Visão Geral v3 (SDD — este arquivo é o spec).
 * Espelha 1:1 os blocos de `docs/ui/mockups/visao-geral-v3.html`: medidor de fôlego + projeção de
 * caixa, 4 tiles (a receber/a pagar/resultado/assinaturas), mix das 3 correntes, ROI (opt-in) e
 * Radar do Simples. SEM Super Consultor (a v3 não tem IA nesta tela) e SEM "Próximos vencimentos"
 * (a v2 tinha; virou o sub-rótulo "atrasado"/"maior" das tiles ① e ②).
 *
 * Dinheiro é SEMPRE `Centavos` (inteiro) — nunca float de reais (`lib/money`).
 */
import type { Centavos } from '@/lib/money';

/** Rotas reais do módulo Financeiro — destino dos drills desta tela (Lei 2: só navegação, a Visão
 * Geral nunca age). */
export type FinanceiroRoute =
  | '/financeiro/entradas-saidas'
  | '/financeiro/recorrentes'
  | '/financeiro/bancario'
  | '/financeiro/fluxo-de-caixa'
  | '/financeiro/relatorios'
  | '/financeiro/roi-negocio';

export interface DrillTarget {
  rota: FinanceiroRoute;
  mensagem: string;
}

/** Zona de saúde do fôlego de caixa — mesmas faixas do mockup: verde ≥30 dias, âmbar 15–30, vermelho <15. */
export type ZonaFolego = 'pos' | 'warn' | 'crit';

/** Bloco ① (dominante) — medidor de fôlego de caixa (`GET /financeiro/previsao-caixa`) + os 2
 * chips "Em caixa"/"Pode tirar" (`GET /financeiro/disponivel-retirada`). Os dois endpoints viram
 * UM card só no mockup — por isso um view-model único (ver `useVisaoGeral`, `Promise.all`). */
export interface GaugeViewModel {
  diasFolego: number | null;
  zona: ZonaFolego;
  verdictoLabel: string;
  probabilidadeFaltarPercent: number;
  /** Texto do ⓘ — já com os números reais embutidos. */
  tooltip: string;
  emCaixaCentavos: Centavos;
  podeTirarCentavos: Centavos;
  drillDial: DrillTarget;
  drillEmCaixa: DrillTarget;
  drillPodeTirar: DrillTarget;
}

export interface TimelinePonto {
  index: number;
  dataIso: string;
  saldoCentavos: Centavos;
  projetado: boolean;
}

/** Bloco ①b — projeção do caixa (`GET /financeiro/fluxo`, 14 dias realizados + 30 previstos). */
export interface TimelineViewModel {
  pontos: TimelinePonto[];
  hojeIndex: number;
  /** Índice do ponto de saldo mais baixo de toda a série — o "dia mais apertado" do mockup. */
  menorIndex: number;
}

/** Tile ① "A receber". */
export interface TileAReceberViewModel {
  totalCentavos: Centavos;
  atrasadoCentavos: Centavos;
  pctEmDia: number;
  pctAtrasado: number;
  drill: DrillTarget;
}

export interface SemanaPagar {
  /** "18–24/07". */
  label: string;
  valorCentavos: Centavos;
  /** 0–100, relativo à semana mais pesada da janela. */
  alturaPct: number;
  /** A semana mais pesada — barra em destaque, como no mockup ("folha"). */
  destaque: boolean;
}

/** Tile ② "A pagar" — 4 semanas (28 dias) a partir de hoje. */
export interface TileAPagarViewModel {
  totalCentavos: Centavos;
  semanas: SemanaPagar[];
  maiorLabel: string;
  maiorDataLabel: string;
  drill: DrillTarget;
}

/** Tile ③ "Resultado" — mesmo `DreGerencialService` do mix (ver `DreResumoViewModel`). */
export interface TileResultadoViewModel {
  resultadoCentavos: Centavos;
  deltaPercentual: number;
  deltaDirecao: 'up' | 'down';
  margemPercent: number;
  drill: DrillTarget;
}

/** Tile ④ "Assinaturas/MRR". */
export interface TileAssinaturasViewModel {
  mrrCentavos: Centavos;
  assinaturasAtivas: number;
  drill: DrillTarget;
}

/** Junta o extrato de horizonte largo (`GET /financeiro/extrato`) — UMA chamada, back de dois
 * tiles (mesmo dataset: contas a receber/pagar em aberto). */
export interface AbertoResumoViewModel {
  receber: TileAReceberViewModel;
  pagar: TileAPagarViewModel;
}

export type CorrenteChave = 'serv' | 'rec' | 'com';

export interface SegmentoMix {
  label: string;
  percent: number;
  chave: CorrenteChave;
}

/** Bloco ③a "De onde vem" — rosca das 3 correntes (`DreDto.porCorrente`). `null` quando o período
 * não tem nenhuma receita reconhecida por corrente (nada a desenhar — vira estado vazio). */
export interface MixViewModel {
  totalCentavos: Centavos;
  segmentos: SegmentoMix[];
  drill: DrillTarget;
}

/** Junta o resultado do mês (tile ③) e o mix (bloco ③a) — mesma chamada `relatorios/dre`. */
export interface DreResumoViewModel {
  resultado: TileResultadoViewModel;
  mix: MixViewModel | null;
}

/** Bloco ③b "Investimento" — opt-in (`imobilizadoRoiAtivo`), `GET /financeiro/roi-negocio`. */
export interface InvestimentoViewModel {
  percentRecuperado: number;
  recuperadoCentavos: Centavos;
  totalCentavos: Centavos;
  drill: DrillTarget;
}

/** Bloco ③c "Simples Nacional" — sempre visível (não é opt-in), `GET /financeiro/radar-simples`. */
export interface SimplesViewModel {
  aliquotaPercent: number;
  faixaAtual: number;
  /** 0–100 — posição de RBT12 no caminho até o próximo degrau (RBT12 ÷ (RBT12 + distância)). */
  fillPercent: number;
  distanciaLabel: string;
  drill: DrillTarget;
}
