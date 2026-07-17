import { describe, it, expect } from 'vitest';

import {
  toISODate,
  parseISODate,
  addDias,
  addSemanas,
  addMeses,
  startOfWeek,
  endOfWeek,
  startOfMonth,
  endOfMonth,
  isSameDiaISO,
  isMesmoMes,
  buildWeekDays,
  buildMonthGrid,
  timeToMinutes,
  minutesToTime,
  addDuracao,
  getBlockTop,
  getBlockHeight,
  checkConflito,
  groupByData,
  filterByProfissional,
  computeStatusSummary,
  podeTransitar,
  generateRecorrenciaDatas,
  buildConsultorInsight,
  parseCentavosDigitados,
} from './calc';
import type { Agendamento } from './types';

function agendamento(overrides: Partial<Agendamento> = {}): Agendamento {
  return {
    id: 'a1',
    clienteId: 'c1',
    clienteNome: 'Ana',
    clienteTelefone: null,
    servicoId: 's1',
    servicoNome: 'Corte',
    data: '2026-07-16',
    horaInicio: '10:00',
    horaFim: '11:00',
    duracaoMin: 60,
    profissionalIds: ['p1'],
    profissionalNomes: ['Carlos'],
    status: 'agendado',
    precoCentavos: 5000,
    observacoes: null,
    ...overrides,
  };
}

describe('toISODate / parseISODate', () => {
  it('round-trip preserva a data (meia-noite local)', () => {
    const d = new Date(2026, 6, 16); // 16/jul/2026 local
    expect(toISODate(d)).toBe('2026-07-16');
    expect(toISODate(parseISODate('2026-07-16'))).toBe('2026-07-16');
  });

  it('parseISODate não desalinha 1 dia em fusos negativos (fixa T00:00:00 local)', () => {
    const d = parseISODate('2026-01-01');
    expect(d.getDate()).toBe(1);
    expect(d.getMonth()).toBe(0);
  });
});

describe('addDias / addSemanas / addMeses', () => {
  it('addDias soma dias corridos', () => {
    expect(toISODate(addDias(parseISODate('2026-07-16'), 5))).toBe('2026-07-21');
  });

  it('addSemanas soma múltiplos de 7 dias', () => {
    expect(toISODate(addSemanas(parseISODate('2026-07-16'), 2))).toBe('2026-07-30');
  });

  it('addMeses vira o ano corretamente', () => {
    expect(toISODate(addMeses(parseISODate('2026-12-16'), 1))).toBe('2027-01-16');
  });
});

describe('startOfWeek / endOfWeek', () => {
  it('domingo é o início da semana (weekStartsOn: 0)', () => {
    const quinta = parseISODate('2026-07-16'); // quinta-feira
    expect(startOfWeek(quinta).getDay()).toBe(0);
    expect(endOfWeek(quinta).getDay()).toBe(6);
  });
});

describe('startOfMonth / endOfMonth', () => {
  it('primeiro e último dia do mês', () => {
    const d = parseISODate('2026-07-16');
    expect(toISODate(startOfMonth(d))).toBe('2026-07-01');
    expect(toISODate(endOfMonth(d))).toBe('2026-07-31');
  });

  it('funciona em fevereiro (ano não bissexto)', () => {
    expect(toISODate(endOfMonth(parseISODate('2026-02-10')))).toBe('2026-02-28');
  });
});

describe('isSameDiaISO / isMesmoMes', () => {
  it('compara Date vs string ISO', () => {
    expect(isSameDiaISO(parseISODate('2026-07-16'), '2026-07-16')).toBe(true);
    expect(isSameDiaISO(parseISODate('2026-07-16'), '2026-07-17')).toBe(false);
  });

  it('mesmo mês ignora o dia', () => {
    expect(isMesmoMes(parseISODate('2026-07-01'), parseISODate('2026-07-31'))).toBe(true);
    expect(isMesmoMes(parseISODate('2026-07-31'), parseISODate('2026-08-01'))).toBe(false);
  });
});

describe('buildWeekDays / buildMonthGrid', () => {
  it('7 dias começando no domingo', () => {
    const dias = buildWeekDays(parseISODate('2026-07-16'));
    expect(dias).toHaveLength(7);
    expect(dias[0].getDay()).toBe(0);
  });

  it('grade mensal sempre tem 42 células (6 semanas)', () => {
    expect(buildMonthGrid(parseISODate('2026-07-16'))).toHaveLength(42);
  });
});

describe('timeToMinutes / minutesToTime', () => {
  it('round-trip preserva o horário', () => {
    expect(timeToMinutes('09:30')).toBe(570);
    expect(minutesToTime(570)).toBe('09:30');
  });

  it('meia-noite é 0 minutos', () => {
    expect(timeToMinutes('00:00')).toBe(0);
  });
});

