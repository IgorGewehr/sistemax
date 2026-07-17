import type {
  Agendamento,
  AgendamentoStatus,
  ConflitoResultado,
  ConsultorInsightData,
  RecorrenciaFrequencia,
  ViewMode,
} from './types';
import { AGENDAMENTO_TRANSICOES } from './types';

/**
 * Funções puras da Agenda — datas (substituindo `date-fns`, ausente do
 * `package.json` do SistemaX), horário, conflito de agenda, agrupamento,
 * FSM e o insight do Super Consultor. Zero DOM, zero React — testável isolado
 * (mesmo espírito de `components/compras/calc.ts` e `components/os/calc.ts`).
 *
 * `calc.ts` só importa tipos de `types.ts`, nunca o inverso (evita import
 * circular).
 */

// ────────────────────────────────────────────────────────────────────────────
// Datas
// ────────────────────────────────────────────────────────────────────────────

function ymd(d: Date): [number, number, number] {
  return [d.getFullYear(), d.getMonth(), d.getDate()];
}

export function toISODate(d: Date): string {
  const [y, m, day] = ymd(d);
  return `${y}-${String(m + 1).padStart(2, '0')}-${String(day).padStart(2, '0')}`;
}

/**
 * Meia-noite LOCAL. `new Date('yyyy-mm-dd')` parseia como UTC (spec) e
 * desalinha 1 dia em fusos negativos (Brasil/Américas) — mesmo cuidado que
 * `safeDate` documenta em `lib/format.ts`. Forçamos `T00:00:00` sempre.
 */
export function parseISODate(iso: string): Date {
  return new Date(`${iso}T00:00:00`);
}

export function addDias(d: Date, n: number): Date {
  const [y, m, day] = ymd(d);
  return new Date(y, m, day + n);
}

export function addSemanas(d: Date, n: number): Date {
  return addDias(d, n * 7);
}

export function addMeses(d: Date, n: number): Date {
  const [y, m, day] = ymd(d);
  return new Date(y, m + n, day);
}

/** Domingo = 0, igual ao `startOfWeek(d, { weekStartsOn: 0 })` do date-fns que o source usava. */
export function startOfWeek(d: Date): Date {
  const [y, m, day] = ymd(d);
  return new Date(y, m, day - d.getDay());
}

export function endOfWeek(d: Date): Date {
  return addDias(startOfWeek(d), 6);
}

export function startOfMonth(d: Date): Date {
  const [y, m] = ymd(d);
  return new Date(y, m, 1);
}

export function endOfMonth(d: Date): Date {
  const [y, m] = ymd(d);
  return new Date(y, m + 1, 0);
}

export function isSameDiaISO(a: Date, isoB: string): boolean {
  return toISODate(a) === isoB;
}

export function isMesmoMes(a: Date, b: Date): boolean {
  return a.getFullYear() === b.getFullYear() && a.getMonth() === b.getMonth();
}

/**
 * Compara `d` com o relógio REAL do dispositivo (não com `ANCHOR_HOJE` do mock) — mesmo
 * `isToday()` do date-fns que o source usa pra destacar "hoje" na grade e mostrar a linha do
 * "agora". Propositalmente independente da data-âncora da demo: se o relógio real não cair em
 * 2026-07-16, a grade simplesmente não realça nenhum dia (degradação graciosa).
 */
export function isHojeReal(d: Date): boolean {
  const agora = new Date();
  return d.getFullYear() === agora.getFullYear() && d.getMonth() === agora.getMonth() && d.getDate() === agora.getDate();
}

/** Abreviação de 3 letras dos dias da semana (domingo primeiro) — cabeçalhos de Semana/Mês. */
export const WEEKDAY_LABELS_PT: readonly string[] = ['Dom', 'Seg', 'Ter', 'Qua', 'Qui', 'Sex', 'Sáb'];

/** 7 datas da semana (domingo→sábado) que contém `currentDate`. */
export function buildWeekDays(currentDate: Date): Date[] {
  const start = startOfWeek(currentDate);
  return Array.from({ length: 7 }, (_, i) => addDias(start, i));
}

