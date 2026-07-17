import { describe, it, expect } from 'vitest';

import {
  formatPctSigned,
  formatPctPlain,
  derivarContaFixa,
  calcularTotaisFixas,
  calcularRetratoFixo,
  calcularSerieMensalFixas,
  calcularProximaGrande,
  anosDesde,
  calcularMrrTotal,
  calcularPctMrr,
  calcularChurnMesTotal,
  calcularNovosMaisExpansaoMes,
  calcularChurnClientesMesTotal,
  calcularNovosClientesMesTotal,
  calcularChurnPctBase,
  calcularArrEstimado,
  calcularTicketMedio,
  calcularDeltaPct,
} from './calc';
import type { AssinaturaServico, ContaFixa, ContaFixaDerivada } from './types';

// Histórico Ago→Jul (índices 0..11). Índices 5..10 = Jan..Jun (trailing6), índice 11 = Jul (atual).
function contaFixa(overrides: Partial<ContaFixa> = {}): ContaFixa {
  return {
    id: 'c1',
    nome: 'Aluguel',
    categoria: 'aluguel',
    diaVencimento: 5,
    proximaLabel: '05/08',
    ativaDesde: 'jan/2021',
    historico12m: [1000, 1000, 1000, 1000, 1000, 1000, 1000, 1000, 1000, 1000, 1000, 1000],
    ...overrides,
  };
}

describe('formatPctSigned', () => {
  it('positivo ganha "+", negativo usa menos unicode (−), vírgula pt-BR', () => {
    expect(formatPctSigned(21.9)).toBe('+21,9%');
    expect(formatPctSigned(-5.4)).toBe('−5,4%');
  });

  it('zero é tratado como não-negativo (>= 0), ganha "+"', () => {
    expect(formatPctSigned(0)).toBe('+0,0%');
  });

  it('respeita dígitos customizados', () => {
    expect(formatPctSigned(12.345, 2)).toBe('+12,35%');
  });
});

describe('formatPctPlain', () => {
  it('sem sinal, vírgula pt-BR', () => {
    expect(formatPctPlain(12.3)).toBe('12,3%');
    expect(formatPctPlain(-12.3)).toBe('-12,3%'); // toFixed não troca ponto->vírgula do sinal
  });
});

describe('derivarContaFixa', () => {
  it('deriva atual/mesPassado/media6m/variacaoPct/totalAnoCorrente do histórico bruto', () => {
    // Ago Set Out Nov Dez Jan Fev Mar Abr Mai Jun Jul
    const item = contaFixa({
      historico12m: [900, 900, 900, 900, 900, 1000, 1000, 1000, 1000, 1000, 1000, 1200],
    });
    const d = derivarContaFixa(item);
    expect(d.atual).toBe(1200); // Jul
    expect(d.mesPassado).toBe(1000); // Jun
    expect(d.media6m).toBe(1000); // média de Jan..Jun = 1000
    expect(d.variacaoPct).toBeCloseTo(20, 5); // (1200-1000)/1000*100
    expect(d.totalAnoCorrente).toBe(1000 * 6 + 1200); // Jan..Jul
  });

  it('emAlerta ativa exatamente no limiar (>= 15%), não estritamente acima', () => {
    const noLimiar = contaFixa({
      historico12m: [0, 0, 0, 0, 0, 1000, 1000, 1000, 1000, 1000, 1000, 1150], // variação = 15% exato
    });
    expect(derivarContaFixa(noLimiar).variacaoPct).toBeCloseTo(15, 5);
    expect(derivarContaFixa(noLimiar).emAlerta).toBe(true);

    const abaixoDoLimiar = contaFixa({
      historico12m: [0, 0, 0, 0, 0, 1000, 1000, 1000, 1000, 1000, 1000, 1149],
    });
    expect(derivarContaFixa(abaixoDoLimiar).emAlerta).toBe(false);
  });

  it('media6m == 0 não gera divisão por zero: variacaoPct cai pra 0 e sem alerta', () => {
    const semHistorico = contaFixa({
      historico12m: [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 500],
    });
    const d = derivarContaFixa(semHistorico);
    expect(d.variacaoPct).toBe(0);
    expect(d.emAlerta).toBe(false);
    expect(Number.isFinite(d.variacaoPct)).toBe(true);
  });

  it('media6m arredonda pra centavo inteiro (não fração de centavo)', () => {
    const item = contaFixa({
      historico12m: [0, 0, 0, 0, 0, 100, 100, 100, 100, 100, 101, 999], // soma=601, /6=100.1666...
    });
    expect(derivarContaFixa(item).media6m).toBe(100); // Math.round(100.1666...) = 100
  });
});

