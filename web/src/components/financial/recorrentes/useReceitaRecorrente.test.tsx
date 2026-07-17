// @vitest-environment jsdom
import { act, renderHook, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { ApiError } from '@/lib/api/client';
import type { ReceitaRecorrenteDto } from '@/lib/api/financeiro';

import { useReceitaRecorrente } from './useReceitaRecorrente';

const receitaRecorrente = vi.fn();

vi.mock('@/lib/api/financeiro', () => ({
  financeiroApi: {
    receitaRecorrente: (...args: unknown[]) => receitaRecorrente(...args),
  },
}));

function money(centavos: number) {
  return { centavos, moeda: 'BRL' };
}

const DTO: ReceitaRecorrenteDto = {
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

describe('useReceitaRecorrente', () => {
  beforeEach(() => {
    receitaRecorrente.mockReset().mockResolvedValue(DTO);
  });

  it('começa carregando e preenche com o adapter ao resolver', async () => {
    const { result } = renderHook(() => useReceitaRecorrente());

    expect(result.current.carregando).toBe(true);
    expect(result.current.dado).toBeNull();

    await waitFor(() => expect(result.current.carregando).toBe(false));

    expect(result.current.erro).toBeNull();
    expect(result.current.dado?.mrrCentavos).toBe(500000);
    expect(result.current.dado?.assinaturasAtivasCount).toBe(12);
  });

  it('erro da API vira mensagem no estado, sem quebrar o hook', async () => {
    receitaRecorrente.mockReset().mockRejectedValue(new ApiError('erro_interno', 'Serviço fora do ar', 500));

    const { result } = renderHook(() => useReceitaRecorrente());

    await waitFor(() => expect(result.current.carregando).toBe(false));
    expect(result.current.erro).toBe('Serviço fora do ar');
    expect(result.current.dado).toBeNull();
  });

  it('erro genérico (não ApiError) cai na mensagem padrão', async () => {
    receitaRecorrente.mockReset().mockRejectedValue(new Error('boom'));

    const { result } = renderHook(() => useReceitaRecorrente());

    await waitFor(() => expect(result.current.carregando).toBe(false));
    expect(result.current.erro).toBe('Não foi possível carregar.');
  });

  it('recarregar() reseta para carregando e refaz a chamada', async () => {
    const { result } = renderHook(() => useReceitaRecorrente());
    await waitFor(() => expect(result.current.carregando).toBe(false));

    expect(receitaRecorrente).toHaveBeenCalledTimes(1);

    act(() => {
      result.current.recarregar();
    });

    expect(result.current.carregando).toBe(true);
    expect(result.current.dado).toBeNull();

    await waitFor(() => expect(result.current.carregando).toBe(false));
    expect(receitaRecorrente).toHaveBeenCalledTimes(2);
  });
});
