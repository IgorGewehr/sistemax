import { describe, it, expect } from 'vitest';

import type { RoiDoNegocioDto, RoiInvestimentoDto, RoiPaybackDto, RoiSerieMensalDto } from '@/lib/api/financeiro';

import { addMonthsIso, computeRoiChartLayout, mesLabel } from './deriveChart';

function investimento(totalCentavos: number): RoiInvestimentoDto {
  return {
    capexCentavos: totalCentavos,
    aportesCentavos: 0,
    totalCentavos,
    giroConsumidoObservadoCentavos: 0,
    bens: 1,
    porCategoria: [],
    resultadoAlienacaoTotalCentavos: 0,
  };
}

function serieItem(competencia: string, acumuladoCentavos: number): RoiSerieMensalDto {
  return {
    competencia,
    fluxoOperacionalCentavos: 0,
    capexCentavos: 0,
    aporteCentavos: 0,
    liquidoCentavos: 0,
    acumuladoCentavos,
    acumuladoDescontadoCentavos: acumuladoCentavos,
  };
}

function payback(overrides: Partial<RoiPaybackDto> = {}): RoiPaybackDto {
  return {
    simplesRealizadoEm: null,
    descontadoRealizadoEm: null,
    projetadoMeses: null,
    descontadoProjetadoMeses: null,
    metodo: 'simples',
    ...overrides,
  };
}

function roi(overrides: Partial<RoiDoNegocioDto> = {}): RoiDoNegocioDto {
  return {
    marcoInicial: '2026-01-01',
    taxaDescontoAnualBps: null,
    investimento: investimento(100000),
    recuperacao: {
      fluxoOperacionalAcumuladoCentavos: 0,
      recuperadoCentavos: 0,
      faltamCentavos: 0,
      percentRecuperado: 0,
    },
    payback: payback(),
    tir: { mensalPercent: null, anualizadaPercent: null, motivoIndefinida: null },
    roi: { caixaPercent: 0, competenciaPercent: 0, mesesAteRoiCompleto: null },
    serie: [],
    ...overrides,
  };
}

describe('addMonthsIso', () => {
  it('soma meses dentro do mesmo ano', () => {
    expect(addMonthsIso('2026-01-01', 3)).toBe('2026-04-01');
  });

  it('vira o ano quando a soma ultrapassa dezembro', () => {
    expect(addMonthsIso('2026-11-01', 3)).toBe('2027-02-01');
  });

  it('soma zero meses retorna o mesmo mês', () => {
    expect(addMonthsIso('2026-07-15', 0)).toBe('2026-07-01');
  });

  it('vira vários anos quando os meses ultrapassam 12', () => {
    expect(addMonthsIso('2026-06-01', 30)).toBe('2028-12-01');
  });
});

describe('mesLabel', () => {
  it('formata mês/ano abreviado em pt-BR (2 dígitos de ano)', () => {
    expect(mesLabel('2026-01-15')).toBe('jan/26');
    expect(mesLabel('2026-12-01')).toBe('dez/26');
  });

  it('não estoura índice do array de meses no limite dez/jan', () => {
    expect(mesLabel('2026-12-01')).toBe('dez/26');
    expect(mesLabel('2027-01-01')).toBe('jan/27');
  });
});

describe('computeRoiChartLayout — série vazia', () => {
  it('retorna layout de fallback sem caminho/crossPoint, sem quebrar', () => {
    const layout = computeRoiChartLayout(roi({ serie: [] }));

    expect(layout.solidPath).toBe('');
    expect(layout.dashedPath).toBeNull();
    expect(layout.gapPath).toBeNull();
    expect(layout.crossPoint).toBeNull();
    expect(layout.axisLabels).toEqual([]);
    expect(layout.investedTotalCentavos).toBe(100000);
  });
});

