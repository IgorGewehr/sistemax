import { describe, it, expect } from 'vitest';

import {
  HOJE,
  hoje,
  addDias,
  diasEntre,
  diasDesde,
  ehTerminal,
  totalOrcamento,
  totalExecucaoAtual,
  valorAtual,
  entrouEm,
  atrasada,
  orcamentoVencido,
  buildKpis,
  buildConsultorInsight,
  bucketDados,
  bucketPorChave,
  bucketItensOrdenados,
  bucketDrillStats,
  operacaoStats,
  centavosOuTraco,
  filtrarFila,
  passosDaLinhaDoTempo,
  indiceAtualDaLinha,
} from './calc';
import type { OrdemServico, Orcamento } from './types';

function orcamento(overrides: Partial<Orcamento> = {}): Orcamento {
  return {
    pecas: [{ desc: 'Tela', produtoId: 'p1', qtd: 1, preco: 20_000 }],
    maoDeObra: 10_000,
    validadeDias: 5,
    enviadoEm: hoje(-3),
    ...overrides,
  };
}

function os(overrides: Partial<OrdemServico> = {}): OrdemServico {
  return {
    numero: 'OS-001',
    cliente: 'João Silva',
    telefone: '11999999999',
    equipamento: 'Notebook',
    marca: 'Dell',
    modelo: 'XPS',
    serie: 'ABC123',
    senha: null,
    acessorios: '',
    estadoEntrada: 'ok',
    defeito: 'não liga',
    tecnico: null,
    abertaEm: hoje(-10),
    prazo: null,
    status: 'Aberta',
    diagnostico: null,
    orcamento: null,
    aprovacao: null,
    historico: [{ para: 'Aberta', em: hoje(-10) }],
    ...overrides,
  };
}

describe('addDias / diasEntre / diasDesde', () => {
  it('addDias soma dias sem mutar a data original', () => {
    const d = hoje(0);
    const d2 = addDias(d, 5);
    expect(d2.getTime()).toBeGreaterThan(d.getTime());
    expect(diasEntre(d, d2)).toBe(5);
  });

  it('diasDesde nunca é negativo (clamp em 0 para datas futuras)', () => {
    expect(diasDesde(hoje(5))).toBe(0);
    expect(diasDesde(hoje(-5))).toBe(5);
  });
});

describe('ehTerminal', () => {
  it('Entregue, Cancelada e DevolvidaSemReparo são terminais', () => {
    expect(ehTerminal('Entregue')).toBe(true);
    expect(ehTerminal('Cancelada')).toBe(true);
    expect(ehTerminal('DevolvidaSemReparo')).toBe(true);
  });

  it('demais status não são terminais', () => {
    expect(ehTerminal('Aberta')).toBe(false);
    expect(ehTerminal('EmExecucao')).toBe(false);
    expect(ehTerminal('Pronta')).toBe(false);
  });
});

describe('totalOrcamento', () => {
  it('mão de obra + soma(preco*qtd) das peças', () => {
    const orc = orcamento({
      maoDeObra: 10_000,
      pecas: [
        { desc: 'A', produtoId: 'p1', qtd: 2, preco: 5_000 },
        { desc: 'B', produtoId: 'p2', qtd: 1, preco: 3_000 },
      ],
    });
    expect(totalOrcamento(orc)).toBe(10_000 + 2 * 5_000 + 1 * 3_000);
  });

  it('orçamento null retorna 0', () => {
    expect(totalOrcamento(null)).toBe(0);
  });
});

describe('totalExecucaoAtual', () => {
  it('usa maoDeObraFinal e pecasExecucao quando definidos', () => {
    const item = os({
      maoDeObraFinal: 8_000,
      pecasExecucao: [{ desc: 'Tela', produtoId: 'p1', qtd: 1, preco: 20_000, linhaId: 'l1', origem: 'orcada', aplicada: true }],
    });
    expect(totalExecucaoAtual(item)).toBe(8_000 + 20_000);
  });

  it('sem maoDeObraFinal, cai pro maoDeObra do orçamento', () => {
    const item = os({ orcamento: orcamento({ maoDeObra: 5_000 }) });
    expect(totalExecucaoAtual(item)).toBe(5_000);
  });

  it('sem orçamento nem maoDeObraFinal, mão de obra é 0', () => {
    expect(totalExecucaoAtual(os())).toBe(0);
  });
});

