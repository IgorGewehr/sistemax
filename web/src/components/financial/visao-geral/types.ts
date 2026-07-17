/**
 * View-model da tela Financeiro › Visão Geral (SDD — este arquivo é o spec).
 * Espelha 1:1 os 5 blocos de `docs/ui/mockups/visao-geral.html`. Ver `docs/ui/financeiro-ui.md`.
 *
 * Dinheiro é SEMPRE `Centavos` (inteiro) — nunca float de reais (`lib/money`).
 */
import type { Centavos } from '@/lib/money';

/** Rotas reais do módulo Financeiro (mesmas do `FinanceiroLayout`) — destino dos drills desta tela. */
export type FinanceiroRoute =
  | '/financeiro/entradas-saidas'
  | '/financeiro/recorrentes'
  | '/financeiro/bancario'
  | '/financeiro/fluxo-de-caixa'
  | '/financeiro/relatorios';

/**
 * Alvo de um "drill" (clique que leva a outra aba do Financeiro — Lei 2 permite link de
 * navegação). `mensagem` é a cópia literal do `data-msg` do mockup: some das abas de destino
 * ainda são stubs sem o filtro aplicado (outras fatias do workflow), então o toast comunica o
 * que o usuário veria lá, além da navegação de verdade acontecer.
 */
export interface DrillTarget {
  rota: FinanceiroRoute;
  mensagem: string;
}

/** Linha de decomposição do hero "Você pode tirar até" (bloco ①). */
export interface LinhaDisponibilidade {
  label: string;
  /** Texto secundário entre parênteses — ex.: "(15 dias + imposto)". */
  sublabel?: string;
  valorCentavos: Centavos;
  tone: 'pos' | 'crit';
  /** Texto que aparece no hover, à direita — ex.: "Bancário →". */
  arrowLabel: string;
  drill: DrillTarget;
}

export interface DisponivelViewModel {
  /** "Você pode tirar até" / linha total "Livre de verdade" — os dois mostram o mesmo valor. */
  livreDeVerdadeCentavos: Centavos;
  noBancoEGaveta: LinhaDisponibilidade;
  jaTemDono: LinhaDisponibilidade;
}

export interface LucroDoMesViewModel {
  lucroCentavos: Centavos;
  deltaPercentual: number;
  deltaDirecao: 'up' | 'down';
  /** "De cada R$ 1 vendido, sobram R$ X" — centavos por 1 real vendido (0–100). */
  margemPorRealCentavos: Centavos;
  /** Ponte lucro → disponível: quanto ainda está por receber (explica por que lucro > livre). */
  aReceberCentavos: Centavos;
  verDeOndeVeio: DrillTarget;
}

export interface EventoTimeline {
  descricao: string;
  deltaCentavos: Centavos;
  tone: 'pos' | 'crit';
}

export interface TimelineViewModel {
  /** Saldo projetado por dia (centavos) — um valor por dia corrido do período de 30 dias. */
  valoresDiarios: Centavos[];
  /** Índice (0-based) do dia de hoje dentro de `valoresDiarios`. */
  hojeIndex: number;
  /** Eventos grandes por dia-do-mês (chave = dia 1–31), usados no tooltip ao clicar num ponto. */
  eventosPorDia: Record<number, EventoTimeline>;
  /** Mês usado para compor "DD/MM" de cada ponto — ex.: "07" (julho). Só usado quando `datasISO`
   * não vem preenchido (o mock assume índice+1 = dia do mês, ver `dayLabel` do componente). */
  mesLabel: string;
  /** Data real (ISO `yyyy-MM-dd`) de cada ponto, mesma ordem de `valoresDiarios` — presente quando
   * o dado vem da API (`GET /financeiro/fluxo`), ausente no mock. Com dado real o período pode
   * atravessar mais de um mês/ano, então "índice+1 = dia do mês" deixa de valer; o componente usa
   * esta série pra rotular cada ponto pela data verdadeira em vez de inferir. */
  datasISO?: string[];
}

export interface ProximoVencimento {
  /** "sáb 18/07" */
  dataLabel: string;
  /** Sinal já embutido no centavo (positivo = entrada, negativo = saída). */
  valorCentavos: Centavos;
  tone: 'pos' | 'crit';
  descricao: string;
  drill: DrillTarget;
}

/**
 * Uma linha do painel "Ver como calculamos" — vem de `ConsultorInsightNarrado.facts` (.NET):
 * `label` é a chave humanizada, `valor` já chega formatado do servidor (ex.: "R$ 2.100", "12", "8%").
 */
export interface FatoConsultor {
  label: string;
  valor: string;
}

/**
 * Um insight narrado do Super Consultor (bloco ⑤), já rankeado pelo backend. `frase` é a análise
 * pronta (determinística — `NarradorTemplate`); `fatos` alimentam o "Ver como calculamos"; `drill`
 * é navegação READ-ONLY (Lei 2 — a IA aponta pra tela, nunca age), `null` quando o destino é a
 * própria Visão Geral ou uma tela sem rota mapeada.
 */
export interface InsightConsultor {
  /** `modulo:ruleId` — chave estável de render/ranking. */
  id: string;
  frase: string;
  fatos: FatoConsultor[];
  drill: DrillTarget | null;
}

/** View-model do bloco ⑤ — a lista de insights reais de `GET /financeiro/consultor`. */
export interface ConsultorViewModel {
  insights: InsightConsultor[];
}

export interface VisaoGeralViewModel {
  /** Rótulo do período no header — ex.: "Julho 2026". */
  periodoLabel: string;
  disponivel: DisponivelViewModel;
  lucroDoMes: LucroDoMesViewModel;
  timeline: TimelineViewModel;
  proximosVencimentos: ProximoVencimento[];
}