describe('addDuracao', () => {
  it('soma minutos ao horário de início', () => {
    expect(addDuracao('10:00', 45)).toBe('10:45');
  });

  it('vira a hora corretamente', () => {
    expect(addDuracao('10:30', 45)).toBe('11:15');
  });
});

describe('getBlockTop / getBlockHeight', () => {
  it('topo é proporcional ao offset desde a hora inicial da grade', () => {
    expect(getBlockTop('07:00', 6, 64)).toBe(64); // 1h após as 6h, hourHeight=64
  });

  it('altura é proporcional à duração, com piso de 24px', () => {
    expect(getBlockHeight(60, 64)).toBe(64);
    expect(getBlockHeight(5, 64)).toBe(24); // clamp mínimo pra blocos curtos
  });
});

describe('checkConflito', () => {
  it('sem profissional selecionado, nunca há conflito', () => {
    expect(checkConflito([], '', '2026-07-16', '10:00', '11:00').temConflito).toBe(false);
  });

  it('detecta overlap do mesmo profissional no mesmo dia', () => {
    const existentes = [agendamento({ id: 'existente', horaInicio: '10:00', horaFim: '11:00' })];
    const r = checkConflito(existentes, 'p1', '2026-07-16', '10:30', '11:30');
    expect(r.temConflito).toBe(true);
    expect(r.mensagem).toContain('Ana');
  });

  it('não detecta conflito quando os horários só se tocam na borda (fim == início)', () => {
    const existentes = [agendamento({ horaInicio: '10:00', horaFim: '11:00' })];
    expect(checkConflito(existentes, 'p1', '2026-07-16', '11:00', '12:00').temConflito).toBe(false);
    expect(checkConflito(existentes, 'p1', '2026-07-16', '09:00', '10:00').temConflito).toBe(false);
  });

  it('ignora o próprio agendamento em edição via excludeId', () => {
    const existentes = [agendamento({ id: 'self', horaInicio: '10:00', horaFim: '11:00' })];
    expect(checkConflito(existentes, 'p1', '2026-07-16', '10:00', '11:00', 'self').temConflito).toBe(false);
  });

  it('ignora agendamentos cancelados', () => {
    const existentes = [agendamento({ status: 'cancelado', horaInicio: '10:00', horaFim: '11:00' })];
    expect(checkConflito(existentes, 'p1', '2026-07-16', '10:00', '11:00').temConflito).toBe(false);
  });

  it('não conflita se for outro profissional', () => {
    const existentes = [agendamento({ profissionalIds: ['p2'], horaInicio: '10:00', horaFim: '11:00' })];
    expect(checkConflito(existentes, 'p1', '2026-07-16', '10:00', '11:00').temConflito).toBe(false);
  });

  it('não conflita se for outro dia', () => {
    const existentes = [agendamento({ data: '2026-07-17', horaInicio: '10:00', horaFim: '11:00' })];
    expect(checkConflito(existentes, 'p1', '2026-07-16', '10:00', '11:00').temConflito).toBe(false);
  });
});

describe('groupByData', () => {
  it('agrupa agendamentos por data', () => {
    const map = groupByData([agendamento({ id: 'a', data: '2026-07-16' }), agendamento({ id: 'b', data: '2026-07-16' }), agendamento({ id: 'c', data: '2026-07-17' })]);
    expect(map.get('2026-07-16')).toHaveLength(2);
    expect(map.get('2026-07-17')).toHaveLength(1);
  });
});

describe('filterByProfissional', () => {
  it('"todos" retorna tudo sem filtrar', () => {
    const lista = [agendamento({ profissionalIds: ['p1'] })];
    expect(filterByProfissional(lista, 'todos')).toHaveLength(1);
  });

  it('filtra por profissional específico', () => {
    const lista = [agendamento({ profissionalIds: ['p1'] }), agendamento({ profissionalIds: ['p2'] })];
    expect(filterByProfissional(lista, 'p1')).toHaveLength(1);
  });
});

describe('computeStatusSummary', () => {
  it('conta agendamentos por status, incluindo os que não aparecem (0)', () => {
    const lista = [agendamento({ status: 'agendado' }), agendamento({ status: 'agendado' }), agendamento({ status: 'concluido' })];
    const summary = computeStatusSummary(lista);
    expect(summary.agendado).toBe(2);
    expect(summary.concluido).toBe(1);
    expect(summary.cancelado).toBe(0);
  });
});

