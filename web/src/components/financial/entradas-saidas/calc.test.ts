import { describe, it, expect } from 'vitest';

import {
  mediaHistoricoAnterior,
  variacaoPct,
  isAnomalia,
  totalDespesasCentavos,
  buildBarras,
  fixoVariavelPct,
  quemMaisSubiu,
  categoriaDrillStats,
  atrasados30MaisDias,
  buildTimeline,
  insertLancamentoOrdenado,
  buildColunasGeometry,
  TODAY_ISO,
} from './calc';
import type { CategoriaDespesaResumo, LancamentoRow } from './types';

function categoria(overrides: Partial<CategoriaDespesaResumo> = {}): CategoriaDespesaResumo {
  return {
    id: 'aluguel',
    nome: 'Aluguel',
    cor: 'primary',
    fixo: true,
    totalCentavos: 100_000,
    historicoCentavos: [100_000, 100_000, 100_000, 100_000, 100_000, 100_000],
    maiorLancamento: { desc: 'Aluguel', valorCentavos: 100_000 },
    ...overrides,
  };
}

function lancamento(overrides: Partial<LancamentoRow> = {}): LancamentoRow {
  return {
    id: 'r1',
    data: '2026-07-01',
    desc: 'Aluguel',
    sub: null,
    categoria: 'aluguel',
    tipo: 'saida',
    status: 'pago',
    valorCentavos: 100_000,
    ...overrides,
  };
}

describe('mediaHistoricoAnterior', () => {
  it('calcula a média dos meses anteriores ao último (mês corrente)', () => {
    expect(mediaHistoricoAnterior([100, 200, 300])).toBe(150); // média(100,200), exclui 300
  });

  it('lista com só o mês corrente (sem histórico) retorna 0, não NaN', () => {
    expect(mediaHistoricoAnterior([500])).toBe(0);
  });

  it('lista vazia retorna 0', () => {
    expect(mediaHistoricoAnterior([])).toBe(0);
  });
});

describe('variacaoPct', () => {
  it('calcula variação do total atual vs a média do histórico anterior', () => {
    const c = categoria({ totalCentavos: 1200, historicoCentavos: [1000, 1000, 1000, 1200] });
    expect(variacaoPct(c)).toBeCloseTo(20, 5); // (1200-1000)/1000*100
  });

  it('média zero não gera divisão por zero — retorna 0', () => {
    const c = categoria({ totalCentavos: 500, historicoCentavos: [500] });
    expect(variacaoPct(c)).toBe(0);
    expect(Number.isFinite(variacaoPct(c))).toBe(true);
  });
});

describe('isAnomalia', () => {
  it('exige total >= R$1.000 E variação > 15% simultaneamente', () => {
    const grandeEAlta = categoria({ totalCentavos: 150_000, historicoCentavos: [100_000, 100_000, 100_000, 150_000] });
    expect(isAnomalia(grandeEAlta)).toBe(true);

    const pequenaEAlta = categoria({ totalCentavos: 50_000, historicoCentavos: [10_000, 10_000, 10_000, 50_000] });
    expect(isAnomalia(pequenaEAlta)).toBe(false); // abaixo do piso de R$1.000

    const grandeEBaixa = categoria({ totalCentavos: 105_000, historicoCentavos: [100_000, 100_000, 100_000, 105_000] });
    expect(isAnomalia(grandeEBaixa)).toBe(false); // variação de só 5%
  });

  it('variação exatamente 15% NÃO é anomalia (estritamente >, não >=)', () => {
    const noLimiar = categoria({ totalCentavos: 115_000, historicoCentavos: [100_000, 100_000, 100_000, 115_000] });
    expect(variacaoPct(noLimiar)).toBeCloseTo(15, 5);
    expect(isAnomalia(noLimiar)).toBe(false);
  });
});

describe('totalDespesasCentavos', () => {
  it('soma o total de todas as categorias', () => {
    const cats = [categoria({ totalCentavos: 1000 }), categoria({ totalCentavos: 2000 })];
    expect(totalDespesasCentavos(cats)).toBe(3000);
  });

  it('lista vazia soma 0', () => {
    expect(totalDespesasCentavos([])).toBe(0);
  });
});