describe('valorAtual', () => {
  it('Entregue: valorServico + valorPecas', () => {
    expect(valorAtual(os({ status: 'Entregue', valorServico: 5_000, valorPecas: 3_000 }))).toBe(8_000);
  });

  it('DevolvidaSemReparo: só a taxa de diagnóstico', () => {
    expect(valorAtual(os({ status: 'DevolvidaSemReparo', taxaDiagnostico: 5_000 }))).toBe(5_000);
  });

  it('Cancelada: sempre 0, mesmo com orçamento presente', () => {
    expect(valorAtual(os({ status: 'Cancelada', orcamento: orcamento() }))).toBe(0);
  });

  it('EmExecucao/Pronta: usa totalExecucaoAtual', () => {
    const item = os({ status: 'Pronta', maoDeObraFinal: 2_000 });
    expect(valorAtual(item)).toBe(2_000);
  });

  it('AguardandoAprovacao/Aprovada/Reprovada: usa totalOrcamento', () => {
    const orc = orcamento({ maoDeObra: 1_000, pecas: [] });
    expect(valorAtual(os({ status: 'AguardandoAprovacao', orcamento: orc }))).toBe(1_000);
  });

  it('demais status (Aberta, EmDiagnostico): 0', () => {
    expect(valorAtual(os({ status: 'Aberta' }))).toBe(0);
    expect(valorAtual(os({ status: 'EmDiagnostico' }))).toBe(0);
  });
});

describe('entrouEm', () => {
  it('acha a data em que a OS entrou num status pelo histórico', () => {
    const d = hoje(-2);
    const item = os({ historico: [{ para: 'Aberta', em: hoje(-10) }, { para: 'EmDiagnostico', em: d }] });
    expect(entrouEm(item, 'EmDiagnostico')).toBe(d);
  });

  it('status nunca visitado retorna null', () => {
    expect(entrouEm(os(), 'Entregue')).toBeNull();
  });
});

describe('atrasada', () => {
  it('true quando hoje > prazo e status não é terminal', () => {
    expect(atrasada(os({ status: 'EmExecucao', prazo: hoje(-1) }))).toBe(true);
  });

  it('false quando o status é terminal, mesmo com prazo vencido', () => {
    expect(atrasada(os({ status: 'Entregue', prazo: hoje(-5) }))).toBe(false);
  });

  it('false quando não há prazo definido', () => {
    expect(atrasada(os({ status: 'EmExecucao', prazo: null }))).toBe(false);
  });

  it('false quando o prazo é hoje mesmo (comparação por data, ignora hora)', () => {
    expect(atrasada(os({ status: 'EmExecucao', prazo: HOJE }))).toBe(false);
  });
});

describe('orcamentoVencido', () => {
  it('true quando passou da validade do orçamento enviado', () => {
    const orc = orcamento({ enviadoEm: hoje(-10), validadeDias: 5 });
    expect(orcamentoVencido(os({ status: 'AguardandoAprovacao', orcamento: orc }))).toBe(true);
  });

  it('false quando ainda dentro da validade', () => {
    const orc = orcamento({ enviadoEm: hoje(-1), validadeDias: 5 });
    expect(orcamentoVencido(os({ status: 'AguardandoAprovacao', orcamento: orc }))).toBe(false);
  });

  it('false se o status não é AguardandoAprovacao, mesmo com orçamento vencido', () => {
    const orc = orcamento({ enviadoEm: hoje(-10), validadeDias: 5 });
    expect(orcamentoVencido(os({ status: 'Aprovada', orcamento: orc }))).toBe(false);
  });

  it('false sem orçamento', () => {
    expect(orcamentoVencido(os({ status: 'AguardandoAprovacao', orcamento: null }))).toBe(false);
  });
});

describe('buildKpis', () => {
  it('agrega naBancada (ativas), esperando (aguardando aprovação) e prontas', () => {
    const lista: OrdemServico[] = [
      os({ status: 'Aberta' }),
      os({ status: 'AguardandoAprovacao', orcamento: orcamento({ enviadoEm: hoje(-3), maoDeObra: 1_000, pecas: [] }) }),
      os({ status: 'Pronta', maoDeObraFinal: 2_000 }),
      os({ status: 'Entregue' }), // terminal, fora de "ativas"
    ];
    const kpis = buildKpis(lista);
    expect(kpis.naBancada.count).toBe(3); // tudo exceto Entregue
    expect(kpis.esperando.count).toBe(1);
    expect(kpis.esperando.valorCentavos).toBe(1_000);
    expect(kpis.prontas.count).toBe(1);
    expect(kpis.prontas.valorCentavos).toBe(2_000);
  });

  it('faturado do mês: servPct + pecPct sempre somam 100 (complemento)', () => {
    const lista: OrdemServico[] = [
      os({ status: 'Entregue', dataEntrega: HOJE, valorServico: 3_000, valorPecas: 7_000 }),
    ];
    const kpis = buildKpis(lista);
    expect(kpis.faturado.servicoPct + kpis.faturado.pecasPct).toBe(100);
    expect(kpis.faturado.valorCentavos).toBe(10_000);
  });

  it('sem faturamento no mês: servPct 0 (sem divisão por zero)', () => {
    const kpis = buildKpis([]);
    expect(kpis.faturado.valorCentavos).toBe(0);
    expect(kpis.faturado.servicoPct).toBe(0);
    expect(kpis.faturado.pecasPct).toBe(100);
  });

  it('prontasMaisAntigaDias é null quando não há prontas', () => {
    expect(buildKpis([]).prontas.maisAntigaDias).toBeNull();
  });
});

