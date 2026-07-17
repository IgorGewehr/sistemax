import { describe, it, expect } from 'vitest';

import {
  fornById,
  notaById,
  formatPct1,
  deltaBadge,
  isItemPedido,
  custoUnitCentavosOf,
  matchCounts,
  pendentesPadrao,
  resolvidosOuIgnorados,
  itensComVariacao,
  buildHomeKpis,
  buildFornecedorRanking,
  atrasoDias,
  parcelasResumoTxt,
  itensQueEntram,
  fatorSugeridoNumero,
  buildSparkline,
} from './calc';
import type { Fornecedor, ItemNotaPadrao, ItemNotaPedido, NotaEntrada, Pedido } from './types';

function fornecedor(overrides: Partial<Fornecedor> = {}): Fornecedor {
  return {
    id: 'f1',
    nome: 'Fornecedor A',
    cnpj: null,
    status: 'ativo',
    comprado90dCentavos: 10_000,
    leadTimeRealDias: 5,
    leadTimePrometidoDias: 3,
    divergNotas: 0,
    divergTotal: 0,
    comprado12mCentavos: 40_000,
    ...overrides,
  };
}

function itemPadrao(overrides: Partial<ItemNotaPadrao> = {}): ItemNotaPadrao {
  return {
    nItem: 1,
    nome: 'Item A',
    cprod: 'c1',
    match: 'auto',
    nf: '1 UN x R$ 10,00',
    custoUnitCentavos: 1000,
    unidade: 'un',
    deltaPct: null,
    ...overrides,
  };
}

function itemPedido(overrides: Partial<ItemNotaPedido> = {}): ItemNotaPedido {
  return {
    nItem: 1,
    nome: 'Item B',
    cprod: 'c2',
    unidade: 'un',
    pedidoQtd: 10,
    pedidoPrecoCentavos: 500,
    notaQtd: 10,
    notaPrecoCentavos: 500,
    fisicoQtd: 10,
    deltaPct: null,
    ...overrides,
  };
}

function nota(overrides: Partial<NotaEntrada> = {}): NotaEntrada {
  return {
    id: 'n1',
    numero: 'NF-001',
    fornecedorId: 'f1',
    emissao: '10/07/2026',
    status: 'recebida',
    totalCentavos: 1000,
    vProdCentavos: 1000,
    vFreteCentavos: 0,
    vSeguroCentavos: 0,
    vOutroCentavos: 0,
    vDescontoCentavos: 0,
    vStCentavos: 0,
    vIpiCentavos: 0,
    parcelas: [],
    pedidoId: null,
    itens: [itemPadrao()],
    ...overrides,
  } as NotaEntrada;
}

function pedido(overrides: Partial<Pedido> = {}): Pedido {
  return {
    id: 'p1',
    numero: 'PC-001',
    fornecedorId: 'f1',
    status: 'enviado',
    enviado: '10/07/2026',
    previsto: '15/07/2026',
    totalCentavos: 5000,
    itensQtd: 2,
    notaId: null,
    ...overrides,
  };
}

describe('fornById / notaById', () => {
  it('acha por id, undefined quando não existe', () => {
    const forns = [fornecedor({ id: 'f1' }), fornecedor({ id: 'f2' })];
    expect(fornById(forns, 'f2')?.id).toBe('f2');
    expect(fornById(forns, 'inexistente')).toBeUndefined();
    expect(notaById([nota({ id: 'n1' })], 'n1')?.id).toBe('n1');
  });
});

describe('formatPct1', () => {
  it('1 casa decimal, vírgula pt-BR', () => {
    expect(formatPct1(14)).toBe('14,0');
    expect(formatPct1(14.05)).toBe('14,1'); // arredonda pra 1 casa
    expect(formatPct1(-3.2)).toBe('-3,2');
  });
});

describe('deltaBadge', () => {
  it('null/undefined vira "1ª compra"', () => {
    expect(deltaBadge(null)).toEqual({ kind: 'novo', label: '1ª compra' });
    expect(deltaBadge(undefined)).toEqual({ kind: 'novo', label: '1ª compra' });
  });

  it('0 vira "estável"', () => {
    expect(deltaBadge(0)).toEqual({ kind: 'flat', label: '▬ estável' });
  });

  it('alta abaixo de 12% é "up-bad", alta >= 12% é "up-crit"', () => {
    expect(deltaBadge(5).kind).toBe('up-bad');
    expect(deltaBadge(11.9).kind).toBe('up-bad');
    expect(deltaBadge(12).kind).toBe('up-crit'); // exatamente no limiar
    expect(deltaBadge(20).kind).toBe('up-crit');
  });

  it('queda é "down-good", com sinal negativo preservado no label', () => {
    const b = deltaBadge(-8);
    expect(b.kind).toBe('down-good');
    expect(b.label).toContain('-8,0%');
  });
});

