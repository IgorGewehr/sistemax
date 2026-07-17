import { describe, it, expect } from 'vitest';

import {
  totalSangriasCentavos,
  totalSuprimentosCentavos,
  esperadoCentavos,
  diferencaCentavos,
  duracaoTurno,
  sessoesFechadas,
  calcularEstatisticasMes,
  calcularDiaCritico,
  calcularMediaDiferencaDia,
  calcularSangriasMes,
  descreverDiferenca,
  valorNaGaveta,
  descreverCaixaHojeFoot,
  diaLabel,
} from './calc';
import type { SangriaEvento, SessaoCaixa, SessaoCaixaFechada, SuprimentoEvento } from './types';

/** `Intl.NumberFormat('pt-BR', { style: 'currency' })` separa "R$" do valor com NBSP (U+00A0),
 * não espaço ASCII — mesma armadilha documentada em `lib/money.test.ts`. */
const NBSP = ' ';
function brl(valor: string): string {
  return `R$${NBSP}${valor}`;
}

function sangria(overrides: Partial<SangriaEvento> = {}): SangriaEvento {
  return { hora: '14:00', valorCentavos: 5000, destino: 'Cofre', ...overrides };
}

function suprimento(overrides: Partial<SuprimentoEvento> = {}): SuprimentoEvento {
  return { hora: '09:00', valorCentavos: 5000, origem: 'Cofre', ...overrides };
}

function sessaoAberta(overrides: Partial<SessaoCaixa> = {}): SessaoCaixa {
  return {
    id: 'sessao-aberta-teste',
    dia: 16,
    diaSemana: 'Qui',
    operador: 'Maria',
    horaAbertura: '08:00',
    aberturaCentavos: 20_000,
    vendasEspecieCentavos: 10_000,
    sangrias: [],
    suprimentos: [],
    trocoCentavos: 0,
    status: 'aberto',
    horaFechamento: null,
    contadoCentavos: null,
    ...overrides,
  } as SessaoCaixa;
}

function sessaoFechada(overrides: Partial<SessaoCaixaFechada> = {}): SessaoCaixaFechada {
  return {
    id: 'sessao-fechada-teste',
    dia: 15,
    diaSemana: 'Qua',
    operador: 'João',
    horaAbertura: '08:00',
    aberturaCentavos: 20_000,
    vendasEspecieCentavos: 10_000,
    sangrias: [],
    suprimentos: [],
    trocoCentavos: 0,
    status: 'fechado',
    horaFechamento: '18:00',
    contadoCentavos: 30_000,
    ...overrides,
  };
}

describe('totalSangriasCentavos', () => {
  it('soma o valor de todas as sangrias da sessão', () => {
    const s = sessaoAberta({ sangrias: [sangria({ valorCentavos: 1000 }), sangria({ valorCentavos: 2000 })] });
    expect(totalSangriasCentavos(s)).toBe(3000);
  });

  it('sem sangrias soma 0', () => {
    expect(totalSangriasCentavos(sessaoAberta())).toBe(0);
  });
});

describe('totalSuprimentosCentavos', () => {
  it('soma o valor de todos os suprimentos da sessão', () => {
    const s = sessaoAberta({ suprimentos: [suprimento({ valorCentavos: 1000 }), suprimento({ valorCentavos: 2000 })] });
    expect(totalSuprimentosCentavos(s)).toBe(3000);
  });

  it('sem suprimentos soma 0', () => {
    expect(totalSuprimentosCentavos(sessaoAberta())).toBe(0);
  });
});

describe('esperadoCentavos', () => {
  it('abertura + vendas em espécie + suprimentos − sangrias − troco', () => {
    const s = sessaoAberta({
      aberturaCentavos: 20_000,
      vendasEspecieCentavos: 15_000,
      suprimentos: [suprimento({ valorCentavos: 3000 })],
      sangrias: [sangria({ valorCentavos: 5000 })],
      trocoCentavos: 1000,
    });
    expect(esperadoCentavos(s)).toBe(20_000 + 15_000 + 3000 - 5000 - 1000);
  });

  it('nunca é armazenado — sempre recalculado a partir dos primitivos (mudar sangrias muda o esperado)', () => {
    const base = sessaoAberta({ aberturaCentavos: 10_000, vendasEspecieCentavos: 0, trocoCentavos: 0 });
    const semSangria = esperadoCentavos(base);
    const comSangria = esperadoCentavos({ ...base, sangrias: [sangria({ valorCentavos: 2000 })] });
    expect(semSangria - comSangria).toBe(2000);
  });

  it('um suprimento aumenta o esperado (contrapartida exata da sangria)', () => {
    const base = sessaoAberta({ aberturaCentavos: 10_000, vendasEspecieCentavos: 0, trocoCentavos: 0 });
    const semSuprimento = esperadoCentavos(base);
    const comSuprimento = esperadoCentavos({ ...base, suprimentos: [suprimento({ valorCentavos: 2000 })] });
    expect(comSuprimento - semSuprimento).toBe(2000);
  });
});

