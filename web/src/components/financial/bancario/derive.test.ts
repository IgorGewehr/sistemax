import { describe, it, expect } from 'vitest';

import {
  somaCentavos,
  saldoTotalContas,
  semanaEntrouTotal,
  semanaSaiuTotal,
  mesEntrouTotal,
  mesSaiuTotal,
  formatSaldoFoot,
  pendingCount,
  pendingTotalCentavos,
  computeDivergentLayout,
  type DivergentChartItem,
} from './derive';
import type { ContaBancaria, SemanaMovimento } from './types';

/**
 * Conciliação bancária é a maior superfície de risco monetário do módulo Financeiro: uma soma
 * errada aqui gera saldo divergente sem ninguém perceber. Todo teste que soma dinheiro checa
 * também o caso vazio (nunca NaN) e o caso negativo (estorno/ajuste não pode "sumir" ao somar).
 */

function conta(overrides: Partial<ContaBancaria> = {}): ContaBancaria {
  return { id: 'c1', label: 'Itaú', saldoCentavos: 10_000, dotClassName: 'bg-fg/60', ...overrides };
}

function semana(overrides: Partial<SemanaMovimento> = {}): SemanaMovimento {
  return {
    id: 1,
    label: 'Semana 1',
    parcial: false,
    diasLabel: ['Seg', 'Ter', 'Qua', 'Qui', 'Sex', 'Sáb', 'Dom'],
    entrouPorDiaCentavos: [1000, 0, 0, 0, 0, 0, 0],
    saiuPorDiaCentavos: [0, 500, 0, 0, 0, 0, 0],
    ...overrides,
  };
}

describe('somaCentavos', () => {
  it('soma valores positivos e negativos sem perder centavo', () => {
    expect(somaCentavos([100, 200, -50])).toBe(250);
  });

  it('lista vazia soma 0, não undefined/NaN', () => {
    expect(somaCentavos([])).toBe(0);
  });

  it('só negativos (estornos) soma um total negativo, não zera', () => {
    expect(somaCentavos([-100, -200])).toBe(-300);
  });
});

describe('saldoTotalContas', () => {
  it('soma o saldo de todas as contas conectadas', () => {
    const contas = [conta({ saldoCentavos: 10_000 }), conta({ saldoCentavos: 5_000 }), conta({ saldoCentavos: -2_000 })];
    expect(saldoTotalContas(contas)).toBe(13_000);
  });

  it('sem contas conectadas retorna 0', () => {
    expect(saldoTotalContas([])).toBe(0);
  });
});

describe('semanaEntrouTotal / semanaSaiuTotal', () => {
  it('soma os centavos por dia da semana', () => {
    const s = semana({ entrouPorDiaCentavos: [100, 200, 300], saiuPorDiaCentavos: [50, 50] });
    expect(semanaEntrouTotal(s)).toBe(600);
    expect(semanaSaiuTotal(s)).toBe(100);
  });

  it('semana sem nenhum dia lançado soma 0', () => {
    const s = semana({ entrouPorDiaCentavos: [], saiuPorDiaCentavos: [] });
    expect(semanaEntrouTotal(s)).toBe(0);
    expect(semanaSaiuTotal(s)).toBe(0);
  });
});

describe('mesEntrouTotal / mesSaiuTotal', () => {
  it('agrega o total do mês somando todas as semanas', () => {
    const semanas = [
      semana({ id: 1, entrouPorDiaCentavos: [100, 100], saiuPorDiaCentavos: [50] }),
      semana({ id: 2, entrouPorDiaCentavos: [200], saiuPorDiaCentavos: [75, 25] }),
    ];
    expect(mesEntrouTotal(semanas)).toBe(400);
    expect(mesSaiuTotal(semanas)).toBe(150);
  });

  it('mês sem semanas soma 0, nunca NaN', () => {
    expect(mesEntrouTotal([])).toBe(0);
    expect(mesSaiuTotal([])).toBe(0);
  });

  it('invariante: total do mês === soma dos totais semanais individuais (nenhum centavo perdido na agregação)', () => {
    const semanas = [
      semana({ id: 1, entrouPorDiaCentavos: [111, 222], saiuPorDiaCentavos: [40] }),
      semana({ id: 2, entrouPorDiaCentavos: [333], saiuPorDiaCentavos: [10, 20] }),
      semana({ id: 3, entrouPorDiaCentavos: [1], saiuPorDiaCentavos: [1] }),
    ];
    const somaEntrouIndividual = semanas.reduce((acc, s) => acc + semanaEntrouTotal(s), 0);
    const somaSaiuIndividual = semanas.reduce((acc, s) => acc + semanaSaiuTotal(s), 0);
    expect(mesEntrouTotal(semanas)).toBe(somaEntrouIndividual);
    expect(mesSaiuTotal(semanas)).toBe(somaSaiuIndividual);
  });
});

describe('formatSaldoFoot', () => {
  it('junta rótulo + saldo formatado de cada conta com " · "', () => {
    const contas = [conta({ label: 'Itaú', saldoCentavos: 812_000 }), conta({ label: 'Nubank', saldoCentavos: 341_000 })];
    const fmt = (v: number) => `R$ ${(v / 100).toFixed(0)}`;
    expect(formatSaldoFoot(contas, fmt)).toBe('Itaú R$ 8120 · Nubank R$ 3410');
  });

  it('sem contas retorna string vazia', () => {
    expect(formatSaldoFoot([], (v) => String(v))).toBe('');
  });

  it('conta única não tem separador sobrando', () => {
    const contas = [conta({ label: 'Stone', saldoCentavos: 100 })];
    expect(formatSaldoFoot(contas, (v) => `R$${v}`)).toBe('Stone R$100');
  });
});