describe('isItemPedido', () => {
  it('discrimina pela presença de pedidoQtd', () => {
    expect(isItemPedido(itemPedido())).toBe(true);
    expect(isItemPedido(itemPadrao())).toBe(false);
  });
});

describe('custoUnitCentavosOf', () => {
  it('item padrão retorna custoUnitCentavos, ou 0 se null', () => {
    expect(custoUnitCentavosOf(itemPadrao({ custoUnitCentavos: 1500 }))).toBe(1500);
    expect(custoUnitCentavosOf(itemPadrao({ custoUnitCentavos: null }))).toBe(0);
  });

  it('item de pedido sempre retorna 0 (não carrega custo unitário próprio nesta tela)', () => {
    expect(custoUnitCentavosOf(itemPedido())).toBe(0);
  });
});

describe('matchCounts', () => {
  it('conta cada categoria de match', () => {
    const itens = [
      itemPadrao({ match: 'auto' }),
      itemPadrao({ match: 'auto' }),
      itemPadrao({ match: 'sugerido' }),
      itemPadrao({ match: 'semmatch' }),
      itemPadrao({ match: 'ignorado' }), // não conta em nenhum dos 3 buckets
    ];
    expect(matchCounts(itens)).toEqual({ auto: 2, sugerido: 1, semmatch: 1 });
  });

  it('lista vazia produz zeros', () => {
    expect(matchCounts([])).toEqual({ auto: 0, sugerido: 0, semmatch: 0 });
  });
});

describe('pendentesPadrao / resolvidosOuIgnorados', () => {
  it('separa itens pendentes (sugerido/semmatch) de resolvidos (auto/ignorado)', () => {
    const itens = [
      itemPadrao({ match: 'auto' }),
      itemPadrao({ match: 'sugerido' }),
      itemPadrao({ match: 'semmatch' }),
      itemPadrao({ match: 'ignorado' }),
    ];
    expect(pendentesPadrao(itens)).toHaveLength(2);
    expect(resolvidosOuIgnorados(itens)).toHaveLength(2);
  });
});

describe('itensComVariacao', () => {
  it('inclui só itens com deltaPct definido e != 0, ordenado por |delta| desc', () => {
    const notas = [
      nota({
        id: 'n1',
        fornecedorId: 'f1',
        itens: [itemPadrao({ deltaPct: 5 }), itemPadrao({ deltaPct: null }), itemPadrao({ deltaPct: 0 }), itemPadrao({ deltaPct: -20 })],
      }),
    ];
    const forns = [fornecedor({ id: 'f1' })];
    const r = itensComVariacao(notas, forns);
    expect(r).toHaveLength(2);
    expect(r[0].item.deltaPct).toBe(-20); // maior |delta| primeiro
    expect(r[1].item.deltaPct).toBe(5);
  });

  it('ignora notas cujo fornecedor não existe (lookup órfão)', () => {
    const notas = [nota({ fornecedorId: 'inexistente', itens: [itemPadrao({ deltaPct: 10 })] })];
    expect(itensComVariacao(notas, [])).toHaveLength(0);
  });
});

describe('buildHomeKpis', () => {
  it('agrega compradoMes (soma de comprado90d dos fornecedores), pedidos abertos e notas a conferir', () => {
    const forns = [fornecedor({ id: 'f1', comprado90dCentavos: 5000 }), fornecedor({ id: 'f2', comprado90dCentavos: 3000 })];
    const pedidos = [
      pedido({ id: 'p1', status: 'enviado', totalCentavos: 1000 }),
      pedido({ id: 'p2', status: 'parcial', totalCentavos: 2000 }),
      pedido({ id: 'p3', status: 'recebido', totalCentavos: 9999 }), // não conta como aberto
    ];
    const notas = [
      nota({ id: 'n1', fornecedorId: 'f1', status: 'conferir', itens: [itemPadrao({ deltaPct: 10 })] }),
      nota({ id: 'n2', fornecedorId: 'f2', status: 'divergente', itens: [itemPadrao({ deltaPct: -5 })] }),
      nota({ id: 'n3', fornecedorId: 'f1', status: 'recebida' }),
    ];
    const kpis = buildHomeKpis(notas, pedidos, forns);
    expect(kpis.compradoMesCentavos).toBe(8000);
    expect(kpis.pedidosAbertos).toHaveLength(2);
    expect(kpis.pedidosAbertoTotalCentavos).toBe(3000);
    expect(kpis.notasConferir).toHaveLength(2);
    expect(kpis.notasComDivergencia).toBe(1);
    expect(kpis.subiram).toBe(1);
    expect(kpis.cairam).toBe(1);
  });
});

