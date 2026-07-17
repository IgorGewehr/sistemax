import { describe, it, expect } from 'vitest';

import type { InadimplenciaDto, PontoDeEquilibrioDto, PrevisaoDeCaixaDto, RadarDoSimplesDto } from '@/lib/api/financeiro';

import { deBreakevenDto, deInadimplenciaDto, deRadarSimplesDto, deRunwayDto } from './sobrevivencia';

describe('deRunwayDto', () => {
  it('mapeia os campos 1:1 e formata a data de dia curto quando presente', () => {
    const dto: PrevisaoDeCaixaDto = {
      bandas: [],
      probabilidadeSaldoNegativoEm30Dias: 0.12,
      primeiroDiaP50Negativo: '2026-08-15',
      diasRunwayBruto: 90,
      diasRunwayRealista: 60,
    };

    const vm = deRunwayDto(dto);

    expect(vm.diasRunwayRealista).toBe(60);
    expect(vm.diasRunwayBruto).toBe(90);
    expect(vm.probabilidadeSaldoNegativoEm30Dias).toBe(0.12);
    expect(vm.primeiroDiaP50NegativoLabel).toBe('15/08');
  });

  it('primeiroDiaP50Negativo nulo vira label nulo (não formata data ausente)', () => {
    const dto: PrevisaoDeCaixaDto = {
      bandas: [],
      probabilidadeSaldoNegativoEm30Dias: 0,
      primeiroDiaP50Negativo: null,
      diasRunwayBruto: null,
      diasRunwayRealista: null,
    };

    expect(deRunwayDto(dto).primeiroDiaP50NegativoLabel).toBeNull();
  });
});

describe('deBreakevenDto', () => {
  it('calcula progressoPercentual como razão acumulado/necessário, arredondada', () => {
    const dto: PontoDeEquilibrioDto = {
      custosFixosMensaisCentavos: 500000,
      margemContribuicaoPercentual: 0.4,
      receitaNecessariaMensalCentavos: 1000000,
      receitaNecessariaDiariaCentavos: 33333,
      receitaAcumuladaNoMesCentavos: 400000,
      diaDoEquilibrio: 20,
      jaAtingiuNoMes: false,
    };

    expect(deBreakevenDto(dto).progressoPercentual).toBe(40);
  });

  it('progressoPercentual é limitado (clamp) em 100 mesmo se acumulado ultrapassar o necessário', () => {
    const dto: PontoDeEquilibrioDto = {
      custosFixosMensaisCentavos: 0,
      margemContribuicaoPercentual: 1,
      receitaNecessariaMensalCentavos: 100,
      receitaNecessariaDiariaCentavos: 0,
      receitaAcumuladaNoMesCentavos: 1000,
      diaDoEquilibrio: 5,
      jaAtingiuNoMes: true,
    };

    expect(deBreakevenDto(dto).progressoPercentual).toBe(100);
  });

  it('receitaNecessariaMensalCentavos == 0 não divide por zero — progresso cai para 0', () => {
    const dto: PontoDeEquilibrioDto = {
      custosFixosMensaisCentavos: 0,
      margemContribuicaoPercentual: 0,
      receitaNecessariaMensalCentavos: 0,
      receitaNecessariaDiariaCentavos: 0,
      receitaAcumuladaNoMesCentavos: 500,
      diaDoEquilibrio: null,
      jaAtingiuNoMes: false,
    };

    const vm = deBreakevenDto(dto);
    expect(vm.progressoPercentual).toBe(0);
    expect(Number.isFinite(vm.progressoPercentual)).toBe(true);
  });
});

describe('deInadimplenciaDto', () => {
  it('filtra a faixa 0 ("Em dia") e traduz o ordinal restante para o rótulo pt-BR', () => {
    const dto: InadimplenciaDto = {
      valorTotalEmAbertoCentavos: 100000,
      provisaoEsperadaCentavos: 8000,
      valorLiquidoEsperadoCentavos: 92000,
      porFaixa: [
        { faixa: 0, valorCentavos: 60000, provisaoCentavos: 0, quantidade: 10 },
        { faixa: 1, valorCentavos: 20000, provisaoCentavos: 1000, quantidade: 3 },
        { faixa: 3, valorCentavos: 20000, provisaoCentavos: 7000, quantidade: 2 },
      ],
    };

    const vm = deInadimplenciaDto(dto);

    expect(vm.porFaixa).toHaveLength(2);
    expect(vm.porFaixa.map((f) => f.label)).toEqual(['Até 30 dias', '61–90 dias']);
    expect(vm.valorTotalEmAbertoCentavos).toBe(100000);
  });

  it('faixa ordinal desconhecida cai no fallback "Faixa N" em vez de quebrar', () => {
    const dto: InadimplenciaDto = {
      valorTotalEmAbertoCentavos: 0,
      provisaoEsperadaCentavos: 0,
      valorLiquidoEsperadoCentavos: 0,
      porFaixa: [{ faixa: 9 as unknown as 0, valorCentavos: 100, provisaoCentavos: 0, quantidade: 1 }],
    };

    expect(deInadimplenciaDto(dto).porFaixa[0].label).toBe('Faixa 9');
  });

  it('sem nenhuma faixa em atraso (só "Em dia"), porFaixa fica vazio', () => {
    const dto: InadimplenciaDto = {
      valorTotalEmAbertoCentavos: 0,
      provisaoEsperadaCentavos: 0,
      valorLiquidoEsperadoCentavos: 0,
      porFaixa: [{ faixa: 0, valorCentavos: 100, provisaoCentavos: 0, quantidade: 1 }],
    };

    expect(deInadimplenciaDto(dto).porFaixa).toEqual([]);
  });
});

describe('deRadarSimplesDto', () => {
  it('mapeia os campos 1:1 (sem cálculo extra)', () => {
    const dto: RadarDoSimplesDto = {
      rbt12Centavos: 3600000,
      faixaAtual: 2,
      aliquotaEfetiva: 0.095,
      aliquotaNominalFaixaAtual: 0.11,
      distanciaAoProximoDegrauCentavos: 400000,
      mesesProjetadosAteOProximoDegrau: 4,
    };

    const vm = deRadarSimplesDto(dto);

    expect(vm.rbt12Centavos).toBe(3600000);
    expect(vm.faixaAtual).toBe(2);
    expect(vm.aliquotaEfetiva).toBe(0.095);
    expect(vm.distanciaAoProximoDegrauCentavos).toBe(400000);
    expect(vm.mesesProjetadosAteOProximoDegrau).toBe(4);
  });

  it('mesesProjetadosAteOProximoDegrau nulo passa direto (já na faixa mais alta)', () => {
    const dto: RadarDoSimplesDto = {
      rbt12Centavos: 0,
      faixaAtual: 6,
      aliquotaEfetiva: 0,
      aliquotaNominalFaixaAtual: 0,
      distanciaAoProximoDegrauCentavos: 0,
      mesesProjetadosAteOProximoDegrau: null,
    };

    expect(deRadarSimplesDto(dto).mesesProjetadosAteOProximoDegrau).toBeNull();
  });
});