describe('buildConsultorInsight', () => {
  it('filtra orçamentos parados 5+ dias e acha o de maior valor', () => {
    const lista: OrdemServico[] = [
      os({
        numero: 'OS-A',
        cliente: 'Ana Paula',
        equipamento: 'TV',
        status: 'AguardandoAprovacao',
        orcamento: orcamento({ enviadoEm: hoje(-6), maoDeObra: 5_000, pecas: [] }),
      }),
      os({
        numero: 'OS-B',
        cliente: 'Beto',
        equipamento: 'Celular',
        status: 'AguardandoAprovacao',
        orcamento: orcamento({ enviadoEm: hoje(-6), maoDeObra: 9_000, pecas: [] }),
      }),
      os({
        numero: 'OS-C',
        status: 'AguardandoAprovacao',
        orcamento: orcamento({ enviadoEm: hoje(-2), maoDeObra: 20_000, pecas: [] }), // só 2 dias, não conta
      }),
    ];
    const insight = buildConsultorInsight(lista);
    expect(insight.qtdEsperaLonga).toBe(2);
    expect(insight.maiorAguardando?.numero).toBe('OS-B');
    expect(insight.maiorAguardando?.clientePrimeiroNome).toBe('Beto');
    expect(insight.valorParadoCentavos).toBe(5_000 + 9_000);
  });

  it('sem espera longa, maiorAguardando é null', () => {
    expect(buildConsultorInsight([]).maiorAguardando).toBeNull();
  });
});

describe('bucketDados / bucketPorChave', () => {
  it('agrupa OS por balde de status e soma valor', () => {
    const lista: OrdemServico[] = [
      os({ status: 'AguardandoAprovacao', orcamento: orcamento({ maoDeObra: 1_000, pecas: [] }) }),
      os({ status: 'Pronta', maoDeObraFinal: 2_000 }),
    ];
    const buckets = bucketDados(lista);
    const aguardando = buckets.find((b) => b.key === 'aguardando')!;
    expect(aguardando.count).toBe(1);
    expect(aguardando.valor).toBe(1_000);
  });

  it('bucketPorChave lança erro para chave desconhecida', () => {
    expect(() => bucketPorChave([], 'inexistente' as never)).toThrow();
  });
});

describe('bucketItensOrdenados', () => {
  it('ordena por data de entrada na etapa, mais antiga primeiro', () => {
    const bucket = bucketDados([
      os({ numero: 'recente', status: 'Aberta', abertaEm: hoje(-1), historico: [{ para: 'Aberta', em: hoje(-1) }] }),
      os({ numero: 'antiga', status: 'Aberta', abertaEm: hoje(-10), historico: [{ para: 'Aberta', em: hoje(-10) }] }),
    ]).find((b) => b.key === 'abertas')!;
    const ordenado = bucketItensOrdenados(bucket);
    expect(ordenado[0].numero).toBe('antiga');
  });
});

describe('bucketDrillStats', () => {
  it('calcula tempo médio e mais antiga em dias', () => {
    const bucket = bucketDados([
      os({ status: 'Aberta', abertaEm: hoje(-4), historico: [{ para: 'Aberta', em: hoje(-4) }] }),
      os({ status: 'Aberta', abertaEm: hoje(-8), historico: [{ para: 'Aberta', em: hoje(-8) }] }),
    ]).find((b) => b.key === 'abertas')!;
    const stats = bucketDrillStats(bucket);
    expect(stats.tempoMedioDias).toBe(6);
    expect(stats.maisAntigaDias).toBe(8);
  });

  it('bucket vazio: zeros, sem NaN', () => {
    const bucket = bucketDados([]).find((b) => b.key === 'abertas')!;
    const stats = bucketDrillStats(bucket);
    expect(stats.tempoMedioDias).toBe(0);
    expect(stats.maisAntigaDias).toBe(0);
  });
});

