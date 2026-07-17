import { describe, it, expect } from 'vitest';

import {
  reais,
  formatCentavos,
  formatSignedCentavos,
  formatCentavosWhole,
  formatSignedCentavosWhole,
} from './money';

/**
 * `Intl.NumberFormat('pt-BR', { style: 'currency' })` separa "R$" do valor com um
 * NBSP (U+00A0), não um espaço ASCII normal — visualmente idêntico no terminal/editor,
 * mas `toBe('R$ 1,00')` com espaço comum falha silenciosamente confuso. Helper centraliza
 * o caractere certo pra não espalhar ` ` cru pelos asserts.
 */
const NBSP = ' ';
function brl(valor: string): string {
  return `R$${NBSP}${valor}`;
}

describe('reais', () => {
  it('converte reais em centavos inteiros', () => {
    expect(reais(200)).toBe(20000);
    expect(reais(1)).toBe(100);
    expect(reais(0)).toBe(0);
  });

  it('arredonda fração de centavo (evita float sujo tipo 19.99999999)', () => {
    expect(reais(19.999)).toBe(2000);
    expect(reais(0.005)).toBe(1); // 0.5 centavo arredonda pra cima
    expect(reais(10.005)).toBe(1001); // clássica armadilha de float: 10.005*100 = 1000.5000000000001
  });

  it('aceita valores negativos (estornos/ajustes)', () => {
    expect(reais(-50)).toBe(-5000);
  });

  it('round-trip nunca perde centavo: reais(x/100) === x para inteiros de centavos', () => {
    for (const centavos of [0, 1, 99, 100, 12345, -12345, 999999]) {
      expect(reais(centavos / 100)).toBe(centavos);
    }
  });
});

describe('formatCentavos', () => {
  it('formata centavos em BRL com 2 casas', () => {
    expect(formatCentavos(123456)).toBe(brl('1.234,56'));
    expect(formatCentavos(100)).toBe(brl('1,00'));
  });

  it('zero é um valor formatável válido, não nulo', () => {
    expect(formatCentavos(0)).toBe(brl('0,00'));
  });

  it('negativos usam o formatador padrão do Intl (sinal "-" embutido)', () => {
    expect(formatCentavos(-500)).toBe(`-${brl('5,00')}`);
  });

  it('nulo/undefined/NaN viram travessão — nunca "R$ NaN" ou "R$ null"', () => {
    expect(formatCentavos(null)).toBe('—');
    expect(formatCentavos(undefined)).toBe('—');
    expect(formatCentavos(NaN)).toBe('—');
  });

  it('centavos fracionários (bug upstream) ainda formatam sem lançar', () => {
    // Centavos deveriam ser sempre inteiros, mas a função não deve explodir se não forem.
    expect(formatCentavos(150.5)).toBe(brl('1,51'));
  });
});

describe('formatSignedCentavos', () => {
  it('positivo ganha "+"', () => {
    expect(formatSignedCentavos(1200)).toBe(`+${brl('12,00')}`);
  });

  it('negativo usa menos unicode (−), não hífen ASCII, e módulo do valor', () => {
    expect(formatSignedCentavos(-4200)).toBe(`−${brl('42,00')}`);
  });

  it('zero não ganha sinal', () => {
    expect(formatSignedCentavos(0)).toBe(brl('0,00'));
  });

  it('nulo/undefined/NaN viram travessão', () => {
    expect(formatSignedCentavos(null)).toBe('—');
    expect(formatSignedCentavos(undefined)).toBe('—');
    expect(formatSignedCentavos(NaN)).toBe('—');
  });
});

describe('formatCentavosWhole', () => {
  it('arredonda pra reais inteiros, sem casas decimais', () => {
    expect(formatCentavosWhole(330000)).toBe(brl('3.300'));
    expect(formatCentavosWhole(329999)).toBe(brl('3.300')); // 3299.99 arredonda pra 3300
  });

  it('arredondamento .5 vai pra cima (round half up do Math.round)', () => {
    expect(formatCentavosWhole(50)).toBe(brl('1')); // 0.5 reais -> Math.round(0.5) = 1
  });

  it('zero é exibível', () => {
    expect(formatCentavosWhole(0)).toBe(brl('0'));
  });

  it('nulo/undefined/NaN viram travessão', () => {
    expect(formatCentavosWhole(null)).toBe('—');
    expect(formatCentavosWhole(undefined)).toBe('—');
    expect(formatCentavosWhole(NaN)).toBe('—');
  });
});

describe('formatSignedCentavosWhole', () => {
  it('positivo ganha "+", negativo usa menos unicode', () => {
    expect(formatSignedCentavosWhole(65000)).toBe(`+${brl('650')}`);
    expect(formatSignedCentavosWhole(-490000)).toBe(`−${brl('4.900')}`);
  });

  it('zero não ganha sinal', () => {
    expect(formatSignedCentavosWhole(0)).toBe(brl('0'));
  });

  it('nulo/undefined/NaN viram travessão', () => {
    expect(formatSignedCentavosWhole(null)).toBe('—');
    expect(formatSignedCentavosWhole(undefined)).toBe('—');
    expect(formatSignedCentavosWhole(NaN)).toBe('—');
  });
});