describe('diferencaCentavos', () => {
  it('sessão aberta retorna null (ainda sem contagem)', () => {
    expect(diferencaCentavos(sessaoAberta())).toBeNull();
  });

  it('sessão fechada: contado − esperado, positivo = sobra', () => {
    const s = sessaoFechada({ aberturaCentavos: 20_000, vendasEspecieCentavos: 10_000, contadoCentavos: 31_000 });
    // esperado = 20000+10000-0-0 = 30000; diff = 31000-30000 = 1000
    expect(diferencaCentavos(s)).toBe(1000);
  });

  it('sessão fechada: contado menor que esperado é negativo = falta', () => {
    const s = sessaoFechada({ aberturaCentavos: 20_000, vendasEspecieCentavos: 10_000, contadoCentavos: 29_000 });
    expect(diferencaCentavos(s)).toBe(-1000);
  });

  it('contado exatamente igual ao esperado: diferença zero (bateu certinho)', () => {
    const s = sessaoFechada({ aberturaCentavos: 20_000, vendasEspecieCentavos: 10_000, contadoCentavos: 30_000 });
    expect(diferencaCentavos(s)).toBe(0);
  });
});

describe('duracaoTurno', () => {
  it('calcula horas e minutos entre abertura e fechamento no mesmo dia', () => {
    expect(duracaoTurno('08:00', '18:17')).toBe('10h 17min');
  });

  it('vira o dia quando o fechamento é depois da meia-noite', () => {
    expect(duracaoTurno('22:00', '02:00')).toBe('4h 0min');
  });

  it('turno de duração zero', () => {
    expect(duracaoTurno('08:00', '08:00')).toBe('0h 0min');
  });
});

describe('sessoesFechadas', () => {
  it('filtra só as sessões com status fechado', () => {
    const sessoes: SessaoCaixa[] = [sessaoAberta(), sessaoFechada({ dia: 1 }), sessaoFechada({ dia: 2 })];
    expect(sessoesFechadas(sessoes)).toHaveLength(2);
  });
});

describe('calcularEstatisticasMes', () => {
  it('soma diferenças, conta faltas (negativo) e sobras (positivo) separadamente', () => {
    const sessoes: SessaoCaixa[] = [
      sessaoFechada({ dia: 1, aberturaCentavos: 0, vendasEspecieCentavos: 0, contadoCentavos: 1000 }), // sobra 1000
      sessaoFechada({ dia: 2, aberturaCentavos: 0, vendasEspecieCentavos: 0, contadoCentavos: -500 }), // falta 500
      sessaoFechada({ dia: 3, aberturaCentavos: 0, vendasEspecieCentavos: 0, contadoCentavos: 0 }), // bateu (nem falta nem sobra)
      sessaoAberta({ dia: 4 }), // ignorada
    ];
    const stats = calcularEstatisticasMes(sessoes);
    expect(stats.quantidadeSobras).toBe(1);
    expect(stats.quantidadeFaltas).toBe(1);
    expect(stats.diasFechados).toBe(3);
    expect(stats.totalDiferencaCentavos).toBe(1000 - 500 + 0);
  });

  it('lista vazia produz estatísticas zeradas', () => {
    const stats = calcularEstatisticasMes([]);
    expect(stats.totalDiferencaCentavos).toBe(0);
    expect(stats.quantidadeFaltas).toBe(0);
    expect(stats.quantidadeSobras).toBe(0);
    expect(stats.diasFechados).toBe(0);
  });
});

describe('calcularDiaCritico', () => {
  it('acha o dia da semana com a PIOR média (mais falta, menor diferença)', () => {
    const sessoes: SessaoCaixa[] = [
      sessaoFechada({ dia: 2, diaSemana: 'Qui', aberturaCentavos: 0, vendasEspecieCentavos: 0, contadoCentavos: -2000 }),
      sessaoFechada({ dia: 9, diaSemana: 'Qui', aberturaCentavos: 0, vendasEspecieCentavos: 0, contadoCentavos: -1000 }),
      sessaoFechada({ dia: 3, diaSemana: 'Sex', aberturaCentavos: 0, vendasEspecieCentavos: 0, contadoCentavos: 500 }),
    ];
    const pior = calcularDiaCritico(sessoes);
    expect(pior?.diaSemana).toBe('Qui');
    expect(pior?.mediaCentavos).toBe(-1500); // média de -2000 e -1000
  });

  it('sem sessões fechadas retorna null', () => {
    expect(calcularDiaCritico([sessaoAberta()])).toBeNull();
  });
});

