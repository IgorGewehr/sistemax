import { describe, it, expect } from 'vitest';

import {
  diasEntre,
  ehAniversarianteNoMes,
  ehAniversarianteNaSemana,
  ehNovoNoMes,
  estaSemComprar90d,
  buildKpis,
  somaGastoVidaCentavos,
  filtrarClientes,
  clienteById,
  statusHistoricoTone,
  parseAniversario,
  buildSparkline,
  ticketMedioExibicaoCentavos,
  frequenciaMediaDias,
} from './calc';
import type { Cliente } from './types';

function cliente(overrides: Partial<Cliente> = {}): Cliente {
  return {
    id: 'c1',
    nome: 'Maria Souza',
    telefone: '11999999999',
    email: 'maria@example.com',
    aniversario: null,
    enderecoResumo: null,
    observacoes: null,
    tags: [],
    status: 'ativo',
    criadoEm: '01/01/2025',
    ultimaVisita: null,
    comprasCount: 0,
    ticketMedioCentavos: 0,
    totalGasto12mCentavos: 0,
    totalGastoVidaCentavos: 0,
    ...overrides,
  };
}

const HOJE = '16/07/2026';

describe('diasEntre', () => {
  it('calcula dias corridos entre duas datas DD/MM/AAAA', () => {
    expect(diasEntre('01/07/2026', '16/07/2026')).toBe(15);
  });

  it('datas iguais retornam 0', () => {
    expect(diasEntre('16/07/2026', '16/07/2026')).toBe(0);
  });

  it('funciona atravessando virada de ano', () => {
    expect(diasEntre('31/12/2025', '01/01/2026')).toBe(1);
  });

  it('data futura (invertida) retorna negativo', () => {
    expect(diasEntre('16/07/2026', '01/07/2026')).toBe(-15);
  });
});

describe('ehAniversarianteNoMes', () => {
  it('compara só o mês, ignora o dia', () => {
    expect(ehAniversarianteNoMes('01/07', HOJE)).toBe(true);
    expect(ehAniversarianteNoMes('31/07', HOJE)).toBe(true);
    expect(ehAniversarianteNoMes('01/08', HOJE)).toBe(false);
  });

  it('null retorna false (sem aniversário cadastrado)', () => {
    expect(ehAniversarianteNoMes(null, HOJE)).toBe(false);
  });
});

describe('ehAniversarianteNaSemana', () => {
  it('true quando o aniversário cai nos próximos 7 dias (incluindo hoje, janela 0..6)', () => {
    expect(ehAniversarianteNaSemana('16/07', HOJE)).toBe(true); // hoje (diff 0)
    expect(ehAniversarianteNaSemana('20/07', HOJE)).toBe(true); // +4 dias
    expect(ehAniversarianteNaSemana('22/07', HOJE)).toBe(true); // +6 dias (limite da janela)
  });

  it('false quando fora da janela de 7 dias', () => {
    expect(ehAniversarianteNaSemana('23/07', HOJE)).toBe(false); // +7 dias, já fora
    expect(ehAniversarianteNaSemana('01/07', HOJE)).toBe(false); // já passou
  });

  it('cobre a virada dezembro→janeiro', () => {
    expect(ehAniversarianteNaSemana('02/01', '28/12/2026')).toBe(true);
  });

  it('null retorna false', () => {
    expect(ehAniversarianteNaSemana(null, HOJE)).toBe(false);
  });
});

describe('ehNovoNoMes', () => {
  it('true quando criado no mesmo mês/ano de referência', () => {
    expect(ehNovoNoMes('10/07/2026', HOJE)).toBe(true);
  });

  it('false quando o ano difere, mesmo com mês igual', () => {
    expect(ehNovoNoMes('10/07/2025', HOJE)).toBe(false);
  });
});

describe('estaSemComprar90d', () => {
  it('true só para clientes ativos com última visita há 90+ dias', () => {
    const c = cliente({ status: 'ativo', ultimaVisita: '01/01/2026' }); // muito mais de 90 dias antes de 16/07
    expect(estaSemComprar90d(c, HOJE)).toBe(true);
  });

  it('false para clientes inativos, mesmo há muito tempo sem comprar', () => {
    const c = cliente({ status: 'inativo', ultimaVisita: '01/01/2026' });
    expect(estaSemComprar90d(c, HOJE)).toBe(false);
  });

  it('false quando nunca comprou (ultimaVisita null)', () => {
    expect(estaSemComprar90d(cliente({ ultimaVisita: null }), HOJE)).toBe(false);
  });

  it('false quando comprou há menos de 90 dias', () => {
    expect(estaSemComprar90d(cliente({ ultimaVisita: '01/07/2026' }), HOJE)).toBe(false);
  });

  it('exatamente 90 dias conta (>= 90, não > 90)', () => {
    // 90 dias antes de 16/07/2026 = 17/04/2026
    expect(estaSemComprar90d(cliente({ ultimaVisita: '17/04/2026' }), HOJE)).toBe(true);
  });
});

describe('buildKpis', () => {
  it('agrega contagens do segmento de clientes ativos', () => {
    const clientes = [
      cliente({ id: 'a', status: 'ativo', criadoEm: HOJE, aniversario: '16/07' }),
      cliente({ id: 'b', status: 'ativo', ultimaVisita: '01/01/2026' }),
      cliente({ id: 'c', status: 'inativo', ultimaVisita: '01/01/2026' }), // excluído por status
    ];
    const kpis = buildKpis(clientes, HOJE);
    expect(kpis.clientesAtivos).toBe(2);
    expect(kpis.novosNoMes).toBe(1);
    expect(kpis.aniversariantesNoMes).toHaveLength(1);
    expect(kpis.semComprar90d).toHaveLength(1);
    expect(kpis.semComprar90d[0].id).toBe('b');
  });

  it('lista vazia produz KPIs zerados', () => {
    const kpis = buildKpis([], HOJE);
    expect(kpis.clientesAtivos).toBe(0);
    expect(kpis.aniversariantesNoMes).toEqual([]);
    expect(kpis.semComprar90d).toEqual([]);
  });
});