describe('buildFornecedorRanking', () => {
  it('top 3 por comprado90d, resto agregado, pcts somam ~100', () => {
    const forns = [
      fornecedor({ id: 'a', comprado90dCentavos: 4000 }),
      fornecedor({ id: 'b', comprado90dCentavos: 3000 }),
      fornecedor({ id: 'c', comprado90dCentavos: 2000 }),
      fornecedor({ id: 'd', comprado90dCentavos: 1000 }),
    ];
    const r = buildFornecedorRanking(forns);
    expect(r.totalCentavos).toBe(10_000);
    expect(r.top3.map((t) => t.fornecedor.id)).toEqual(['a', 'b', 'c']);
    expect(r.restoCount).toBe(1);
    expect(r.restoValorCentavos).toBe(1000);
    expect(r.restoPct).toBe(10);
    const somaPctTop3 = r.top3.reduce((s, t) => s + t.pct, 0);
    expect(somaPctTop3 + r.restoPct).toBeCloseTo(100, 5);
  });

  it('lista vazia: total 0, sem NaN de divisão', () => {
    const r = buildFornecedorRanking([]);
    expect(r.totalCentavos).toBe(0);
    expect(r.restoPct).toBe(0);
    expect(r.top3).toEqual([]);
  });
});

describe('atrasoDias', () => {
  it('positivo quando lead time real excede o prometido', () => {
    expect(atrasoDias(fornecedor({ leadTimeRealDias: 8, leadTimePrometidoDias: 5 }))).toBe(3);
  });

  it('negativo/zero quando dentro do combinado', () => {
    expect(atrasoDias(fornecedor({ leadTimeRealDias: 5, leadTimePrometidoDias: 5 }))).toBe(0);
    expect(atrasoDias(fornecedor({ leadTimeRealDias: 3, leadTimePrometidoDias: 5 }))).toBe(-2);
  });
});

describe('parcelasResumoTxt', () => {
  const fmt = (c: number) => `R$${(c / 100).toFixed(2)}`;

  it('sem parcelas: "à vista"', () => {
    expect(parcelasResumoTxt([], fmt)).toBe('à vista');
  });

  it('com parcelas: "Nx valor_da_primeira"', () => {
    expect(parcelasResumoTxt([{ valorCentavos: 1000 }, { valorCentavos: 1000 }], fmt)).toBe('2× R$10.00');
  });
});

describe('itensQueEntram', () => {
  it('conta tudo exceto itens marcados como ignorado', () => {
    const itens = [itemPadrao({ match: 'auto' }), itemPadrao({ match: 'ignorado' }), itemPedido()];
    expect(itensQueEntram(itens)).toBe(2);
  });
});

describe('fatorSugeridoNumero', () => {
  it('extrai o número depois do "=" — não concatena o "1" de "1 CX" com o número da conversão', () => {
    expect(fatorSugeridoNumero('1 CX = 10,000 kg')).toBe('10,000');
  });

  it('sem fatorSugerido, usa o fallback "10,000"', () => {
    expect(fatorSugeridoNumero(undefined)).toBe('10,000');
  });

  it('string sem "=" cai no fallback', () => {
    expect(fatorSugeridoNumero('formato inesperado')).toBe('10,000');
  });
});

describe('buildSparkline', () => {
  it('gera path/area/lastPoint com base na série', () => {
    const s = buildSparkline([100, 200, 150, 300]);
    expect(s.path.startsWith('M')).toBe(true);
    expect(s.area).toContain(s.path);
    expect(s.lastPoint).toBeDefined();
  });

  it('série de 1 valor não divide por zero (n<=1 -> x=0)', () => {
    const s = buildSparkline([100]);
    expect(s.lastPoint[0]).toBe(0);
  });

  it('série constante (span 0) não gera NaN — usa piso de 1 no divisor', () => {
    const s = buildSparkline([100, 100, 100]);
    expect(s.path.includes('NaN')).toBe(false);
  });
});