/** 42 células (6 semanas) da grade mensal — sempre começa no domingo da semana do dia 1. */
export function buildMonthGrid(currentDate: Date): Date[] {
  const start = startOfWeek(startOfMonth(currentDate));
  return Array.from({ length: 42 }, (_, i) => addDias(start, i));
}

const MES_FORMATTER = new Intl.DateTimeFormat('pt-BR', { month: 'long' });

/** "16 de julho" (dia) / "13 – 19 de julho 2026" (semana) / "julho de 2026" (mês). */
export function formatPeriodo(viewMode: ViewMode, currentDate: Date): string {
  if (viewMode === 'dia') {
    return `${currentDate.getDate()} de ${MES_FORMATTER.format(currentDate)}`;
  }
  if (viewMode === 'mes') {
    return `${MES_FORMATTER.format(currentDate)} de ${currentDate.getFullYear()}`;
  }
  const start = startOfWeek(currentDate);
  const end = endOfWeek(currentDate);
  if (isMesmoMes(start, end)) {
    return `${start.getDate()} – ${end.getDate()} de ${MES_FORMATTER.format(start)} ${end.getFullYear()}`;
  }
  return `${start.getDate()} de ${MES_FORMATTER.format(start)} – ${end.getDate()} de ${MES_FORMATTER.format(end)} ${end.getFullYear()}`;
}

// ────────────────────────────────────────────────────────────────────────────
// Horário
// ────────────────────────────────────────────────────────────────────────────

export function timeToMinutes(hhmm: string): number {
  const [h, m] = hhmm.split(':').map(Number);
  return h * 60 + m;
}

export function minutesToTime(min: number): string {
  const h = Math.floor(min / 60);
  const m = min % 60;
  return `${String(h).padStart(2, '0')}:${String(m).padStart(2, '0')}`;
}

export function addDuracao(horaInicio: string, duracaoMin: number): string {
  return minutesToTime(timeToMinutes(horaInicio) + duracaoMin);
}

export function getBlockTop(horaInicio: string, startHour: number, hourHeight: number): number {
  const offsetMin = timeToMinutes(horaInicio) - startHour * 60;
  return (offsetMin / 60) * hourHeight;
}

export function getBlockHeight(duracaoMin: number, hourHeight: number): number {
  return Math.max((duracaoMin / 60) * hourHeight, 24);
}

// ────────────────────────────────────────────────────────────────────────────
// Conflito (1 profissional, 1 slot exclusivo — sem noção de turma no v1)
// ────────────────────────────────────────────────────────────────────────────

/**
 * Reimplementa a mesma regra do saas-erp (`lib/services/appointmentConflicts.ts`):
 * overlap de horário do MESMO profissional no MESMO dia, ignorando `excludeId`
 * (o próprio agendamento em edição) e ignorando agendamentos `cancelado`.
 */
export function checkConflito(
  agendamentos: Agendamento[],
  profissionalId: string,
  data: string,
  horaInicio: string,
  horaFim: string,
  excludeId?: string,
): ConflitoResultado {
  if (!profissionalId) return { temConflito: false, mensagem: '' };

  const conflito = agendamentos.find(
    (a) =>
      a.id !== excludeId &&
      a.data === data &&
      a.status !== 'cancelado' &&
      a.profissionalIds.includes(profissionalId) &&
      !(horaFim <= a.horaInicio || horaInicio >= a.horaFim),
  );

  if (!conflito) return { temConflito: false, mensagem: '' };
  return {
    temConflito: true,
    mensagem: `Conflito com ${conflito.clienteNome} (${conflito.horaInicio} – ${conflito.horaFim})`,
  };
}

// ────────────────────────────────────────────────────────────────────────────
// Agrupamento / resumo
// ────────────────────────────────────────────────────────────────────────────