describe('buildBarras', () => {
  it('widthPct relativo à maior categoria, pctDoTotal relativo à soma — ambos em 100 na maior quando única', () => {
    const cats = [categoria({ id: 'aluguel', totalCentavos: 1000, historicoCentavos: [1000] })];
    const [barra] = buildBarras(cats);
    expect(barra.widthPct).toBe(100);
    expect(barra.pctDoTotal).toBe(100);
  });

  it('divide proporcionalmente entre duas categorias', () => {
    const cats = [
      categoria({ id: 'aluguel', totalCentavos: 1000, historicoCentavos: [1000] }),
      categoria({ id: 'software', totalCentavos: 500, historicoCentavos: [500] }),
    ];
    const barras = buildBarras(cats);
    expect(barras[0].widthPct).toBe(100); // maior
    expect(barras[1].widthPct).toBe(50); // metade da maior
    expect(barras[0].pctDoTotal).toBe(67); // 1000/1500 arredondado
    expect(barras[1].pctDoTotal).toBe(33);
  });

  it('lista vazia não quebra (Math.max de array vazio é -Infinity, mas fica sem itens pra mapear)', () => {
    expect(buildBarras([])).toEqual([]);
  });
});

describe('fixoVariavelPct', () => {
  it('separa fixo vs variável e os dois somam 100', () => {
    const cats = [
      categoria({ fixo: true, totalCentavos: 700 }),
      categoria({ fixo: false, totalCentavos: 300 }),
    ];
    const r = fixoVariavelPct(cats);
    expect(r.fixoPct).toBe(70);
    expect(r.varPct).toBe(30);
    expect(r.fixoPct + r.varPct).toBe(100);
  });

  it('total zero: fixoPct 0, varPct 100 (complemento, não divisão por zero)', () => {
    const r = fixoVariavelPct([]);
    expect(r.fixoPct).toBe(0);
    expect(r.varPct).toBe(100);
  });
});

describe('quemMaisSubiu', () => {
  it('ignora categorias abaixo do piso de R$1.000 mesmo que a variação seja maior', () => {
    const cats = [
      categoria({ id: 'software', totalCentavos: 500, historicoCentavos: [100, 500] }), // +400%, mas pequena
      categoria({ id: 'aluguel', totalCentavos: 110_000, historicoCentavos: [100_000, 110_000] }), // +10%, mas grande o bastante
    ];
    expect(quemMaisSubiu(cats)?.categoria.id).toBe('aluguel');
  });

  it('nenhuma categoria elegível retorna null', () => {
    expect(quemMaisSubiu([categoria({ totalCentavos: 500, historicoCentavos: [500] })])).toBeNull();
  });

  it('lista vazia retorna null', () => {
    expect(quemMaisSubiu([])).toBeNull();
  });
});

describe('categoriaDrillStats', () => {
  it('agrega média/pct do total/variação/flag de anomalia da categoria selecionada', () => {
    const c = categoria({ totalCentavos: 1200, historicoCentavos: [1000, 1000, 1200] });
    const stats = categoriaDrillStats(c, 2400);
    expect(stats.avg5Centavos).toBe(1000);
    expect(stats.pctDoTotal).toBe(50);
    expect(stats.variacaoPct).toBe(20);
  });

  it('totalDespesas 0 não gera divisão por zero', () => {
    const c = categoria();
    expect(categoriaDrillStats(c, 0).pctDoTotal).toBe(0);
  });
});

describe('atrasados30MaisDias', () => {
  it('filtra só status atrasado com diasAtraso > 30, soma valor e conta', () => {
    const rows = [
      lancamento({ id: 'a', status: 'atrasado', diasAtraso: 31, valorCentavos: 1000 }),
      lancamento({ id: 'b', status: 'atrasado', diasAtraso: 30, valorCentavos: 2000 }), // exatamente 30, não conta
      lancamento({ id: 'c', status: 'atrasado', diasAtraso: 45, valorCentavos: 500 }),
      lancamento({ id: 'd', status: 'pago', diasAtraso: 60, valorCentavos: 999 }),
    ];
    const r = atrasados30MaisDias(rows);
    expect(r.qtdClientes).toBe(2);
    expect(r.totalCentavos).toBe(1500);
  });

  it('sem diasAtraso definido (undefined) trata como 0 — não quebra', () => {
    const rows = [lancamento({ status: 'atrasado', diasAtraso: undefined })];
    expect(atrasados30MaisDias(rows).qtdClientes).toBe(0);
  });

  it('lista vazia produz zeros', () => {
    const r = atrasados30MaisDias([]);
    expect(r.totalCentavos).toBe(0);
    expect(r.qtdClientes).toBe(0);
  });
});

