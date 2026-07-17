import type { Centavos } from '@/lib/money';

import { formatCentavosWhole } from './MoneyWhole';
import type { DiaSemanaAbrev, SessaoCaixa, SessaoCaixaFechada } from './types';

/** Nome completo, minúsculo, sem "-feira" — o encurtamento que o mockup usa nos drills
 * ("quinta", não "quinta-feira"; ver `WD_FULL` do protótipo). */
export const DIA_SEMANA_COMPLETO: Record<DiaSemanaAbrev, string> = {
  Dom: 'domingo',
  Seg: 'segunda',
  Ter: 'terça',
  Qua: 'quarta',
  Qui: 'quinta',
  Sex: 'sexta',
  Sáb: 'sábado',
};

/** "16" → "16/07". O mês é fixo (o período corrente da tela), como no protótipo. */
export function diaLabel(dia: number): string {
  return `${String(dia).padStart(2, '0')}/07`;
}

/** Relógio de parede em "HH:MM" — usado ao registrar fechamento/sangria agora. */
export function horaAgora(): string {
  const agora = new Date();
  return `${String(agora.getHours()).padStart(2, '0')}:${String(agora.getMinutes()).padStart(2, '0')}`;
}

export function totalSangriasCentavos(sessao: SessaoCaixa): Centavos {
  return sessao.sangrias.reduce((soma, s) => soma + s.valorCentavos, 0);
}

export function totalSuprimentosCentavos(sessao: SessaoCaixa): Centavos {
  return sessao.suprimentos.reduce((soma, s) => soma + s.valorCentavos, 0);
}

/** O que a gaveta DEVERIA ter: abertura + vendas em espécie + suprimentos − sangrias − troco dado.
 * Nunca é armazenado — sempre recalculado dos primitivos, pra não existir um "esperado"
 * desatualizado. Espelha `SessaoCaixa.SaldoEsperado = abertura + entradas − saídas` do domínio
 * real, onde entradas = suprimento + vendaEmEspecie (ver `adapters/financeiro/fluxoCaixa.ts`). */
export function esperadoCentavos(sessao: SessaoCaixa): Centavos {
  return (
    sessao.aberturaCentavos +
    sessao.vendasEspecieCentavos +
    totalSuprimentosCentavos(sessao) -
    totalSangriasCentavos(sessao) -
    sessao.trocoCentavos
  );
}

/** Diferença = contado − esperado. `null` enquanto a sessão está aberta (ainda sem contagem). */
export function diferencaCentavos(sessao: SessaoCaixa): Centavos | null {
  if (sessao.status === 'aberto') return null;
  return sessao.contadoCentavos - esperadoCentavos(sessao);
}

/** "10h 17min" entre dois horários "HH:MM" (vira o dia se o fechamento for depois da meia-noite). */
export function duracaoTurno(horaAbertura: string, horaFechamento: string): string {
  const [oh, om] = horaAbertura.split(':').map(Number);
  const [ch, cm] = horaFechamento.split(':').map(Number);
  let minutos = ch * 60 + cm - (oh * 60 + om);
  if (minutos < 0) minutos += 24 * 60;
  return `${Math.floor(minutos / 60)}h ${minutos % 60}min`;
}

export function sessoesFechadas(sessoes: SessaoCaixa[]): SessaoCaixaFechada[] {
  return sessoes.filter((s): s is SessaoCaixaFechada => s.status === 'fechado');
}

export interface EstatisticasMes {
  totalDiferencaCentavos: Centavos;
  quantidadeFaltas: number;
  quantidadeSobras: number;
  diasFechados: number;
}

export function calcularEstatisticasMes(todasAsSessoes: SessaoCaixa[]): EstatisticasMes {
  const fechadas = sessoesFechadas(todasAsSessoes);
  let total = 0;
  let faltas = 0;
  let sobras = 0;
  fechadas.forEach((s) => {
    const diff = diferencaCentavos(s) ?? 0;
    total += diff;
    if (diff < 0) faltas += 1;
    else if (diff > 0) sobras += 1;
  });
  return { totalDiferencaCentavos: total, quantidadeFaltas: faltas, quantidadeSobras: sobras, diasFechados: fechadas.length };
}

export interface DiaCritico {
  diaSemana: DiaSemanaAbrev;
  mediaCentavos: Centavos;
}

/** O dia da semana com a PIOR média de diferença (mais falta) entre as sessões fechadas — vira o
 * "dia crítico" do card "Padrão do caixa". */
