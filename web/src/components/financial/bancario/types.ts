import type { Centavos } from '@/lib/money';

/**
 * View-model da tela Bancário (SDD) — espelha 1:1 `docs/ui/mockups/bancario.html`.
 * Alimentado por dado REAL via `useBancario` (docs/wiring/financeiro-telas-restantes.md §3) —
 * adapter em `lib/api/adapters/financeiro/bancario.ts` converte os DTOs do backend pra este
 * mesmo tipo.
 */

export type ContaId = string;

/** Conta bancária conectada — fatia do "Saldo em bancos" e filtro do extrato. */
export interface ContaBancaria {
  id: ContaId;
  /** Rótulo curto usado no extrato/KPI (ex.: "Itaú") — o chip de filtro adiciona "PJ" à parte. */
  label: string;
  saldoCentavos: Centavos;
  /** Classe Tailwind do "pontinho" na tabela do extrato (replica a escala fg/0.6, /0.4, /0.25 do mockup). */
  dotClassName: string;
}

/** Semana do mês corrente — cada dia soma pra bater com os KPIs do topo (fonte única, como no mockup). */
export interface SemanaMovimento {
  id: number;
  /** Rótulo da semana — a 3ª vem com "*" (semana em andamento); removido ao entrar no drill de dias. */
  label: string;
  /** Semana em andamento — barras esmaecidas no gráfico da visão geral. */
  parcial: boolean;
  diasLabel: string[];
  entrouPorDiaCentavos: Centavos[];
  saiuPorDiaCentavos: Centavos[];
}

export type StatusExtrato = 'conciliado' | 'pendente';

/** Uma linha do extrato — também alimenta a lista de "movimentos" no drill de dias da semana. */
export interface MovimentoExtrato {
  id: string;
  /** Formato "DD/MM" — o mesmo texto exibido, nunca parseado como Date. */
  data: string;
  descricao: string;
  forma: string;
  contaId: ContaId;
  /** Sinal indica entrada (+) ou saída (−). */
  valorCentavos: Centavos;
  status: StatusExtrato;
  semanaId: number;
}

/** Item pendente num dos dois "baldes" de sobra (banco ou sistema). */
export interface ItemConciliacaoPendente {
  id: string;
  data: string;
  descricao: string;
  valorCentavos: Centavos;
  /** Texto de correspondência sugerida por heurística de conciliação (não é o Super Consultor). */
  sugestao: string;
  /** Botão primário — ao clicar, o item é resolvido E contabilizado em "Bateu certinho". */
  rotuloAcaoPrimaria: string;
  /** Botão secundário — ao clicar, o item só é descartado da lista (não conta como batido). */
  rotuloAcaoSecundaria: string;
  /** Id do melhor candidato do lado oposto (extrato↔movimento), pronto pro par de
   * `POST /financeiro/conciliacao` — só existe quando o dado vem da API real (ver
   * `lib/api/adapters/financeiro/bancario.ts`); `undefined` no mock. `null` quando a heurística do
   * backend não achou nenhum candidato plausível (o botão primário fica desabilitado nesse caso). */
  idSugerido?: string | null;
}

export interface ItemBatidoAmostra {
  data: string;
  descricao: string;
}

/** Estado dos 3 "baldes" de conciliação — a peça central da tela. */
export interface ConciliacaoBancaria {
  bateuCertinhoTotal: number;
  /** Amostra fixa (ilustrativa) mostrada no drill de "Bateu certinho" — não cresce com o total. */
  bateuCertinhoAmostra: ItemBatidoAmostra[];
  sobrouNoBanco: ItemConciliacaoPendente[];
  sobrouNoSistema: ItemConciliacaoPendente[];
}

/** Uma linha do painel "Ver por forma" do Super Consultor. */
export interface TaxaPorForma {
  forma: string;
  valorCentavos: Centavos;
  /** Rótulo da taxa tal como o mockup exibe — mistura "%" e "fixo", mantido como copy literal. */
  taxaLabel: string;
  /** Taxa efetiva mais alta do grupo (crédito parcelado) — destacada em vermelho. */
  destaque?: boolean;
}

/** Dado que alimenta o card do Super Consultor (read-only — Lei 2 do contrato). */
export interface ConsultorBancarioInsight {
  taxaTotalCentavos: Centavos;
  percentualVolume: number;
  taxaCreditoParceladoPct: number;
  porForma: TaxaPorForma[];
}

/** Copy de exemplo do delta de um KPI — não é recalculado a partir da amostra de movimentos. */
export interface KpiDeltaExemplo {
  label: string;
  direcao: 'up' | 'down';
}

export interface BancarioViewModel {
  periodoLabel: string;
  contas: ContaBancaria[];
  semanas: SemanaMovimento[];
  movimentos: MovimentoExtrato[];
  conciliacaoInicial: ConciliacaoBancaria;
  consultor: ConsultorBancarioInsight;
  kpiSaldoDelta: KpiDeltaExemplo;
  kpiEntrouDelta: KpiDeltaExemplo;
  kpiEntrouFoot: string;
  kpiSaiuDelta: KpiDeltaExemplo;
  kpiSaiuFoot: string;
  extratoHint: string;
}
