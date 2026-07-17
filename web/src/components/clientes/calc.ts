import type { Centavos } from '@/lib/money';

import type { Cliente, FiltroClientes } from './types';

/**
 * Derivações puras de "Clientes" — nenhuma depende de `useState`/JSX, testável isoladamente.
 * Mesma disciplina de `components/compras/calc.ts`: toda matemática de segmento/filtro mora aqui,
 * nunca inline em componente.
 */

/** Dias corridos entre duas datas "DD/MM/AAAA" — `Date.UTC` com números validados do split,
 *  nunca `new Date(string)` (evita `RangeError` e ambiguidade de fuso). */
export function diasEntre(dataDDMMAAAA: string, hojeDDMMAAAA: string): number {
  const [d1, m1, y1] = dataDDMMAAAA.split('/').map(Number);
  const [d2, m2, y2] = hojeDDMMAAAA.split('/').map(Number);
  const ms = Date.UTC(y2, m2 - 1, d2) - Date.UTC(y1, m1 - 1, d1);
  return Math.round(ms / 86_400_000);
}

/** "É aniversário dentro do mês corrente?" — só compara o campo "MM" de `aniversario` (DD/MM)
 *  contra o mês de `hojeDDMMAAAA`. Ignora ano de propósito (não guardamos idade). */
export function ehAniversarianteNoMes(aniversario: string | null, hojeDDMMAAAA: string): boolean {
  if (!aniversario) return false;
  const [, mAni] = aniversario.split('/');
  const [, mHoje] = hojeDDMMAAAA.split('/');
  return mAni === mHoje;
}

/** "Aniversário nos próximos 7 dias corridos (incluindo hoje)?" — usado no rodapé do KPI. */
export function ehAniversarianteNaSemana(aniversario: string | null, hojeDDMMAAAA: string): boolean {
  if (!aniversario) return false;
  const [dAni, mAni] = aniversario.split('/').map(Number);
  const [dHoje, mHoje, yHoje] = hojeDDMMAAAA.split('/').map(Number);
  const hoje = Date.UTC(yHoje, mHoje - 1, dHoje);
  // Testa o aniversário no ano corrente E no seguinte (cobre virada dez→jan).
  for (const ano of [yHoje, yHoje + 1]) {
    const diff = Math.round((Date.UTC(ano, mAni - 1, dAni) - hoje) / 86_400_000);
    if (diff >= 0 && diff <= 6) return true;
  }
  return false;
}

/** Cliente cadastrado no mês/ano corrente de `hojeDDMMAAAA` (via `criadoEm`). */
export function ehNovoNoMes(criadoEm: string, hojeDDMMAAAA: string): boolean {
  const [, mCriado, yCriado] = criadoEm.split('/');
  const [, mHoje, yHoje] = hojeDDMMAAAA.split('/');
  return mCriado === mHoje && yCriado === yHoje;
}

/** Segmento "sem comprar há 90+ dias" — SÓ entre clientes com `status === 'ativo'`
 *  (quem já foi desativado sai do funil de reengajamento) e que já compraram alguma vez. */
export function estaSemComprar90d(cliente: Cliente, hojeDDMMAAAA: string): boolean {
  if (cliente.status !== 'ativo' || !cliente.ultimaVisita) return false;
  return diasEntre(cliente.ultimaVisita, hojeDDMMAAAA) >= 90;
}

export interface ClientesKpis {
  clientesAtivos: number;
  novosNoMes: number;
  novosNaSemana: number;
  aniversariantesNoMes: Cliente[];
  aniversariantesNaSemana: number;
  semComprar90d: Cliente[];
}

export function buildKpis(clientes: Cliente[], hojeDDMMAAAA: string): ClientesKpis {
  const ativos = clientes.filter((c) => c.status === 'ativo');
  return {
    clientesAtivos: ativos.length,
    novosNoMes: ativos.filter((c) => ehNovoNoMes(c.criadoEm, hojeDDMMAAAA)).length,
    novosNaSemana: ativos.filter((c) => ehNovoNoMes(c.criadoEm, hojeDDMMAAAA) && diasEntre(c.criadoEm, hojeDDMMAAAA) <= 7).length,
    aniversariantesNoMes: ativos.filter((c) => ehAniversarianteNoMes(c.aniversario, hojeDDMMAAAA)),
    aniversariantesNaSemana: ativos.filter((c) => ehAniversarianteNaSemana(c.aniversario, hojeDDMMAAAA)).length,
    semComprar90d: ativos.filter((c) => estaSemComprar90d(c, hojeDDMMAAAA)),
  };
}

/** Soma `totalGastoVidaCentavos` de um segmento de clientes (ex.: `kpis.semComprar90d`) — usada pelo
 *  card do Super Consultor pra citar uma cifra sempre reproduzível a partir do array real, nunca
 *  hardcoded ao lado da copy (bug já visto aqui: o texto fixo chegou a afirmar quase o dobro do que
 *  o segmento realmente somava). */