describe('somaGastoVidaCentavos', () => {
  it('soma totalGastoVidaCentavos de uma lista de clientes', () => {
    const clientes = [cliente({ totalGastoVidaCentavos: 1000 }), cliente({ totalGastoVidaCentavos: 2500 })];
    expect(somaGastoVidaCentavos(clientes)).toBe(3500);
  });

  it('lista vazia soma 0 (nunca hardcoded)', () => {
    expect(somaGastoVidaCentavos([])).toBe(0);
  });
});

describe('filtrarClientes', () => {
  const clientes = [
    cliente({ id: 'a', nome: 'Ana', aniversario: '16/07', telefone: '111', email: 'a@x.com' }),
    cliente({ id: 'b', nome: 'Bruno', ultimaVisita: '01/01/2026', telefone: '222', email: 'b@x.com' }),
  ];

  it('filtro "aniversariantes" restringe ao mês corrente', () => {
    expect(filtrarClientes(clientes, 'aniversariantes', '', HOJE).map((c) => c.id)).toEqual(['a']);
  });

  it('filtro "semComprar90d" restringe ao segmento', () => {
    expect(filtrarClientes(clientes, 'semComprar90d', '', HOJE).map((c) => c.id)).toEqual(['b']);
  });

  it('busca normalizada casa por nome/telefone/email (case-insensitive, já em lowercase)', () => {
    expect(filtrarClientes(clientes, 'todos' as never, 'bruno', HOJE).map((c) => c.id)).toEqual(['b']);
  });

  it('sem busca, "todos" retorna a base completa', () => {
    expect(filtrarClientes(clientes, 'todos' as never, '', HOJE)).toHaveLength(2);
  });
});

describe('clienteById', () => {
  it('acha por id, undefined se não existe', () => {
    const clientes = [cliente({ id: 'x' })];
    expect(clienteById(clientes, 'x')?.id).toBe('x');
    expect(clienteById(clientes, 'y')).toBeUndefined();
  });
});

describe('statusHistoricoTone', () => {
  it('"conclu..." vira pos, "andamento" vira warn, resto faint', () => {
    expect(statusHistoricoTone('Concluída')).toBe('pos');
    expect(statusHistoricoTone('Em andamento')).toBe('warn');
    expect(statusHistoricoTone('Orçamento')).toBe('faint');
    expect(statusHistoricoTone(undefined)).toBe('faint');
  });
});

describe('parseAniversario', () => {
  it('string vazia é válida, vira null (campo opcional)', () => {
    expect(parseAniversario('')).toBeNull();
    expect(parseAniversario('   ')).toBeNull();
  });

  it('formato DD/MM válido normaliza com zero à esquerda', () => {
    expect(parseAniversario('5/8')).toBe('05/08');
    expect(parseAniversario('29/02')).toBe('29/02'); // válido, ano bissexto de referência
  });

  it('mês fora do intervalo é inválido (undefined)', () => {
    expect(parseAniversario('10/13')).toBeUndefined();
    expect(parseAniversario('10/00')).toBeUndefined();
  });

  it('dia inválido pro mês (ex.: 31 de abril) retorna undefined', () => {
    expect(parseAniversario('31/04')).toBeUndefined();
  });

  it('formato não numérico ou incompleto retorna undefined', () => {
    expect(parseAniversario('abc')).toBeUndefined();
    expect(parseAniversario('10')).toBeUndefined();
  });
});

describe('buildSparkline', () => {
  it('gera path/area válidos sem NaN mesmo com série constante', () => {
    const s = buildSparkline([5, 5, 5]);
    expect(s.path.includes('NaN')).toBe(false);
  });

  it('série de 1 ponto não divide por zero', () => {
    expect(buildSparkline([10]).lastPoint[0]).toBe(0);
  });
});

describe('ticketMedioExibicaoCentavos', () => {
  it('retorna o ticket médio quando há compras', () => {
    expect(ticketMedioExibicaoCentavos(cliente({ comprasCount: 3, ticketMedioCentavos: 1500 }))).toBe(1500);
  });

  it('retorna 0 quando nunca comprou (evita mostrar ticket "fantasma")', () => {
    expect(ticketMedioExibicaoCentavos(cliente({ comprasCount: 0, ticketMedioCentavos: 999 }))).toBe(0);
  });
});

describe('frequenciaMediaDias', () => {
  it('divide dias desde o cadastro pelo nº de compras', () => {
    const c = cliente({ criadoEm: '01/07/2026', comprasCount: 5 });
    // 15 dias desde o cadastro / 5 compras = 3
    expect(frequenciaMediaDias(c, HOJE)).toBe(3);
  });

  it('null quando nunca comprou (evita divisão por zero e frequência sem sentido)', () => {
    expect(frequenciaMediaDias(cliente({ comprasCount: 0 }), HOJE)).toBeNull();
  });

  it('piso de 1 dia mesmo se cadastrado hoje (evita divisão por zero)', () => {
    const c = cliente({ criadoEm: HOJE, comprasCount: 2 });
    expect(frequenciaMediaDias(c, HOJE)).toBe(Math.round(1 / 2));
  });
});
