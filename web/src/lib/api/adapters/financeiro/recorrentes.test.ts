import { describe, it, expect } from 'vitest';

import type { ReceitaRecorrenteDto } from '@/lib/api/financeiro';

import { deReceitaRecorrenteDto } from './recorrentes';

function money(centavos: number) {
  return { centavos, moeda: 'BRL' };
}

const BASE_DTO: ReceitaRecorrenteDto = {
  mrr: money(500000),
  arr: money(6000000),
  assinaturasAtivas: 12,
  ticketMedio: money(41666),
  mrrNovoNoMes: money(89000),
  mrrChurnNoMes: money(34900),
  clientesChurnNoMes: 1,
  churnPercent: 0.07,
  porServico: [],
  maiorConcentracao: null,
};

describe('deReceitaRecorrenteDto', () => {
  it('mapeia os agregados 1:1 (mrr/arr/ticket médio/assinaturas ativas)', () => {
    const vm = deReceitaRecorrenteDto(BASE_DTO);

    expect(vm.mrrCentavos).toBe(500000);
    expect(vm.arrCentavos).toBe(6000000);
    expect(vm.assinaturasAtivasCount).toBe(12);
    expect(vm.ticketMedioCentavos).toBe(41666);
  });

  it('mapeia churn do mês (valor, clientes e percentual)', () => {
    const vm = deReceitaRecorrenteDto(BASE_DTO);

    expect(vm.churnMesCentavos).toBe(34900);
    expect(vm.clientesChurnNoMes).toBe(1);
    expect(vm.churnPercent).toBe(0.07);
  });

  it('maiorConcentracao nulo (sem serviços ainda) vira nome/percentual nulos, sem quebrar', () => {
    const vm = deReceitaRecorrenteDto(BASE_DTO);

    expect(vm.maiorConcentracaoNome).toBeNull();
    expect(vm.maiorConcentracaoPercentual).toBeNull();
  });

  it('maiorConcentracao presente extrai nome e percentual direto do DTO (sem lookup em array externo)', () => {
    const dto: ReceitaRecorrenteDto = {
      ...BASE_DTO,
      maiorConcentracao: { servicoId: 'servicepro', servicoNome: 'ServicePro', mrr: money(104700), percentual: 0.21 },
    };

    const vm = deReceitaRecorrenteDto(dto);

    expect(vm.maiorConcentracaoNome).toBe('ServicePro');
    expect(vm.maiorConcentracaoPercentual).toBe(0.21);
  });

  it('zeros não geram NaN nem undefined', () => {
    const dto: ReceitaRecorrenteDto = {
      ...BASE_DTO,
      mrr: money(0),
      arr: money(0),
      assinaturasAtivas: 0,
      ticketMedio: money(0),
      mrrChurnNoMes: money(0),
      clientesChurnNoMes: 0,
      churnPercent: 0,
    };

    const vm = deReceitaRecorrenteDto(dto);

    expect(vm.mrrCentavos).toBe(0);
    expect(vm.assinaturasAtivasCount).toBe(0);
    expect(Number.isFinite(vm.churnPercent)).toBe(true);
  });
});