export function somaGastoVidaCentavos(clientes: Cliente[]): Centavos {
  return clientes.reduce((soma, c) => soma + c.totalGastoVidaCentavos, 0);
}

/** Filtro exclusivo da tabela (chips + KPIs clicáveis escrevem no mesmo estado). */
export function filtrarClientes(clientes: Cliente[], filtro: FiltroClientes, buscaNormalizada: string, hojeDDMMAAAA: string): Cliente[] {
  let base = clientes;
  if (filtro === 'aniversariantes') base = base.filter((c) => ehAniversarianteNoMes(c.aniversario, hojeDDMMAAAA));
  if (filtro === 'semComprar90d') base = base.filter((c) => estaSemComprar90d(c, hojeDDMMAAAA));
  if (!buscaNormalizada) return base;
  return base.filter((c) =>
    [c.nome, c.telefone ?? '', c.email ?? ''].some((campo) => campo.toLowerCase().includes(buscaNormalizada)),
  );
}

export function clienteById(clientes: Cliente[], id: string): Cliente | undefined {
  return clientes.find((c) => c.id === id);
}

/** Tom de estado. Sem `'info'` — diferente de `components/compras/chips.tsx` (onde `info` decora um
 *  status fixo, "a conferir"), aqui todo tom de linha do histórico é `pos/warn/faint` dinâmico via
 *  `statusHistoricoTone`; incluir `info` seria vocabulário nunca exercitado (YAGNI). */
export type Tone = 'warn' | 'pos' | 'crit' | 'faint';

export const STATUS_LABEL: Record<Cliente['status'], string> = { ativo: 'Ativo', inativo: 'Inativo' };
export const STATUS_TONE: Record<Cliente['status'], Tone> = { ativo: 'pos', inativo: 'faint' };

/** Só `venda` — tom de linha `os` nunca é fixo, sempre vem de `statusHistoricoTone` (depende do
 *  status da OS), então não faz parte deste mapa. */
export const HISTORICO_TONE: Record<'venda', Tone> = { venda: 'faint' };

/** Tom do status de OS no histórico ("Concluída"/"Em andamento"/"Orçamento") — string livre porque
 *  vem de outro módulo (Ordem de Serviço); aqui só decoramos, nunca validamos FSM alheia. */
export function statusHistoricoTone(statusLabel: string | undefined): Tone {
  if (!statusLabel) return 'faint';
  const s = statusLabel.toLowerCase();
  if (s.includes('conclu')) return 'pos';
  if (s.includes('andamento')) return 'warn';
  return 'faint';
}

/** Valida/normaliza a máscara livre "DD/MM" do input do formulário. `''` é válido (campo opcional,
 *  vira `null`). Retorna `undefined` quando a string não é uma data DD/MM plausível. */
export function parseAniversario(valor: string): string | null | undefined {
  const trimmed = valor.trim();
  if (!trimmed) return null;
  const m = /^(\d{1,2})\/(\d{1,2})$/.exec(trimmed);
  if (!m) return undefined;
  const dia = Number(m[1]);
  const mes = Number(m[2]);
  if (mes < 1 || mes > 12) return undefined;
  const diasNoMes = new Date(Date.UTC(2024, mes, 0)).getUTCDate(); // 2024 = ano bissexto, cobre 29/02
  if (dia < 1 || dia > diasNoMes) return undefined;
  return `${String(dia).padStart(2, '0')}/${String(mes).padStart(2, '0')}`;
}

// ───────────────────────── Sparkline (KPI hero "Clientes cadastrados") ─────────────────────────

export interface SparklineGeometria {
  viewW: number;
  viewH: number;
  path: string;
  area: string;
  lastPoint: [number, number];
}

/** Mesma matemática do `Sparkline` de Compras — aqui a série é contagem (`number[]`), não `Centavos[]`. */
export function buildSparkline(valores: number[]): SparklineGeometria {
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

/** Ticket médio de exibição — 0 quando o cliente nunca comprou (evita `NaN`/divisão por zero na UI). */
export function ticketMedioExibicaoCentavos(cliente: Cliente): Centavos {
  return cliente.comprasCount > 0 ? cliente.ticketMedioCentavos : 0;
}

/** Frequência média de compra em dias (dias desde o cadastro ÷ nº de compras) — `null` quando o
 *  cliente ainda não comprou (evita divisão por zero e uma "frequência" sem sentido pra 0 compras). */
export function frequenciaMediaDias(cliente: Cliente, hojeDDMMAAAA: string): number | null {
  if (cliente.comprasCount <= 0) return null;
  const diasDesdeCadastro = Math.max(1, diasEntre(cliente.criadoEm, hojeDDMMAAAA));
  return Math.round(diasDesdeCadastro / cliente.comprasCount);
}