describe('calcularTotaisFixas', () => {
  const itens: ContaFixaDerivada[] = [
    derivarContaFixa(contaFixa({ id: 'a', historico12m: [0, 0, 0, 0, 0, 1000, 1000, 1000, 1000, 1000, 1000, 1200] })),
    derivarContaFixa(contaFixa({ id: 'b', historico12m: [0, 0, 0, 0, 0, 500, 500, 500, 500, 500, 500, 400] })),
  ];

  it('soma atual/mesPassado, calcula delta absoluto e percentual', () => {
    const t = calcularTotaisFixas(itens, 10000, 22);
    expect(t.totalAtual).toBe(1200 + 400); // 1600
    expect(t.totalMesPassado).toBe(1000 + 500); // 1500
    expect(t.deltaAbs).toBe(100);
    expect(t.deltaPct).toBeCloseTo((100 / 1500) * 100, 5);
  });

  it('custoPorDia divide o total atual pelos dias úteis, arredondado', () => {
    const t = calcularTotaisFixas(itens, 10000, 22);
    expect(t.custoPorDia).toBe(Math.round(1600 / 22));
  });

  it('pesoReceitaPct = 0 quando receita de referência é 0 (sem dividir por zero)', () => {
    const t = calcularTotaisFixas(itens, 0, 22);
    expect(t.pesoReceitaPct).toBe(0);
  });

  it('deltaPct = 0 quando totalMesPassado é 0 (sem dividir por zero)', () => {
    const semMesPassado: ContaFixaDerivada[] = [
      derivarContaFixa(contaFixa({ historico12m: [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 500] })),
    ];
    const t = calcularTotaisFixas(semMesPassado, 10000, 22);
    expect(t.deltaPct).toBe(0);
  });

  it('lista vazia produz totais zerados, não NaN/erro', () => {
    const t = calcularTotaisFixas([], 10000, 22);
    expect(t.totalAtual).toBe(0);
    expect(t.totalMesPassado).toBe(0);
    expect(t.deltaAbs).toBe(0);
    expect(t.custoPorDia).toBe(0);
  });
});

describe('calcularRetratoFixo', () => {
  it('projeta anual como atual * 12 e calcula variação vs 6 meses atrás (valor de Jan)', () => {
    const itens: ContaFixaDerivada[] = [
      derivarContaFixa(contaFixa({ historico12m: [0, 0, 0, 0, 0, 1000, 0, 0, 0, 0, 0, 1500] })),
    ];
    const r = calcularRetratoFixo(itens);
    expect(r.totalAtual).toBe(1500);
    expect(r.projecaoAnual).toBe(1500 * 12);
    expect(r.totalHaSeisMeses).toBe(1000);
    expect(r.variacaoSeisMesesPct).toBeCloseTo(50, 5); // (1500-1000)/1000*100
    expect(r.compromissosAtivos).toBe(1);
  });

  it('variacaoSeisMesesPct = 0 quando totalHaSeisMeses é 0', () => {
    const itens: ContaFixaDerivada[] = [
      derivarContaFixa(contaFixa({ historico12m: [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 500] })),
    ];
    expect(calcularRetratoFixo(itens).variacaoSeisMesesPct).toBe(0);
  });
});

describe('calcularSerieMensalFixas', () => {
  it('soma cada mês entre todas as contas, mantendo 12 posições', () => {
    const itens: ContaFixaDerivada[] = [
      derivarContaFixa(contaFixa({ historico12m: [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12] })),
      derivarContaFixa(contaFixa({ historico12m: [10, 20, 30, 40, 50, 60, 70, 80, 90, 100, 110, 120] })),
    ];
    const serie = calcularSerieMensalFixas(itens);
    expect(serie).toHaveLength(12);
    expect(serie[0]).toBe(11); // 1+10
    expect(serie[11]).toBe(132); // 12+120
  });

  it('lista vazia produz série de zeros', () => {
    const serie = calcularSerieMensalFixas([]);
    expect(serie).toEqual(new Array(12).fill(0));
  });
});

describe('calcularProximaGrande', () => {
  it('filtra pelo sufixo do mês de referência e pega o maior valor atual', () => {
    const itens: ContaFixaDerivada[] = [
      derivarContaFixa(contaFixa({ id: 'a', proximaLabel: '10/07', historico12m: [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 500] })),
      derivarContaFixa(contaFixa({ id: 'b', proximaLabel: '20/07', historico12m: [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 900] })),
      derivarContaFixa(contaFixa({ id: 'c', proximaLabel: '05/08', historico12m: [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 5000] })),
    ];
    const maior = calcularProximaGrande(itens);
    expect(maior?.id).toBe('b');
  });

  it('retorna null quando nenhuma conta vence no mês de referência', () => {
    const itens: ContaFixaDerivada[] = [
      derivarContaFixa(contaFixa({ proximaLabel: '05/08' })),
    ];
    expect(calcularProximaGrande(itens)).toBeNull();
  });

  it('lista vazia retorna null', () => {
    expect(calcularProximaGrande([])).toBeNull();
  });
});