describe('calcularMediaDiferencaDia', () => {
  it('calcula a média simples das diferenças das sessões fechadas', () => {
    const sessoes: SessaoCaixa[] = [
      sessaoFechada({ dia: 1, aberturaCentavos: 0, vendasEspecieCentavos: 0, contadoCentavos: 1000 }),
      sessaoFechada({ dia: 2, aberturaCentavos: 0, vendasEspecieCentavos: 0, contadoCentavos: -400 }),
    ];
    expect(calcularMediaDiferencaDia(sessoes)).toBe(300);
  });

  it('lista vazia retorna 0, não NaN', () => {
    expect(calcularMediaDiferencaDia([])).toBe(0);
  });
});

describe('calcularSangriasMes', () => {
  it('soma sangrias de TODAS as sessões (abertas e fechadas), contando a quantidade de eventos', () => {
    const sessoes: SessaoCaixa[] = [
      sessaoAberta({ sangrias: [sangria({ valorCentavos: 1000 })] }),
      sessaoFechada({ sangrias: [sangria({ valorCentavos: 2000 }), sangria({ valorCentavos: 500 })] }),
    ];
    const r = calcularSangriasMes(sessoes);
    expect(r.totalCentavos).toBe(3500);
    expect(r.quantidade).toBe(3);
  });

  it('sem sangrias retorna zeros', () => {
    const r = calcularSangriasMes([sessaoAberta()]);
    expect(r.totalCentavos).toBe(0);
    expect(r.quantidade).toBe(0);
  });
});

describe('descreverDiferenca', () => {
  it('sessão aberta: "em aberto"', () => {
    expect(descreverDiferenca(sessaoAberta())).toBe('em aberto');
  });

  it('diferença zero: "bateu certinho"', () => {
    const s = sessaoFechada({ aberturaCentavos: 0, vendasEspecieCentavos: 0, contadoCentavos: 0 });
    expect(descreverDiferenca(s)).toBe('bateu certinho');
  });

  it('diferença positiva: "sobra R$ X" com valor formatado', () => {
    const s = sessaoFechada({ aberturaCentavos: 0, vendasEspecieCentavos: 0, contadoCentavos: 1000 });
    expect(descreverDiferenca(s)).toBe(`sobra ${brl('10')}`);
  });

  it('diferença negativa: "falta R$ X" com valor ABSOLUTO formatado (sem sinal negativo duplicado)', () => {
    const s = sessaoFechada({ aberturaCentavos: 0, vendasEspecieCentavos: 0, contadoCentavos: -1000 });
    expect(descreverDiferenca(s)).toBe(`falta ${brl('10')}`);
  });
});

describe('valorNaGaveta', () => {
  it('sessão aberta usa o valor esperado com sufixo "(esperado)"', () => {
    const s = sessaoAberta({ aberturaCentavos: 20_000, vendasEspecieCentavos: 0, trocoCentavos: 0 });
    const r = valorNaGaveta(s);
    expect(r.centavos).toBe(20_000);
    expect(r.sufixo).toBe('(esperado)');
  });

  it('sessão fechada usa o valor contado com sufixo "(fechado)"', () => {
    const s = sessaoFechada({ contadoCentavos: 12_345 });
    const r = valorNaGaveta(s);
    expect(r.centavos).toBe(12_345);
    expect(r.sufixo).toBe('(fechado)');
  });
});

describe('descreverCaixaHojeFoot', () => {
  it('sessão aberta cita horário, operador e valor de abertura', () => {
    const s = sessaoAberta({ horaAbertura: '08:00', operador: 'Maria', aberturaCentavos: 20_000 });
    expect(descreverCaixaHojeFoot(s)).toBe(`08:00 · Maria · abertura ${brl('200')}`);
  });

  it('sessão fechada cita horário de fechamento e operador', () => {
    const s = sessaoFechada({ horaFechamento: '18:00', operador: 'João' });
    expect(descreverCaixaHojeFoot(s)).toBe('fechado 18:00 · João');
  });
});

describe('diaLabel', () => {
  it('formata dia com zero à esquerda e mês fixo /07', () => {
    expect(diaLabel(5)).toBe('05/07');
    expect(diaLabel(16)).toBe('16/07');
  });
});
