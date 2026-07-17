import { describe, it, expect } from 'vitest';

import { buildSparkline, filtrarVendasTabela, formatPct1, deltaTone, formatFormasPagamento } from './calc';
import type { FiltrosVendas, VendaRow } from './types';

/**
 * Espelha o estilo de `components/compras/calc.test.ts` (mesma matemática de sparkline/pct/tone) —
 * factories com overrides, um `describe` por função exportada.
 */

function venda(overrides: Partial<VendaRow> = {}): VendaRow {
  return {
    id: 'v1',
    numero: 'V-00001',
    dataHoraLabel: '16/07 14:32',
    canal: 'Caixa 01',
    operador: 'Maria',
    clienteNome: 'João Silva',
    status: 'Concluida',
    itens: [],
    pagamentos: [],
    formasPagamento: ['Pix'],
    descontoCentavos: 0,
    subtotalCentavos: 10_000,
    totalCentavos: 10_000,
    ...overrides,
  };
}

function filtros(overrides: Partial<FiltrosVendas> = {}): FiltrosVendas {
  return { canal: 'todos', operador: 'todos', formaPagamento: 'todas', apenasEstornadas: false, busca: '', ...overrides };
}

describe('formatPct1', () => {
  it('1 casa decimal, vírgula pt-BR', () => {
    expect(formatPct1(14)).toBe('14,0');
    expect(formatPct1(14.05)).toBe('14,1'); // arredonda pra 1 casa
    expect(formatPct1(-3.2)).toBe('-3,2');
  });
});

describe('deltaTone', () => {
  it('crescimento (>= 0) é sempre "pos" — nesta tela, vender mais é sempre bom', () => {
    expect(deltaTone(10)).toBe('pos');
    expect(deltaTone(0)).toBe('pos'); // zero não é queda
  });

  it('qualquer queda (< 0) é sempre "crit"', () => {
    expect(deltaTone(-0.1)).toBe('crit');
    expect(deltaTone(-50)).toBe('crit');
  });
});

describe('formatFormasPagamento', () => {
  it('uma forma retorna ela mesma, sem separador', () => {
    expect(formatFormasPagamento(['Pix'])).toBe('Pix');
  });

  it('duas ou mais formas juntam com " + " (pagamento dividido)', () => {
    expect(formatFormasPagamento(['Pix', 'Dinheiro'])).toBe('Pix + Dinheiro');
    expect(formatFormasPagamento(['Pix', 'Dinheiro', 'Credito'])).toBe('Pix + Dinheiro + Credito');
  });

  it('lista vazia retorna string vazia', () => {
    expect(formatFormasPagamento([])).toBe('');
  });
});

describe('filtrarVendasTabela', () => {
  const vendas: VendaRow[] = [
    venda({ id: '1', numero: 'V-00001', status: 'Concluida', canal: 'Caixa 01', operador: 'Maria', formasPagamento: ['Pix'], clienteNome: 'João Silva' }),
    venda({ id: '2', numero: 'V-00002', status: 'Estornada', canal: 'Caixa 02', operador: 'Pedro', formasPagamento: ['Dinheiro'], clienteNome: null }),
    venda({ id: '3', numero: 'V-00003', status: 'Aberta', canal: 'Caixa 01', operador: 'Maria', formasPagamento: ['Credito', 'Dinheiro'], clienteNome: 'Ana Souza' }),
  ];

  it('sem nenhum filtro ativo, retorna tudo', () => {
    expect(filtrarVendasTabela(vendas, filtros())).toHaveLength(3);
  });

  it('apenasEstornadas filtra só as vendas com status Estornada', () => {
    const r = filtrarVendasTabela(vendas, filtros({ apenasEstornadas: true }));
    expect(r.map((v) => v.id)).toEqual(['2']);
  });

  it('canal filtra pelo terminal exato', () => {
    const r = filtrarVendasTabela(vendas, filtros({ canal: 'Caixa 01' }));
    expect(r.map((v) => v.id)).toEqual(['1', '3']);
  });

  it('operador filtra pelo operador exato', () => {
    const r = filtrarVendasTabela(vendas, filtros({ operador: 'Pedro' }));
    expect(r.map((v) => v.id)).toEqual(['2']);
  });

  it('formaPagamento acha venda mesmo em pagamento dividido (includes, não igualdade)', () => {
    const r = filtrarVendasTabela(vendas, filtros({ formaPagamento: 'Dinheiro' }));
    expect(r.map((v) => v.id)).toEqual(['2', '3']);
  });

  it('busca normaliza acento e caixa, acha por número, cliente ou operador', () => {
    expect(filtrarVendasTabela(vendas, filtros({ busca: 'joao' })).map((v) => v.id)).toEqual(['1']);
    expect(filtrarVendasTabela(vendas, filtros({ busca: 'ANA' })).map((v) => v.id)).toEqual(['3']);
    expect(filtrarVendasTabela(vendas, filtros({ busca: 'v-00002' })).map((v) => v.id)).toEqual(['2']);
  });

  it('busca não quebra quando clienteNome é null (consumidor final) — ainda acha pelo operador', () => {
    expect(filtrarVendasTabela(vendas, filtros({ busca: 'pedro' })).map((v) => v.id)).toEqual(['2']);
  });

  it('combina múltiplos filtros com AND', () => {
    const r = filtrarVendasTabela(vendas, filtros({ canal: 'Caixa 01', operador: 'Maria', formaPagamento: 'Credito' }));
    expect(r.map((v) => v.id)).toEqual(['3']);
  });

  it('filtro que não bate com nada retorna lista vazia', () => {
    expect(filtrarVendasTabela(vendas, filtros({ canal: 'Caixa 99' }))).toEqual([]);
  });
});

describe('buildSparkline', () => {
  it('gera path/area/lastPoint com base na série de centavos', () => {
    const s = buildSparkline([10_000, 20_000, 15_000, 30_000]);
    expect(s.path.startsWith('M')).toBe(true);
    expect(s.area).toContain(s.path);
    expect(s.lastPoint).toBeDefined();
    expect(s.viewW).toBe(240);
    expect(s.viewH).toBe(34);
  });

  it('série de 1 valor não divide por zero (x fixo em 0)', () => {
    const s = buildSparkline([50_000]);
    expect(s.lastPoint[0]).toBe(0);
  });

  it('série constante (span 0) não gera NaN — usa piso de 1 no divisor', () => {
    const s = buildSparkline([10_000, 10_000, 10_000]);
    expect(s.path.includes('NaN')).toBe(false);
    expect(s.area.includes('NaN')).toBe(false);
  });

  it('o maior valor da série fica mais alto no SVG (y menor) que os vizinhos', () => {
    const s = buildSparkline([10_000, 50_000, 20_000]);
    const pontos = s.path.split(' L').map((seg) => seg.replace('M', '').split(',').map(Number));
    expect(pontos[1][1]).toBeLessThan(pontos[0][1]);
    expect(pontos[1][1]).toBeLessThan(pontos[2][1]);
  });
});