export function groupByData(agendamentos: Agendamento[]): Map<string, Agendamento[]> {
  const map = new Map<string, Agendamento[]>();
  for (const a of agendamentos) {
    const lista = map.get(a.data);
    if (lista) lista.push(a);
    else map.set(a.data, [a]);
  }
  return map;
}

export function filterByProfissional(agendamentos: Agendamento[], profissionalId: string | 'todos'): Agendamento[] {
  if (profissionalId === 'todos') return agendamentos;
  return agendamentos.filter((a) => a.profissionalIds.includes(profissionalId));
}

export function computeStatusSummary(agendamentos: Agendamento[]): Record<AgendamentoStatus, number> {
  const counts: Record<AgendamentoStatus, number> = {
    agendado: 0,
    confirmado: 0,
    em_andamento: 0,
    concluido: 0,
    cancelado: 0,
    nao_compareceu: 0,
  };
  for (const a of agendamentos) counts[a.status]++;
  return counts;
}

// ────────────────────────────────────────────────────────────────────────────
// FSM
// ────────────────────────────────────────────────────────────────────────────

export function podeTransitar(de: AgendamentoStatus, para: AgendamentoStatus): boolean {
  return AGENDAMENTO_TRANSICOES[de].includes(para);
}

// ────────────────────────────────────────────────────────────────────────────
// Recorrência (criação de série)
// ────────────────────────────────────────────────────────────────────────────

export function generateRecorrenciaDatas(
  dataInicioISO: string,
  freq: RecorrenciaFrequencia,
  ocorrencias: number,
): string[] {
  if (freq === 'nenhuma' || ocorrencias <= 1) return [dataInicioISO];
  const start = parseISODate(dataInicioISO);
  const datas: string[] = [];
  for (let i = 0; i < ocorrencias; i++) {
    let d: Date;
    switch (freq) {
      case 'diaria':
        d = addDias(start, i);
        break;
      case 'semanal':
        d = addSemanas(start, i);
        break;
      case 'quinzenal':
        d = addSemanas(start, i * 2);
        break;
      case 'mensal':
        d = addMeses(start, i);
        break;
    }
    datas.push(toISODate(d));
  }
  return datas;
}

// ────────────────────────────────────────────────────────────────────────────
// Super Consultor (IA read-only)
// ────────────────────────────────────────────────────────────────────────────

/** Janela útil do dia pro cálculo de buracos livres — mesmo range da grade visual. */
const START_HOUR_INSIGHT = 6;
const END_HOUR_INSIGHT = 22;

/**
 * Observa a grade de hoje: quantos agendamentos, buracos livres de 45min+
 * (janela 06h–22h) e quantos ainda não confirmaram presença. Read-only —
 * consumido pelo `ConsultorSection` via `ConsultorInsight` (nunca age).
 */
export function buildConsultorInsight(agendamentos: Agendamento[], hojeISO: string): ConsultorInsightData {
  const doDia = agendamentos.filter((a) => a.data === hojeISO && a.status !== 'cancelado');
  const hojeCount = doDia.length;

  const naoConfirmados = doDia
    .filter((a) => a.status === 'agendado')
    .slice()
    .sort((a, b) => timeToMinutes(a.horaInicio) - timeToMinutes(b.horaInicio));

  const proximoNaoConfirmado = naoConfirmados[0]
    ? { clienteNome: naoConfirmados[0].clienteNome, horaInicio: naoConfirmados[0].horaInicio }
    : null;

  const ocupados = doDia.slice().sort((a, b) => timeToMinutes(a.horaInicio) - timeToMinutes(b.horaInicio));
  const gapsHoje: ConsultorInsightData['gapsHoje'] = [];
  let cursor = START_HOUR_INSIGHT * 60;
  const fimJanela = END_HOUR_INSIGHT * 60;

  for (const a of ocupados) {
    const inicioMin = timeToMinutes(a.horaInicio);
    if (inicioMin > cursor) {
      const gapMin = inicioMin - cursor;
      if (gapMin >= 45) gapsHoje.push({ inicio: minutesToTime(cursor), fim: a.horaInicio, minutos: gapMin });
    }
    cursor = Math.max(cursor, timeToMinutes(a.horaFim));
  }
  if (fimJanela > cursor) {
    const gapMin = fimJanela - cursor;
    if (gapMin >= 45) gapsHoje.push({ inicio: minutesToTime(cursor), fim: minutesToTime(fimJanela), minutos: gapMin });
  }

  return { hojeCount, gapsHoje, naoConfirmadosHoje: naoConfirmados.length, proximoNaoConfirmado };
}

