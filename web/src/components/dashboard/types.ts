/**
 * View-model do Dashboard (SDD — este arquivo é o spec).
 *
 * O Dashboard é a única tela que atravessa TODOS os módulos: puxa o número mais importante de
 * Vendas, Financeiro, Estoque, Compras e OS, e destaca o que precisa de atenção agora. É a
 * primeira tela que o dono abre — por isso é curta (3 blocos) e cada bloco é PERMISSION-AWARE:
 * a seção só aparece com o que a flag correspondente libera (ver `usePermissoesDashboard`), nunca
 * um card vazio/cinza no lugar do que falta permissão.
 *
 * Dinheiro é SEMPRE `Centavos` (inteiro) — nunca float de reais (`lib/money`).
 */
import type { Centavos } from '@/lib/money';

/** Os 5 módulos que o Dashboard resume — mesmo vocabulário/ordem do `Sidebar`. */
export type ModuloDashboard = 'vendas' | 'financeiro' | 'estoque' | 'compras' | 'os';

/** Rotas reais para onde o Dashboard sabe navegar (mesmas do `App.tsx`). */
export type RotaDashboard = '/financeiro' | '/pdv' | '/vendas' | '/estoque' | '/compras' | '/ordens';

/**
 * Alvo de um "drill" (clique que sai do Dashboard e vai pro módulo de origem — mesma Lei 2 do
 * Financeiro: é sempre navegação, nunca ação que a IA executa). `disponivel: false` quando a rota
 * ainda não existe em `App.tsx` (ex.: `/vendas` antes da tela de Vendas ser construída — o
 * Dashboard chega antes dela no roadmap). Nesse caso o clique só mostra o toast, sem navegar — sem
 * isso o usuário cairia no catch-all de `App.tsx` e seria redirecionado pro Financeiro sem aviso,
 * o que pareceria bug, não "em breve" (ver `useDashboardDrill`). Some sozinho, sem editar mais
 * nada, quando a rota for registrada.
 */
export interface DrillTarget {
  rota: RotaDashboard;
  mensagem: string;
  disponivel?: boolean;
}

export type FormatoKpi = 'moeda' | 'contagem';

/** Cor do valor — reservada pra estado (nunca "série" de gráfico): `pos` saudável, `crit` precisa
 * agir, `warn` atenção, `neutro` informativo. */
export type ToneKpi = 'pos' | 'crit' | 'warn' | 'neutro';

/** Um card da fileira de KPIs (bloco ②) — um número-chave por módulo. */
export interface KpiDashboardItem {
  modulo: ModuloDashboard;
  label: string;
  formato: FormatoKpi;
  /** Presente quando `formato === 'moeda'`. */
  valorCentavos?: Centavos;
  /** Presente quando `formato === 'contagem'`. */
  valorContagem?: number;
  tone: ToneKpi;
  /** Variação vs período anterior (ontem p/ Vendas). Omitido quando não fizer sentido comparar. */
  deltaPercentual?: number;
  deltaDirecao?: 'up' | 'down';
  /** Rodapé de contexto — ex.: "38 vendas · ticket médio R$ 126,80". */
  foot: string;
  /** Card em destaque (glow de marca) — só o pulso do dia leva `hero`; no máximo um por tela. */
  hero?: boolean;
  drill: DrillTarget;
}

export type SeveridadeAtencao = 'crit' | 'warn';

/** Um item da lista "Precisa de atenção agora" (bloco ③) — um achado por módulo, mais urgente primeiro. */
export interface ItemAtencao {
  modulo: ModuloDashboard;
  moduloLabel: string;
  severidade: SeveridadeAtencao;
  titulo: string;
  detalhe: string;
  /** Só quando o achado tem um valor monetário associado (ex.: parcela vencendo). */
  valorCentavos?: Centavos;
  drill: DrillTarget;
}

/** Insight único do Super Consultor (bloco ④) — cruza dois módulos numa frase (Lei 2: read-only). */
export interface ConsultorDashboardViewModel {
  itemNome: string;
  quantidadeRestante: number;
  unidade: string;
  mediaVendaLabel: string;
  previsaoLabel: string;
  drill: DrillTarget;
}

export interface DashboardViewModel {
  kpis: KpiDashboardItem[];
  atencao: ItemAtencao[];
  consultor: ConsultorDashboardViewModel;
}