describe('podeTransitar (FSM)', () => {
  it('agendado pode ir pra confirmado/em_andamento/cancelado/nao_compareceu', () => {
    expect(podeTransitar('agendado', 'confirmado')).toBe(true);
    expect(podeTransitar('agendado', 'concluido')).toBe(false); // precisa passar por confirmado/em_andamento
  });

  it('estados terminais não têm transições', () => {
    expect(podeTransitar('concluido', 'agendado')).toBe(false);
    expect(podeTransitar('cancelado', 'confirmado')).toBe(false);
  });
});

describe('generateRecorrenciaDatas', () => {
  it('freq "nenhuma" ou 1 ocorrência retorna só a data inicial', () => {
    expect(generateRecorrenciaDatas('2026-07-16', 'nenhuma', 5)).toEqual(['2026-07-16']);
    expect(generateRecorrenciaDatas('2026-07-16', 'semanal', 1)).toEqual(['2026-07-16']);
  });

  it('semanal gera datas +7 dias cada', () => {
    expect(generateRecorrenciaDatas('2026-07-16', 'semanal', 3)).toEqual(['2026-07-16', '2026-07-23', '2026-07-30']);
  });

  it('quinzenal gera datas +14 dias cada', () => {
    expect(generateRecorrenciaDatas('2026-07-16', 'quinzenal', 3)).toEqual(['2026-07-16', '2026-07-30', '2026-08-13']);
  });

  it('diária gera datas consecutivas', () => {
    expect(generateRecorrenciaDatas('2026-07-16', 'diaria', 3)).toEqual(['2026-07-16', '2026-07-17', '2026-07-18']);
  });

  it('mensal mantém o mesmo dia do mês', () => {
    expect(generateRecorrenciaDatas('2026-07-16', 'mensal', 3)).toEqual(['2026-07-16', '2026-08-16', '2026-09-16']);
  });
});

describe('buildConsultorInsight', () => {
  it('conta agendamentos do dia (exclui cancelados)', () => {
    const lista = [agendamento({ data: '2026-07-16', status: 'agendado' }), agendamento({ data: '2026-07-16', status: 'cancelado' }), agendamento({ data: '2026-07-17' })];
    expect(buildConsultorInsight(lista, '2026-07-16').hojeCount).toBe(1);
  });

  it('acha o próximo não confirmado (status agendado) mais cedo do dia', () => {
    const lista = [
      agendamento({ clienteNome: 'Zeca', horaInicio: '14:00', status: 'agendado', data: '2026-07-16' }),
      agendamento({ clienteNome: 'Ana', horaInicio: '09:00', status: 'agendado', data: '2026-07-16' }),
      agendamento({ clienteNome: 'Bia', horaInicio: '08:00', status: 'confirmado', data: '2026-07-16' }),
    ];
    const insight = buildConsultorInsight(lista, '2026-07-16');
    expect(insight.naoConfirmadosHoje).toBe(2);
    expect(insight.proximoNaoConfirmado?.clienteNome).toBe('Ana');
  });

  it('encontra buracos livres de 45min+ na janela 06h-22h', () => {
    const lista = [agendamento({ horaInicio: '10:00', horaFim: '11:00', data: '2026-07-16', status: 'agendado' })];
    const insight = buildConsultorInsight(lista, '2026-07-16');
    // buraco antes (06h-10h = 240min) e depois (11h-22h = 660min)
    expect(insight.gapsHoje.length).toBeGreaterThanOrEqual(2);
    expect(insight.gapsHoje[0].inicio).toBe('06:00');
    expect(insight.gapsHoje[0].fim).toBe('10:00');
  });

  it('não reporta buracos menores que 45min', () => {
    const lista = [
      agendamento({ horaInicio: '06:00', horaFim: '14:00', data: '2026-07-16', status: 'agendado' }),
      agendamento({ horaInicio: '14:20', horaFim: '22:00', data: '2026-07-16', status: 'agendado' }),
    ];
    // buraco de 20min entre os dois — não deve aparecer
    const insight = buildConsultorInsight(lista, '2026-07-16');
    expect(insight.gapsHoje).toHaveLength(0);
  });

  it('sem agendamentos no dia, sem próximo não confirmado', () => {
    const insight = buildConsultorInsight([], '2026-07-16');
    expect(insight.hojeCount).toBe(0);
    expect(insight.proximoNaoConfirmado).toBeNull();
  });
});

describe('parseCentavosDigitados', () => {
  it('digitação livre vira centavos (sem separador)', () => {
    expect(parseCentavosDigitados('12345')).toBe(12345);
  });

  it('remove tudo que não é dígito', () => {
    expect(parseCentavosDigitados('R$ 123,45')).toBe(12345);
  });

  it('string vazia ou só não-dígitos vira 0', () => {
    expect(parseCentavosDigitados('')).toBe(0);
    expect(parseCentavosDigitados('R$ ')).toBe(0);
  });
});