export function calcularDiaCritico(todasAsSessoes: SessaoCaixa[]): DiaCritico | null {
  const fechadas = sessoesFechadas(todasAsSessoes);
  const porDia = new Map<DiaSemanaAbrev, Centavos[]>();
  fechadas.forEach((s) => {
    const diff = diferencaCentavos(s) ?? 0;
    const lista = porDia.get(s.diaSemana) ?? [];
    lista.push(diff);
    porDia.set(s.diaSemana, lista);
  });
  let pior: DiaCritico | null = null;
  porDia.forEach((diffs, diaSemana) => {
    const media = diffs.reduce((a, b) => a + b, 0) / diffs.length;
    if (!pior || media < pior.mediaCentavos) pior = { diaSemana, mediaCentavos: media };
  });
  return pior;
}

/** "Ter" → "terças" — plural do dia crítico p/ a copy do Super Consultor ("Ver as quintas →"). */
const DIA_SEMANA_PLURAL: Record<DiaSemanaAbrev, string> = {
  Dom: 'domingos',
  Seg: 'segundas',
  Ter: 'terças',
  Qua: 'quartas',
  Qui: 'quintas',
  Sex: 'sextas',
  Sáb: 'sábados',
};
export function diaSemanaPlural(dia: DiaSemanaAbrev): string {
  return DIA_SEMANA_PLURAL[dia];
}

/** Operador com mais fechamentos naquele dia da semana (moda) — "sempre no fechamento sozinho de
 * X" do card do Super Consultor, derivado das sessões reais, nunca hardcoded. */
export function operadorMaisFrequenteNoDia(todasAsSessoes: SessaoCaixa[], diaSemana: DiaSemanaAbrev): string | null {
  const contagem = new Map<string, number>();
  sessoesFechadas(todasAsSessoes)
    .filter((s) => s.diaSemana === diaSemana)
    .forEach((s) => contagem.set(s.operador, (contagem.get(s.operador) ?? 0) + 1));
  let maisFrequente: string | null = null;
  let maiorContagem = 0;
  contagem.forEach((qtd, operador) => {
    if (qtd > maiorContagem) {
      maisFrequente = operador;
      maiorContagem = qtd;
    }
  });
  return maisFrequente;
}

export interface FaltasSobrasMes {
  faltasCentavos: Centavos;
  sobrasCentavos: Centavos;
}

/** Soma separada de faltas (magnitude positiva) e sobras do mês — "as faltas somam X (parcialmente
 * compensadas por Y de sobra)" do Super Consultor. */
export function calcularFaltasSobrasMes(todasAsSessoes: SessaoCaixa[]): FaltasSobrasMes {
  let faltasCentavos = 0;
  let sobrasCentavos = 0;
  sessoesFechadas(todasAsSessoes).forEach((s) => {
    const diff = diferencaCentavos(s) ?? 0;
    if (diff < 0) faltasCentavos += Math.abs(diff);
    else if (diff > 0) sobrasCentavos += diff;
  });
  return { faltasCentavos, sobrasCentavos };
}

export function calcularMediaDiferencaDia(todasAsSessoes: SessaoCaixa[]): Centavos {
  const fechadas = sessoesFechadas(todasAsSessoes);
  if (fechadas.length === 0) return 0;
  const total = fechadas.reduce((soma, s) => soma + (diferencaCentavos(s) ?? 0), 0);
  return total / fechadas.length;
}

export interface SangriasMes {
  totalCentavos: Centavos;
  quantidade: number;
}

export function calcularSangriasMes(todasAsSessoes: SessaoCaixa[]): SangriasMes {
  let total = 0;
  let quantidade = 0;
  todasAsSessoes.forEach((s) => {
    s.sangrias.forEach((sg) => {
      total += sg.valorCentavos;
      quantidade += 1;
    });
  });
  return { totalCentavos: total, quantidade };
}

/** "em aberto" / "bateu certinho" / "sobra R$ X" / "falta R$ X" — usado no hint do drill do dia. */
export function descreverDiferenca(sessao: SessaoCaixa): string {
  if (sessao.status === 'aberto') return 'em aberto';
  const diff = diferencaCentavos(sessao) ?? 0;
  if (diff === 0) return 'bateu certinho';
  return diff > 0 ? `sobra ${formatCentavosWhole(diff)}` : `falta ${formatCentavosWhole(Math.abs(diff))}`;
}

/** Valor + sufixo do KPI "Na gaveta agora": esperado enquanto aberta, contado assim que fecha. */
export function valorNaGaveta(sessao: SessaoCaixa): { centavos: Centavos; sufixo: string } {
  if (sessao.status === 'aberto') return { centavos: esperadoCentavos(sessao), sufixo: '(esperado)' };
  return { centavos: sessao.contadoCentavos, sufixo: '(fechado)' };
}

/** Rodapé do KPI "Caixa de hoje". */
export function descreverCaixaHojeFoot(sessao: SessaoCaixa): string {
  if (sessao.status === 'aberto') {
    return `${sessao.horaAbertura} · ${sessao.operador} · abertura ${formatCentavosWhole(sessao.aberturaCentavos)}`;
  }
  return `fechado ${sessao.horaFechamento} · ${sessao.operador}`;
}