// ────────────────────────────────────────────────────────────────────────────
// Vocabulário de status (rótulo + classes de tom — ver README §Vocabulário)
// ────────────────────────────────────────────────────────────────────────────

export const STATUS_LABEL: Record<AgendamentoStatus, string> = {
  agendado: 'Agendado',
  confirmado: 'Confirmado',
  em_andamento: 'Em andamento',
  concluido: 'Concluído',
  cancelado: 'Cancelado',
  nao_compareceu: 'Não compareceu',
};

/**
 * Vocabulário próprio do módulo — os 6 status não cabem no `ChipTone` do
 * Financeiro (`sobra/falta/aberto/bateu/neutro`), então reusam os tokens
 * reservados (pos/warn/crit/primary/faint/surface-2) com variações de tom,
 * mesmo princípio do `OsStatusChip`. `chip` = pílula (100% opacidade);
 * `borda`/`fundo` = borda esquerda + fundo suave do bloco no calendário.
 */
export const STATUS_TONE_CLASSES: Record<AgendamentoStatus, { chip: string; borda: string; fundo: string }> = {
  agendado: { chip: 'text-muted-foreground bg-surface-2', borda: 'border-l-muted-foreground', fundo: 'bg-surface-2' },
  confirmado: { chip: 'text-pos bg-pos-soft', borda: 'border-l-pos', fundo: 'bg-pos-soft/40' },
  em_andamento: { chip: 'text-warn bg-warn-soft', borda: 'border-l-warn', fundo: 'bg-warn-soft/40' },
  concluido: { chip: 'text-primary-600 bg-primary-soft', borda: 'border-l-primary-600', fundo: 'bg-primary-soft/60' },
  cancelado: { chip: 'text-crit bg-crit-soft', borda: 'border-l-crit', fundo: 'bg-crit-soft/40' },
  nao_compareceu: { chip: 'text-faint bg-surface-2', borda: 'border-l-faint', fundo: 'bg-surface-2' },
};

// ────────────────────────────────────────────────────────────────────────────
// Constantes de grade (mirror das do AgendaModule original)
// ────────────────────────────────────────────────────────────────────────────

export const HOUR_HEIGHT = 64;
export const START_HOUR = 6;
export const END_HOUR = 22;
export const DURATION_OPTIONS = [15, 30, 45, 60, 90, 120] as const;

function generateTimeOptions(): string[] {
  const opts: string[] = [];
  for (let h = START_HOUR; h <= END_HOUR - 1; h++) {
    opts.push(`${String(h).padStart(2, '0')}:00`);
    opts.push(`${String(h).padStart(2, '0')}:30`);
  }
  return opts;
}

/** 06:00…21:30 de 30 em 30 — mesmo range gerado do `TIME_OPTIONS` do source. */
export const TIME_OPTIONS: string[] = generateTimeOptions();

// ────────────────────────────────────────────────────────────────────────────
// Dinheiro (form)
// ────────────────────────────────────────────────────────────────────────────

/** Digitação livre → centavos, sem depender de libs de máscara externas: "12345" digitado → 12345 centavos = R$123,45. */
export function parseCentavosDigitados(raw: string): number {
  const digits = raw.replace(/\D/g, '');
  if (!digits) return 0;
  return parseInt(digits, 10);
}

/** Id curto pra novos agendamentos/séries — mesmo formato do `uid()` de `components/os/calc.ts`. */
export function uid(): string {
  return 'a' + Math.random().toString(36).slice(2, 9);
}
