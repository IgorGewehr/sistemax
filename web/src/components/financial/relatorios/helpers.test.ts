import { describe, it, expect } from 'vitest';

import { docGenKey, getDocGenState, agingWidths, toggleAccountSelection, extratoSummaryLabel } from './helpers';
import type { AccountOption, AgingBucket, DocGenState } from './types';

function bucket(overrides: Partial<AgingBucket> = {}): AgingBucket {
  return { id: 'b1', label: '0-15d', amountCentavos: 1000, colorVar: 'var(--warn)', ...overrides };
}

describe('docGenKey / getDocGenState', () => {
  it('a chave combina cardId e format', () => {
    expect(docGenKey('mrr', 'PDF')).toBe('mrr:PDF');
  });

  it('estado ausente no mapa retorna "idle"', () => {
    expect(getDocGenState({}, 'mrr', 'PDF')).toBe('idle');
  });

  it('acha o estado correto quando presente', () => {
    const map: Record<string, DocGenState> = { 'mrr:PDF': 'generating' };
    expect(getDocGenState(map, 'mrr', 'PDF')).toBe('generating');
  });

  it('não confunde formatos diferentes do mesmo card', () => {
    const map: Record<string, DocGenState> = { 'mrr:PDF': 'done' };
    expect(getDocGenState(map, 'mrr', 'Excel')).toBe('idle');
  });
});

describe('agingWidths', () => {
  it('larguras proporcionais ao valor de cada faixa, somando ~100', () => {
    const buckets = [bucket({ amountCentavos: 500 }), bucket({ amountCentavos: 500 })];
    const widths = agingWidths(buckets);
    expect(widths).toEqual([50, 50]);
    expect(widths.reduce((a, b) => a + b, 0)).toBeCloseTo(100, 5);
  });

  it('total zero retorna larguras zeradas, não NaN/Infinity', () => {
    const buckets = [bucket({ amountCentavos: 0 }), bucket({ amountCentavos: 0 })];
    expect(agingWidths(buckets)).toEqual([0, 0]);
  });

  it('lista vazia retorna lista vazia', () => {
    expect(agingWidths([])).toEqual([]);
  });

  it('nunca diverge do total real exibido — largura é sempre derivada, nunca hardcoded', () => {
    const buckets = [bucket({ amountCentavos: 300 }), bucket({ amountCentavos: 700 })];
    const total = buckets.reduce((s, b) => s + b.amountCentavos, 0);
    const widths = agingWidths(buckets);
    buckets.forEach((b, i) => {
      expect(widths[i]).toBeCloseTo((b.amountCentavos / total) * 100, 5);
    });
  });
});

describe('toggleAccountSelection', () => {
  it('clicar em "todas" é exclusivo — zera qualquer seleção anterior', () => {
    expect(toggleAccountSelection(['a', 'b'], 'todas')).toEqual(['todas']);
  });

  it('selecionar uma conta específica remove "todas" da seleção', () => {
    expect(toggleAccountSelection(['todas'], 'a')).toEqual(['a']);
  });

  it('clicar de novo numa conta já selecionada a remove', () => {
    expect(toggleAccountSelection(['a', 'b'], 'a')).toEqual(['b']);
  });

  it('desmarcar a última conta específica volta pra "todas" sozinha', () => {
    expect(toggleAccountSelection(['a'], 'a')).toEqual(['todas']);
  });

  it('adiciona uma nova conta à seleção existente', () => {
    expect(toggleAccountSelection(['a'], 'b')).toEqual(['a', 'b']);
  });
});

describe('extratoSummaryLabel', () => {
  const accounts: AccountOption[] = [
    { id: 'a', label: 'Conta A' },
    { id: 'b', label: 'Conta B' },
  ];

  it('"todas" selecionada produz label fixo', () => {
    expect(extratoSummaryLabel(['todas'], accounts)).toBe('Selecionado: todas as contas');
  });

  it('contas específicas produzem lista de rótulos', () => {
    expect(extratoSummaryLabel(['a', 'b'], accounts)).toBe('Selecionado: Conta A, Conta B');
  });

  it('uma única conta selecionada', () => {
    expect(extratoSummaryLabel(['a'], accounts)).toBe('Selecionado: Conta A');
  });
});
