import { describe, it, expect } from 'vitest';

import type { ConsultorInsightDto } from '@/lib/api/financeiro';

import { deConsultorDtos } from './consultor';

function insight(over: Partial<ConsultorInsightDto> = {}): ConsultorInsightDto {
  return {
    modulo: 'financeiro',
    ruleId: 'fin.inadimplencia',
    tela: 'entradas-saidas',
    score: 4200,
    frase: 'Da sua carteira de R$ 10.000 a receber, a expectativa é perder cerca de R$ 800.',
    origem: 0,
    facts: { valorEmAberto: 'R$ 10.000,00', provisaoEsperada: 'R$ 800,00' },
    drill: { tela: 'entradas-saidas' },
    ...over,
  };
}

describe('deConsultorDtos', () => {
  it('preserva a ordem (o backend já rankeou) e monta id a partir de modulo:ruleId', () => {
    const vm = deConsultorDtos([
      insight({ ruleId: 'fin.inadimplencia' }),
      insight({ ruleId: 'fin.runway', tela: 'visao-geral', drill: { tela: 'visao-geral' } }),
    ]);

    expect(vm.insights).toHaveLength(2);
    expect(vm.insights[0].id).toBe('financeiro:fin.inadimplencia');
    expect(vm.insights[1].id).toBe('financeiro:fin.runway');
  });

  it('humaniza as chaves conhecidas de facts e mantém o valor já formatado do servidor', () => {
    const vm = deConsultorDtos([insight()]);

    expect(vm.insights[0].fatos).toEqual([
      { label: 'Valor em aberto', valor: 'R$ 10.000,00' },
      { label: 'Perda esperada por atraso', valor: 'R$ 800,00' },
    ]);
  });

  it('chave de fact desconhecida cai no próprio identificador (nunca some)', () => {
    const vm = deConsultorDtos([insight({ facts: { chaveNova: 'valor X' } })]);
    expect(vm.insights[0].fatos).toEqual([{ label: 'chaveNova', valor: 'valor X' }]);
  });

  it('mapeia o slot de tela pra rota real do Financeiro (fluxo-caixa → fluxo-de-caixa)', () => {
    const vm = deConsultorDtos([insight({ tela: 'fluxo-caixa', drill: { tela: 'fluxo-caixa' } })]);
    expect(vm.insights[0].drill?.rota).toBe('/financeiro/fluxo-de-caixa');
  });

  it('slot visao-geral (a própria tela) ou desconhecido não vira drill', () => {
    const naPropriaTela = deConsultorDtos([insight({ tela: 'visao-geral', drill: { tela: 'visao-geral' } })]);
    const desconhecido = deConsultorDtos([insight({ tela: 'inexistente', drill: { tela: 'inexistente' } })]);

    expect(naPropriaTela.insights[0].drill).toBeNull();
    expect(desconhecido.insights[0].drill).toBeNull();
  });

  it('drill nulo cai no slot da tela do insight', () => {
    const vm = deConsultorDtos([insight({ tela: 'bancario', drill: null })]);
    expect(vm.insights[0].drill?.rota).toBe('/financeiro/bancario');
  });

  it('lista vazia devolve insights vazio (o consultor sempre pode ficar sem card)', () => {
    expect(deConsultorDtos([]).insights).toEqual([]);
  });
});
