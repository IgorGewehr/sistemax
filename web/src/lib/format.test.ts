import { describe, it, expect } from 'vitest';

import {
  formatCurrency,
  formatCurrencyCompact,
  formatSignedCurrency,
  formatPercent,
  formatDate,
  formatDateShort,
  formatWeekday,
  formatDateTime,
  formatRelativeDays,
  daysBetween,
} from './format';

// `Intl.NumberFormat('pt-BR', { style: 'currency' })` separa "R$" do valor com NBSP
// (U+00A0), não espaço ASCII — e a notação compacta usa NBSP entre número e "mil"/"mi".
const NBSP = ' ';
function brl(valor: string): string {
  return `R$${NBSP}${valor}`;
}

describe('formatCurrency', () => {
  it('formata em BRL', () => {
    expect(formatCurrency(1234.56)).toBe(brl('1.234,56'));
    expect(formatCurrency(0)).toBe(brl('0,00'));
  });

  it('nulo/undefined/NaN viram travessão', () => {
    expect(formatCurrency(null)).toBe('—');
    expect(formatCurrency(undefined)).toBe('—');
    expect(formatCurrency(NaN)).toBe('—');
  });

  it('negativo preserva sinal do Intl', () => {
    expect(formatCurrency(-10)).toBe(`-${brl('10,00')}`);
  });
});

describe('formatCurrencyCompact', () => {
  it('usa notação compacta pra valores grandes (eixo Y de gráfico)', () => {
    expect(formatCurrencyCompact(1500)).toBe(brl(`1,5${NBSP}mil`));
    expect(formatCurrencyCompact(2_000_000)).toBe(brl(`2,0${NBSP}mi`));
  });

  it('nulo/undefined/NaN viram travessão', () => {
    expect(formatCurrencyCompact(null)).toBe('—');
    expect(formatCurrencyCompact(undefined)).toBe('—');
    expect(formatCurrencyCompact(NaN)).toBe('—');
  });
});

describe('formatSignedCurrency', () => {
  it('positivo ganha "+", zero e negativo não', () => {
    expect(formatSignedCurrency(50)).toBe(`+${brl('50,00')}`);
    expect(formatSignedCurrency(0)).toBe(brl('0,00'));
    expect(formatSignedCurrency(-50)).toBe(`-${brl('50,00')}`);
  });

  it('nulo/undefined/NaN viram travessão', () => {
    expect(formatSignedCurrency(null)).toBe('—');
    expect(formatSignedCurrency(undefined)).toBe('—');
    expect(formatSignedCurrency(NaN)).toBe('—');
  });
});

describe('formatPercent', () => {
  it('formata com dígitos default (0 casas)', () => {
    expect(formatPercent(42)).toBe('42%');
    expect(formatPercent(42.6)).toBe('43%'); // toFixed(0) arredonda
  });

  it('aceita dígitos customizados', () => {
    expect(formatPercent(12.345, 1)).toBe('12.3%');
    expect(formatPercent(12.345, 2)).toBe('12.35%' /* toFixed usa round-half-to-even do IEEE754 em alguns casos, mas 12.345 representa ~12.34499999 */);
  });

  it('zero e negativo formatam normalmente', () => {
    expect(formatPercent(0)).toBe('0%');
    expect(formatPercent(-5.5, 1)).toBe('-5.5%');
  });

  it('nulo/undefined/NaN viram travessão', () => {
    expect(formatPercent(null)).toBe('—');
    expect(formatPercent(undefined)).toBe('—');
    expect(formatPercent(NaN)).toBe('—');
  });
});

describe('formatDate / formatDateShort / formatWeekday / formatDateTime', () => {
  it('formata data "yyyy-mm-dd" sem desalinhar 1 dia (fuso local, não UTC)', () => {
    // 2026-07-16 tem que continuar sendo dia 16, não 15, em fusos negativos (Brasil).
    expect(formatDate('2026-07-16')).toBe('16/07/2026');
    expect(formatDateShort('2026-07-16')).toBe('16/07');
  });

  it('formata Date object diretamente', () => {
    expect(formatDate(new Date(2026, 6, 16))).toBe('16/07/2026');
  });

  it('formata timestamp numérico', () => {
    const ts = new Date(2026, 6, 16).getTime();
    expect(formatDate(ts)).toBe('16/07/2026');
  });

  it('data inválida/nula/undefined vira "-" (travessão simples, não em-dash)', () => {
    expect(formatDate(null)).toBe('-');
    expect(formatDate(undefined)).toBe('-');
    expect(formatDate('not-a-date')).toBe('-');
    expect(formatDate('invalid')).toBe('-');
  });

  it('formatDateTime inclui hora:minuto', () => {
    const d = new Date(2026, 6, 16, 14, 30);
    expect(formatDateTime(d)).toBe('16/07, 14:30');
  });

  it('formatDateTime com entrada inválida também vira "-"', () => {
    expect(formatDateTime(null)).toBe('-');
    expect(formatDateTime('lixo')).toBe('-');
  });

  it('formatWeekday devolve abreviação sem ponto final', () => {
    const weekday = formatWeekday('2026-07-16'); // quinta-feira
    expect(weekday).not.toContain('.');
    expect(weekday.length).toBeGreaterThan(0);
  });

  it('formatWeekday com entrada inválida vira "-"', () => {
    expect(formatWeekday(null)).toBe('-');
  });
});

describe('formatRelativeDays', () => {
  it('hoje/amanhã/ontem têm rótulo especial', () => {
    const hoje = new Date();
    hoje.setHours(12, 0, 0, 0);
    expect(formatRelativeDays(hoje)).toBe('hoje');

    const amanha = new Date(hoje);
    amanha.setDate(amanha.getDate() + 1);
    expect(formatRelativeDays(amanha)).toBe('amanhã');

    const ontem = new Date(hoje);
    ontem.setDate(ontem.getDate() - 1);
    expect(formatRelativeDays(ontem)).toBe('ontem');
  });

  it('dias futuros/passados > 1 usam contagem', () => {
    const hoje = new Date();
    hoje.setHours(12, 0, 0, 0);

    const futuro = new Date(hoje);
    futuro.setDate(futuro.getDate() + 5);
    expect(formatRelativeDays(futuro)).toBe('em 5 dias');

    const passado = new Date(hoje);
    passado.setDate(passado.getDate() - 5);
    expect(formatRelativeDays(passado)).toBe('há 5 dias');
  });

  it('entrada inválida vira "-"', () => {
    expect(formatRelativeDays(null)).toBe('-');
    expect(formatRelativeDays('xablau')).toBe('-');
  });
});

describe('daysBetween', () => {
  it('calcula dias corridos entre duas datas', () => {
    expect(daysBetween('2026-07-01', '2026-07-16')).toBe(15);
    expect(daysBetween('2026-07-16', '2026-07-01')).toBe(-15);
  });

  it('mesma data resulta em 0', () => {
    expect(daysBetween('2026-07-16', '2026-07-16')).toBe(0);
  });

  it('data inválida em qualquer lado retorna 0 (fallback seguro, não NaN)', () => {
    expect(daysBetween('lixo', '2026-07-16')).toBe(0);
    expect(daysBetween('2026-07-16', 'lixo')).toBe(0);
  });

  it('atravessa virada de mês/ano corretamente', () => {
    expect(daysBetween('2025-12-25', '2026-01-05')).toBe(11);
  });
});