describe('operacaoStats', () => {
  it('taxa de aprovação = aprovadas / decididas, ticket médio das OS com valor > 0', () => {
    const lista: OrdemServico[] = [
      os({ status: 'Entregue', abertaEm: hoje(-5), dataEntrega: hoje(0), valorServico: 1_000, valorPecas: 0, aprovacao: { decisao: 'Aprovada', canal: 'WhatsApp', em: hoje(-3) } }),
      os({ status: 'Reprovada', aprovacao: { decisao: 'Reprovada', canal: 'Telefone', em: hoje(-1) } }),
    ];
    const stats = operacaoStats(lista);
    expect(stats.decididasCount).toBe(2);
    expect(stats.aprovadasCount).toBe(1);
    expect(stats.taxaAprovacaoPct).toBe(50);
    expect(stats.portaAPortaDias).toBe(5);
  });

  it('lista vazia: zeros sem divisão por zero', () => {
    const stats = operacaoStats([]);
    expect(stats.portaAPortaDias).toBe(0);
    expect(stats.taxaAprovacaoPct).toBe(0);
    expect(stats.ticketMedioCentavos).toBe(0);
  });
});

describe('centavosOuTraco', () => {
  it('zero ou negativo vira null ("nada a mostrar")', () => {
    expect(centavosOuTraco(0)).toBeNull();
    expect(centavosOuTraco(-100)).toBeNull();
  });

  it('positivo passa direto', () => {
    expect(centavosOuTraco(500)).toBe(500);
  });
});

describe('filtrarFila', () => {
  it('filtro "ativas" exclui terminais, "encerradas" só terminais', () => {
    const lista: OrdemServico[] = [os({ numero: 'a', status: 'Aberta' }), os({ numero: 'b', status: 'Entregue' })];
    expect(filtrarFila(lista, 'ativas', '').map((o) => o.numero)).toEqual(['a']);
    expect(filtrarFila(lista, 'encerradas', '').map((o) => o.numero)).toEqual(['b']);
    expect(filtrarFila(lista, 'todas', '')).toHaveLength(2);
  });

  it('busca filtra por número, cliente ou equipamento (case-insensitive)', () => {
    const lista: OrdemServico[] = [os({ numero: 'OS-999', cliente: 'Zeca', equipamento: 'Furadeira' })];
    expect(filtrarFila(lista, 'todas', 'zeca')).toHaveLength(1);
    expect(filtrarFila(lista, 'todas', 'inexistente')).toHaveLength(0);
  });

  it('ordena atrasadas primeiro, depois por antiguidade', () => {
    const lista: OrdemServico[] = [
      os({ numero: 'no-prazo', status: 'EmExecucao', prazo: hoje(10), abertaEm: hoje(-1), historico: [{ para: 'EmExecucao', em: hoje(-1) }] }),
      os({ numero: 'atrasada', status: 'EmExecucao', prazo: hoje(-1), abertaEm: hoje(-1), historico: [{ para: 'EmExecucao', em: hoje(-1) }] }),
    ];
    expect(filtrarFila(lista, 'todas', '')[0].numero).toBe('atrasada');
  });
});

describe('passosDaLinhaDoTempo / indiceAtualDaLinha', () => {
  it('ramo reprovado usa passos alternativos (Reprovada/DevolvidaSemReparo)', () => {
    const passos = passosDaLinhaDoTempo(os({ status: 'Reprovada' }));
    expect(passos).toContain('Reprovada');
    expect(passos).not.toContain('Entregue');
  });

  it('ramo principal segue a ordem padrão', () => {
    const passos = passosDaLinhaDoTempo(os({ status: 'EmExecucao' }));
    expect(passos).toEqual(['Aberta', 'EmDiagnostico', 'AguardandoAprovacao', 'Aprovada', 'EmExecucao', 'Pronta', 'Entregue']);
  });

  it('Cancelada acha o índice do último status antes de cancelar', () => {
    const item = os({
      status: 'Cancelada',
      historico: [{ para: 'Aberta', em: hoje(-5) }, { para: 'EmDiagnostico', em: hoje(-3) }, { para: 'Cancelada', em: hoje(-1) }],
    });
    const passos = passosDaLinhaDoTempo(item);
    expect(indiceAtualDaLinha(item, passos)).toBe(passos.indexOf('EmDiagnostico'));
  });
});
