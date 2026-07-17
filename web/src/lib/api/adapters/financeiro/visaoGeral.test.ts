import { describe, it, expect } from 'vitest';

import type { DisponivelParaRetiradaDto, FluxoDeCaixaDto } from '@/lib/api/financeiro';

import { deDisponivelDto, deTimelineDto } from './visaoGeral';

function money(centavos: number) {
  return { centavos, moeda: 'BRL' };
}

describe('deDisponivelDto', () => {
  it('mapeia saldo/já-tem-dono/pode-tirar do DTO para o view-model, com jaTemDono negativo', () => {
    const dto: DisponivelParaRetiradaDto = {
      saldoEmCaixa: money(500000),
      jaTemDono: money(120000),
      podeTirar: money(380000),
    };

    const vm = deDisponivelDto(dto);

    expect(vm.livreDeVerdadeCentavos).toBe(380000);
    expect(vm.noBancoEGaveta.valorCentavos).toBe(500000);
    expect(vm.noBancoEGaveta.tone).toBe('pos');
    expect(vm.jaTemDono.valorCentavos).toBe(-120000); // sinal invertido — é uma dedução
    expect(vm.jaTemDono.tone).toBe('crit');
  });

  it('drills apontam para as rotas fixas (Bancário e Entradas & Saídas)', () => {
    const dto: DisponivelParaRetiradaDto = { saldoEmCaixa: money(0), jaTemDono: money(0), podeTirar: money(0) };
    const vm = deDisponivelDto(dto);

    expect(vm.noBancoEGaveta.drill.rota).toBe('/financeiro/bancario');
    expect(vm.jaTemDono.drill.rota).toBe('/financeiro/entradas-saidas');
  });

  it('valores zerados não geram sinal nem NaN', () => {
    const dto: DisponivelParaRetiradaDto = { saldoEmCaixa: money(0), jaTemDono: money(0), podeTirar: money(0) };
    const vm = deDisponivelDto(dto);

    expect(vm.livreDeVerdadeCentavos).toBe(0);
    expect(vm.jaTemDono.valorCentavos === 0).toBe(true); // aceita -0 (JS: -0 === 0), nunca "-R$0,00" na UI
    expect(Number.isFinite(vm.jaTemDono.valorCentavos)).toBe(true);
  });
});

function ponto(data: string, saldoAcumulado: number, projetado: boolean) {
  return { data, entradas: money(0), saidas: money(0), saldoAcumulado: money(saldoAcumulado), projetado };
}

describe('deTimelineDto', () => {
  it('hojeIndex é o índice do último ponto NÃO projetado (realizado)', () => {
    const dto: FluxoDeCaixaDto = {
      primeiroDiaNegativo: null,
      pontos: [
        ponto('2026-07-10', 1000, false),
        ponto('2026-07-11', 1100, false),
        ponto('2026-07-12', 1200, true),
        ponto('2026-07-13', 1300, true),
      ],
    };

    const vm = deTimelineDto(dto);

    expect(vm.hojeIndex).toBe(1);
    expect(vm.valoresDiarios).toEqual([1000, 1100, 1200, 1300]);
    expect(vm.datasISO).toEqual(['2026-07-10', '2026-07-11', '2026-07-12', '2026-07-13']);
  });

  it('todos os pontos projetados: hojeIndex cai no valor inicial (0), nenhum é marcado realizado', () => {
    const dto: FluxoDeCaixaDto = {
      primeiroDiaNegativo: null,
      pontos: [ponto('2026-07-10', 1000, true), ponto('2026-07-11', 1100, true)],
    };

    expect(deTimelineDto(dto).hojeIndex).toBe(0);
  });

  it('mesLabel extrai o mês (MM) da data de hojeIndex', () => {
    const dto: FluxoDeCaixaDto = {
      primeiroDiaNegativo: null,
      pontos: [ponto('2026-07-10', 1000, false), ponto('2026-08-01', 900, true)],
    };

    expect(deTimelineDto(dto).mesLabel).toBe('07');
  });

  it('eventosPorDia fica sempre vazio — read-model de origem por dia ainda não existe', () => {
    const dto: FluxoDeCaixaDto = { primeiroDiaNegativo: null, pontos: [ponto('2026-07-10', 1000, false)] };
    expect(deTimelineDto(dto).eventosPorDia).toEqual({});
  });

  it('lista de pontos vazia não quebra: hojeIndex 0, arrays vazios, mesLabel vazio', () => {
    const dto: FluxoDeCaixaDto = { primeiroDiaNegativo: null, pontos: [] };
    const vm = deTimelineDto(dto);

    expect(vm.hojeIndex).toBe(0);
    expect(vm.valoresDiarios).toEqual([]);
    expect(vm.datasISO).toEqual([]);
    expect(vm.mesLabel).toBe('');
  });
});
