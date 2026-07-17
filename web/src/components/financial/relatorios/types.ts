import type { Centavos } from '@/lib/money';

/**
 * View-model da tela Relatórios (`docs/ui/mockups/relatorios.html` — fonte da verdade).
 * Hoje implementado por `mocks/financeiro/relatorios.ts`; amanhã por uma resposta de API com
 * exatamente este shape (troca de origem, não de tela — ver `docs/ui/financeiro-ui.md` §4).
 */

/** Único toggle explícito e global de regime nesta tela (as demais telas não expõem isso). */
export type Regime = 'competencia' | 'caixa';

/** Canal de envio pro contador — usado tanto no menu "Enviar" quanto no histórico. */
export type EnvioChannel = 'email' | 'whatsapp' | null;

/** Formato gerável por qualquer um dos 4 cards de documento "simples" (Pacote tem fluxo próprio). */
export type DocFormat = 'PDF' | 'Excel';

/** Estado do botão de geração de documento (`generateDoc` do mockup): idle → generating → done → idle. */
export type DocGenState = 'idle' | 'generating' | 'done';

export interface PeriodOption {
  id: string;
  label: string;
}

/**
 * Trecho de texto com negrito opcional — usado nas notas compostas (bridge note do DRE, linha de
 * resultado do pacote) pra preservar o `<b>` do mockup sem colocar JSX dentro do mock (`.ts` puro).
 */
export interface RichTextPart {
  text: string;
  bold?: boolean;
}

export interface DreLine {
  label: string;
  valueCentavos: Centavos;
}

export interface DreRegimeBlock {
  /** Rótulo curto exibido no `doc-sub` do card ("competência" / "caixa"). */
  regimeLabel: string;
  topLine: DreLine;
  /** Linhas de dedução, na ordem exibida (estilo `.v.neg` — mais claras, nunca vermelhas). */
  deductionLines: DreLine[];
  totalLine: DreLine;
  delta: {
    direction: 'up' | 'down';
    /** Copy already composta ("▲ 12% vs Junho (R$ 3.780)") — é exemplo/prosa, não dado calculado. */
    label: string;
  };
  bridgeNote: RichTextPart[];
}

export interface DreViewModel {
  docLabel: string;
  periodLabel: string;
  byRegime: Record<Regime, DreRegimeBlock>;
}

export interface ChecklistItem {
  label: string;
  /** Contagem entre parênteses, quando houver ("(3 contas)"). */
  count?: string;
}

export interface PacoteViewModel {
  docLabel: string;
  zipFileName: string;
  checklist: ChecklistItem[];
  resultLineByRegime: Record<Regime, RichTextPart[]>;
}

export interface AccountOption {
  id: string;
  label: string;
}

export interface ExtratoViewModel {
  docLabel: string;
  /** Primeira opção é sempre a exclusiva "Todas" — ver `toggleAccountSelection` em `helpers.ts`. */
  accounts: AccountOption[];
  defaultFrom: string;
  defaultTo: string;
}

export interface AgingBucket {
  id: string;
  label: string;
  amountCentavos: Centavos;
  /**
   * Valor de cor via `var(--token)` (não classe Tailwind) — a faixa de 0–15d usa opacidade parcial
   * do `warn`, e o modificador `bg-warn/NN` do Tailwind não é garantido pra um token sem
   * `<alpha-value>` no config; referenciar a CSS var diretamente reproduz o mockup com segurança.
   */
  colorVar: string;
}

export interface AbertoViewModel {
  docLabel: string;
  receberEmAberto: Centavos;
  receberAtrasado: Centavos;
  pagarEmAberto: Centavos;
  agingBuckets: AgingBucket[];
}

export interface MrrViewModel {
  docLabel: string;
  condicaoLabel: string;
  mrr: Centavos;
  churnMes: Centavos;
  arrEstimado: Centavos;
}

export interface AccountantContact {
  emailLabel: string;
  email: string;
  whatsappLabel: string;
  whatsapp: string;
}

export interface HistoryRow {
  id: string;
  date: string;
  document: string;
  format: string;
  generatedBy: string;
  channel: EnvioChannel;
  /** Só true nas linhas inseridas em runtime — dispara o flash de entrada (`.row-new` do mockup). */
  isNew?: boolean;
}

export interface ReportsViewModel {
  periods: PeriodOption[];
  defaultPeriodId: string;
  defaultRegime: Regime;
  contact: AccountantContact;
  dre: DreViewModel;
  pacote: PacoteViewModel;
  extrato: ExtratoViewModel;
  aberto: AbertoViewModel;
  mrr: MrrViewModel;
  initialHistory: HistoryRow[];
}