describe('pendingCount', () => {
  it('soma o tamanho dos dois baldes (sobrou no banco + sobrou no sistema)', () => {
    expect(pendingCount([1, 2, 3], [1, 2])).toBe(5);
  });

  it('ambos vazios conta 0', () => {
    expect(pendingCount([], [])).toBe(0);
  });
});

describe('pendingTotalCentavos', () => {
  it('soma o valor absoluto dos itens pendentes dos dois baldes', () => {
    const banco = [{ valorCentavos: 1000 }, { valorCentavos: -500 }];
    const sistema = [{ valorCentavos: 200 }];
    expect(pendingTotalCentavos(banco, sistema)).toBe(1700); // |1000| + |-500| + |200|
  });

  it('listas vazias somam 0', () => {
    expect(pendingTotalCentavos([], [])).toBe(0);
  });

  it('usa valor absoluto — sinal negativo não cancela o total (perda de centavo seria bug)', () => {
    const banco = [{ valorCentavos: -100 }];
    const sistema = [{ valorCentavos: -100 }];
    expect(pendingTotalCentavos(banco, sistema)).toBe(200); // não 0
  });
});

describe('computeDivergentLayout', () => {
  it('viewBox, zeroY e limites do eixo x são fixos, réplica 1:1 do mockup', () => {
    const layout = computeDivergentLayout([], false);
    expect(layout.viewBox).toBe('0 0 350 150');
    expect(layout.zeroY).toBe(62);
    expect(layout.x0).toBe(18);
    expect(layout.x1).toBe(332);
  });

  it('lista vazia gera 0 barras, sem dividir por zero', () => {
    expect(computeDivergentLayout([], false).bars).toEqual([]);
  });

  it('o item com o maior valor da série encosta exatamente no topo do gráfico (upY === top)', () => {
    const items: DivergentChartItem[] = [
      { id: 'a', label: 'Seg', entrouCentavos: 10_000, saiuCentavos: 0 },
      { id: 'b', label: 'Ter', entrouCentavos: 5_000, saiuCentavos: 0 },
    ];
    const layout = computeDivergentLayout(items, false);
    expect(layout.bars[0].upY).toBe(8); // top
    expect(layout.bars[0].upHeight).toBeCloseTo(54, 5); // capUp = zeroY - top
  });

  it('todos os valores zero não gera NaN (maxV usa piso de 1 no divisor)', () => {
    const items: DivergentChartItem[] = [{ id: 'a', label: 'Seg', entrouCentavos: 0, saiuCentavos: 0 }];
    const layout = computeDivergentLayout(items, false);
    expect(layout.bars[0].upHeight).toBe(0);
    expect(layout.bars[0].downHeight).toBe(0);
    expect(Number.isNaN(layout.bars[0].upY)).toBe(false);
    expect(Number.isNaN(layout.bars[0].downY)).toBe(false);
  });

  it('item muted reduz opacidade; item normal fica opaco', () => {
    const items: DivergentChartItem[] = [
      { id: 'a', label: 'Seg', entrouCentavos: 100, saiuCentavos: 100, muted: true },
      { id: 'b', label: 'Ter', entrouCentavos: 100, saiuCentavos: 100 },
    ];
    const layout = computeDivergentLayout(items, false);
    expect(layout.bars[0].opacity).toBe(0.55);
    expect(layout.bars[1].opacity).toBe(1);
  });

  it('clickable=true adiciona colBg (área de clique); clickable=false omite', () => {
    const items: DivergentChartItem[] = [{ id: 'a', label: 'Seg', entrouCentavos: 100, saiuCentavos: 50 }];
    expect(computeDivergentLayout(items, true).bars[0].colBg).toBeDefined();
    expect(computeDivergentLayout(items, false).bars[0].colBg).toBeUndefined();
  });

  it('preserva entrouCentavos/saiuCentavos originais em cada barra — geometria não altera o valor monetário', () => {
    const items: DivergentChartItem[] = [{ id: 'a', label: 'Seg', entrouCentavos: 12_345, saiuCentavos: 6_789 }];
    const layout = computeDivergentLayout(items, false);
    expect(layout.bars[0].entrouCentavos).toBe(12_345);
    expect(layout.bars[0].saiuCentavos).toBe(6_789);
  });

  it('barras distribuídas em slots iguais ao longo do eixo x', () => {
    const items: DivergentChartItem[] = [
      { id: 'a', label: 'Seg', entrouCentavos: 100, saiuCentavos: 0 },
      { id: 'b', label: 'Ter', entrouCentavos: 100, saiuCentavos: 0 },
      { id: 'c', label: 'Qua', entrouCentavos: 100, saiuCentavos: 0 },
    ];
    const layout = computeDivergentLayout(items, false);
    const slot = (332 - 18) / 3;
    const cx = (bar: (typeof layout.bars)[number]) => bar.barX + bar.barWidth / 2;
    expect(cx(layout.bars[1]) - cx(layout.bars[0])).toBeCloseTo(slot, 5);
    expect(cx(layout.bars[2]) - cx(layout.bars[1])).toBeCloseTo(slot, 5);
  });
});