describe('buildTimeline', () => {
  it('insere divisor "Hoje" antes do 1º lançamento já vencido/realizado', () => {
    const rows = [
      lancamento({ id: 'futuro', data: '2026-07-20' }),
      lancamento({ id: 'hoje-ou-antes', data: TODAY_ISO }),
      lancamento({ id: 'passado', data: '2026-07-01' }),
    ];
    const entries = buildTimeline(rows, 'tudo', null);
    const kinds = entries.map((e) => e.kind);
    expect(kinds).toEqual(['row', 'divider', 'row', 'row']);
  });

  it('sem nenhum lançamento vencido, não insere divisor', () => {
    const rows = [lancamento({ data: '2026-08-01' })];
    const entries = buildTimeline(rows, 'tudo', null);
    expect(entries.some((e) => e.kind === 'divider')).toBe(false);
  });

  it('segFiltro "receber" mantém só entradas; "pagar" só saídas', () => {
    const rows = [
      lancamento({ id: 'in', tipo: 'entrada', data: '2026-08-01' }),
      lancamento({ id: 'out', tipo: 'saida', data: '2026-08-02' }),
    ];
    const receber = buildTimeline(rows, 'receber', null).filter((e) => e.kind === 'row');
    expect(receber).toHaveLength(1);
    const pagar = buildTimeline(rows, 'pagar', null).filter((e) => e.kind === 'row');
    expect(pagar).toHaveLength(1);
  });

  it('insere o resumo do PDV logo após o lançamento r19, só quando não filtrado por "pagar" nem por filtro ativo', () => {
    const rows = [lancamento({ id: 'r19', data: '2026-08-01' }), lancamento({ id: 'depois', data: '2026-08-02' })];
    const semFiltro = buildTimeline(rows, 'tudo', null);
    expect(semFiltro.map((e) => e.kind)).toEqual(['row', 'summary', 'row']);

    const comFiltroPagar = buildTimeline(rows, 'pagar', null);
    expect(comFiltroPagar.some((e) => e.kind === 'summary')).toBe(false);
  });
});

describe('insertLancamentoOrdenado', () => {
  it('insere mantendo ordem decrescente por data (mais recente primeiro)', () => {
    const rows = [lancamento({ id: 'a', data: '2026-08-01' }), lancamento({ id: 'c', data: '2026-07-01' })];
    const novo = lancamento({ id: 'b', data: '2026-07-15' });
    const result = insertLancamentoOrdenado(rows, novo);
    expect(result.map((r) => r.id)).toEqual(['a', 'b', 'c']);
  });

  it('data mais recente que tudo vai pro início', () => {
    const rows = [lancamento({ id: 'a', data: '2026-07-01' })];
    const novo = lancamento({ id: 'novo', data: '2026-08-01' });
    expect(insertLancamentoOrdenado(rows, novo).map((r) => r.id)).toEqual(['novo', 'a']);
  });

  it('data mais antiga que tudo vai pro fim (índice -1 -> append)', () => {
    const rows = [lancamento({ id: 'a', data: '2026-08-01' })];
    const novo = lancamento({ id: 'novo', data: '2026-01-01' });
    expect(insertLancamentoOrdenado(rows, novo).map((r) => r.id)).toEqual(['a', 'novo']);
  });
});

describe('buildColunasGeometry', () => {
  it('gera uma barra por mês do histórico, altura mínima de 2px mesmo pra valor 0', () => {
    const { bars } = buildColunasGeometry([0, 100, 200], 100);
    expect(bars).toHaveLength(3);
    expect(bars[0].height).toBe(2); // clamp mínimo
    expect(bars[2].height).toBeGreaterThan(bars[1].height); // proporcional ao valor
  });

  it('avgY reflete a posição da linha de média no mesmo fator de escala das barras', () => {
    const { avgY } = buildColunasGeometry([100, 100], 100);
    // com histórico uniforme igual à média, avgY deve coincidir com o topo da barra
    const { bars } = buildColunasGeometry([100, 100], 100);
    expect(avgY).toBeCloseTo(bars[0].y, 5);
  });
});