describe('anosDesde', () => {
  it('calcula anos e meses de casa contra a referência fixa (jul/2026)', () => {
    expect(anosDesde('jan/2021')).toBe('5 anos e 6 meses de casa');
    expect(anosDesde('jul/2021')).toBe('5 anos de casa');
    expect(anosDesde('fev/2026')).toBe('5 meses de casa');
  });

  it('singular correto para 1 ano / 1 mês', () => {
    expect(anosDesde('jul/2025')).toBe('1 ano de casa');
    expect(anosDesde('jun/2026')).toBe('1 mês de casa');
  });

  it('mês exatamente igual ao de referência (0 meses de diferença)', () => {
    expect(anosDesde('jul/2026')).toBe('0 meses de casa');
  });
});

function assinatura(overrides: Partial<AssinaturaServico> = {}): AssinaturaServico {
  return {
    id: 's1',
    nome: 'Plano A',
    mrr: 1000,
    corClasse: 'bg-primary-600',
    clientes: 5,
    churnClientesMes: 0,
    tempoMedioMeses: '12',
    ltv: 12000,
    retencaoPct: '90%',
    novos6m: [0, 0, 0, 0, 0, 0],
    churn6m: [0, 0, 0, 0, 0, 0],
    ...overrides,
  };
}

describe('calcularMrrTotal / calcularPctMrr', () => {
  it('soma o mrr de todos os serviços', () => {
    const servicos = [assinatura({ mrr: 1000 }), assinatura({ mrr: 2000 })];
    expect(calcularMrrTotal(servicos)).toBe(3000);
  });

  it('calcularPctMrr = 0 quando mrrTotal é 0', () => {
    expect(calcularPctMrr(assinatura({ mrr: 500 }), 0)).toBe(0);
  });

  it('calcularPctMrr calcula participação correta', () => {
    expect(calcularPctMrr(assinatura({ mrr: 250 }), 1000)).toBe(25);
  });
});

describe('calcularChurnMesTotal / calcularNovosMaisExpansaoMes', () => {
  it('soma o último mês (Jul) da série de cada serviço', () => {
    const servicos = [
      assinatura({ churn6m: [10, 20, 30, 40, 50, 60], novos6m: [1, 2, 3, 4, 5, 6] }),
      assinatura({ churn6m: [0, 0, 0, 0, 0, 100], novos6m: [0, 0, 0, 0, 0, 200] }),
    ];
    expect(calcularChurnMesTotal(servicos)).toBe(60 + 100);
    expect(calcularNovosMaisExpansaoMes(servicos)).toBe(6 + 200);
  });

  it('lista vazia soma para 0', () => {
    expect(calcularChurnMesTotal([])).toBe(0);
    expect(calcularNovosMaisExpansaoMes([])).toBe(0);
  });
});

describe('calcularChurnClientesMesTotal / calcularNovosClientesMesTotal', () => {
  it('soma churnClientesMes de todos os serviços', () => {
    const servicos = [assinatura({ churnClientesMes: 2 }), assinatura({ churnClientesMes: 3 })];
    expect(calcularChurnClientesMesTotal(servicos)).toBe(5);
  });

  it('conta serviços com novos6m do último mês > 0', () => {
    const servicos = [
      assinatura({ novos6m: [0, 0, 0, 0, 0, 1] }),
      assinatura({ novos6m: [0, 0, 0, 0, 0, 0] }),
      assinatura({ novos6m: [5, 0, 0, 0, 0, 3] }),
    ];
    expect(calcularNovosClientesMesTotal(servicos)).toBe(2);
  });
});

describe('calcularChurnPctBase', () => {
  it('calcula % do churn sobre a base ANTES do churn (mrrAtual + churnMes)', () => {
    // Exemplo do comentário: 16,9% = 1239 / (6077+1239)
    const pct = calcularChurnPctBase(1239, 6077);
    expect(pct).toBeCloseTo((1239 / (6077 + 1239)) * 100, 5);
    expect(pct).toBeCloseTo(16.9, 1);
  });

  it('retorna 0 quando base é 0 (sem churn nem mrr)', () => {
    expect(calcularChurnPctBase(0, 0)).toBe(0);
  });
});

describe('calcularArrEstimado', () => {
  it('multiplica o mrr atual por 12', () => {
    expect(calcularArrEstimado(1000)).toBe(12000);
    expect(calcularArrEstimado(0)).toBe(0);
  });
});

describe('calcularTicketMedio', () => {
  it('divide mrr pelo nº de assinaturas ativas, arredondado', () => {
    expect(calcularTicketMedio(1000, 3)).toBe(Math.round(1000 / 3));
  });

  it('retorna 0 quando não há assinaturas ativas (evita divisão por zero)', () => {
    expect(calcularTicketMedio(1000, 0)).toBe(0);
  });
});

describe('calcularDeltaPct', () => {
  it('calcula variação % genérica entre atual e referência', () => {
    expect(calcularDeltaPct(120, 100)).toBe(20);
    expect(calcularDeltaPct(80, 100)).toBe(-20);
  });

  it('retorna 0 quando referência é 0 (sem divisão por zero)', () => {
    expect(calcularDeltaPct(100, 0)).toBe(0);
  });
});
