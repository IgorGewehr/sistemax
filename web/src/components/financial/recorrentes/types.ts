/**
 * View-model da tela Financeiro › Recorrentes (SDD — este arquivo é o spec).
 * Espelha 1:1 os dados de `docs/ui/mockups/recorrentes.html` (painel Contas fixas +
 * resumo do painel Assinaturas) e `docs/ui/mockups/financeiro-assinaturas.html`
 * (números da carteira reaproveitados no resumo). Ver `docs/ui/financeiro-ui.md`.
 *
 * Dinheiro é SEMPRE `Centavos` (inteiro) — nunca float de reais (`lib/money`).
 */
import type { Centavos } from '@/lib/money';

export type LenteRecorrentes = 'fixas' | 'assinaturas';

// ───────────────────────── Contas fixas ─────────────────────────

/**
 * Uma conta fixa (recorrência) com 12 meses de histórico, na ordem Ago→Jul
 * (mês mais recente por último) — mesma janela do mockup.
 */
export interface ContaFixa {
  id: string;
  nome: string;
  categoria: string;
  /** Dia do vencimento (1–31). */
  diaVencimento: number;
  /** Rótulo da próxima cobrança, formato "dd/mm" (copy do mockup, ex. "18/07"). */
  proximaLabel: string;
  /** Rótulo de "ativa desde", formato "mês/ano" (ex. "jan/2021"). */
  ativaDesde: string;
  /** 12 meses de histórico em centavos: [Ago, Set, Out, Nov, Dez, Jan, Fev, Mar, Abr, Mai, Jun, Jul]. */
  historico12m: Centavos[];
}

/** `ContaFixa` + métricas derivadas (calculadas em `calc.ts`, nunca hardcoded). */
export interface ContaFixaDerivada extends ContaFixa {
  /** Valor do mês corrente (Jul) — último item do histórico. */
  atual: Centavos;
  /** Valor do mês anterior (Jun). */
  mesPassado: Centavos;
  /** Média dos 6 meses anteriores ao atual (Jan→Jun) — base de comparação. */
  media6m: Centavos;
  /** Variação % do atual vs a média de 6 meses. */
  variacaoPct: number;
  /** Sinalizado quando a variação é >= 15% (degrau fora do padrão). */
  emAlerta: boolean;
  /** Total pago no ano corrente até o mês atual (Jan→Jul, 7 meses). */
  totalAnoCorrente: Centavos;
}

export interface RetratoFixo {
  projecaoAnual: Centavos;
  variacaoSeisMesesPct: number;
  totalHaSeisMeses: Centavos;
  totalAtual: Centavos;
  compromissosAtivos: number;
}

export interface ContasFixasViewModel {
  itens: ContaFixa[];
  /** Receita média dos últimos 3 meses — referência do "peso na receita". */
  receitaMediaReferencia: Centavos;
  /** Dias úteis do mês — referência do "custo por dia útil". */
  diasUteisMes: number;
  /** Nota editorial fixa do KPI "Vs. mês passado" (copy do mockup, não é derivada). */
  notaVariacaoMensal: string;
  /** Nota editorial fixa do card do Super Consultor ("daqui a 2 dias"). */
  notaPrazoConsultor: string;
}

// ───────────────────────── Assinaturas (resumo) ─────────────────────────

export interface AssinaturaServico {
  id: string;
  nome: string;
  /** MRR atual do serviço. */
  mrr: Centavos;
  /** Classe Tailwind do dot/barra (ex. "bg-primary-600", "bg-foreground/55"). */
  corClasse: string;
  clientes: number;
  /** Nº de clientes que deram churn neste serviço no mês corrente. */
  churnClientesMes: number;
  tempoMedioMeses: string;
  ltv: Centavos;
  retencaoPct: string;
  /** Novos/expansão por mês, 6 meses (Fev→Jul), em centavos. */
  novos6m: Centavos[];
  /** Churn por mês, 6 meses (Fev→Jul), em centavos. */
  churn6m: Centavos[];
}

export interface CarteiraAssinaturas {
  tempoMedioMeses: string;
  ltv: Centavos;
  retencaoPct: string;
}

export interface AssinaturasResumoViewModel {
  servicos: AssinaturaServico[];
  carteira: CarteiraAssinaturas;
  mrrMesAnterior: Centavos;
  /** 6 pontos ilustrativos do MRR (Fev→Jul) usados no sparkline do KPI hero. */
  sparklineMrr6m: Centavos[];
  /** Nº total de assinaturas ativas (contratos, não serviços) — copy do mockup. */
  assinaturasAtivasCount: number;
  /** Cliente(s) que deram churn no mês — copy fixa (não modelada por cliente aqui). */
  churnClienteNomes: string;
  /** Cliente novo do mês — copy fixa. */
  novoClienteNomes: string;
  /** Serviço com maior concentração de MRR — referência do card do Consultor. */
  concentracaoServicoId: string;
  /** Cliente por trás desse serviço concentrado — copy fixa. */
  concentracaoClienteNome: string;
}

export interface RecorrentesViewModel {
  /** Rótulo do período no header (ex. "Julho 2026") — mesmo período nas duas lentes. */
  periodoLabel: string;
  fixas: ContasFixasViewModel;
  assinaturas: AssinaturasResumoViewModel;
}