describe('computeRoiChartLayout — payback já realizado', () => {
  it('crossPoint marca o PRIMEIRO mês em que recuperado >= investido, com realizado=true', () => {
    const investido = 100000;
    const r = roi({
      investimento: investimento(investido),
      serie: [
        serieItem('2026-01-01', -80000), // recuperado = 20000
        serieItem('2026-02-01', -20000), // recuperado = 80000
        serieItem('2026-03-01', 5000), // recuperado = 105000 -> cruza aqui
        serieItem('2026-04-01', 40000), // recuperado = 140000 (já cruzou antes, não deve pegar este)
      ],
      payback: payback({ simplesRealizadoEm: '2026-03-01' }),
    });

    const layout = computeRoiChartLayout(r);

    expect(layout.crossPoint).not.toBeNull();
    expect(layout.crossPoint?.realizado).toBe(true);
    expect(layout.crossPoint?.label).toContain('realizado');
    expect(layout.crossPoint?.label).toContain(mesLabel('2026-03-01'));
    // Sem segmento futuro/projetado quando já realizado.
    expect(layout.dashedPath).toBeNull();
    expect(layout.gapPath).toBeNull();
  });

  it('sem nenhum mês onde recuperado >= investido (dado inconsistente): crossPoint fica null', () => {
    const r = roi({
      investimento: investimento(100000),
      serie: [serieItem('2026-01-01', -90000)], // recuperado = 10000, nunca cruza
      payback: payback({ simplesRealizadoEm: '2026-01-01' }),
    });

    expect(computeRoiChartLayout(r).crossPoint).toBeNull();
  });
});

describe('computeRoiChartLayout — payback projetado (ainda não realizado)', () => {
  it('projeta ponto futuro em hoje+projetadoMeses com recuperado==investido, dashed path e crossPoint.realizado=false', () => {
    const investido = 100000;
    const r = roi({
      investimento: investimento(investido),
      serie: [serieItem('2026-05-01', -60000), serieItem('2026-06-01', -40000)], // recuperado hoje = 60000
      payback: payback({ simplesRealizadoEm: null, projetadoMeses: 4 }),
    });

    const layout = computeRoiChartLayout(r);

    expect(layout.dashedPath).not.toBeNull();
    expect(layout.gapPath).not.toBeNull();
    expect(layout.crossPoint).not.toBeNull();
    expect(layout.crossPoint?.realizado).toBe(false);
    expect(layout.crossPoint?.label).toContain('projetado');
    expect(layout.crossPoint?.label).toContain(mesLabel(addMonthsIso('2026-06-01', 4)));
    // O ponto projetado cruza exatamente na linha do investido — cross.y == investedY.
    expect(layout.crossPoint?.y).toBe(layout.investedY);
  });

  it('projetadoMeses null: sem segmento futuro, crossPoint null (nada pra projetar)', () => {
    const r = roi({
      serie: [serieItem('2026-05-01', -60000)],
      payback: payback({ simplesRealizadoEm: null, projetadoMeses: null }),
    });

    const layout = computeRoiChartLayout(r);
    expect(layout.dashedPath).toBeNull();
    expect(layout.gapPath).toBeNull();
    expect(layout.crossPoint).toBeNull();
  });

  it('projetadoMeses == 0 (ou negativo) não gera segmento futuro (guard > 0)', () => {
    const r = roi({
      serie: [serieItem('2026-05-01', -60000)],
      payback: payback({ simplesRealizadoEm: null, projetadoMeses: 0 }),
    });

    const layout = computeRoiChartLayout(r);
    expect(layout.dashedPath).toBeNull();
    expect(layout.crossPoint).toBeNull();
  });
});

describe('computeRoiChartLayout — axisLabels', () => {
  it('rotula o primeiro mês, "hoje" no último histórico e o mês projetado quando existir', () => {
    const r = roi({
      serie: [serieItem('2026-01-01', -90000), serieItem('2026-02-01', -60000)],
      payback: payback({ simplesRealizadoEm: null, projetadoMeses: 3 }),
    });

    const layout = computeRoiChartLayout(r);
    expect(layout.axisLabels).toHaveLength(3);
    expect(layout.axisLabels[0].label).toBe(mesLabel('2026-01-01'));
    expect(layout.axisLabels[1].label).toContain('hoje');
    expect(layout.axisLabels[1].label).toContain(mesLabel('2026-02-01'));
    expect(layout.axisLabels[2].label).toBe(mesLabel(addMonthsIso('2026-02-01', 3)));
  });

  it('um único ponto histórico não duplica o rótulo "hoje"', () => {
    const r = roi({ serie: [serieItem('2026-01-01', -90000)], payback: payback() });
    const layout = computeRoiChartLayout(r);
    expect(layout.axisLabels).toHaveLength(1);
  });
});
