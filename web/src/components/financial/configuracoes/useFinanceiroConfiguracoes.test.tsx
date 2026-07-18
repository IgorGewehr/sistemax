// @vitest-environment jsdom
import { act, renderHook, waitFor } from '@testing-library/react';
import type { ReactNode } from 'react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { ApiError } from '@/lib/api/client';
import type { ConfiguracaoFinanceiraDto } from '@/lib/api/financeiro';
import { ToastProvider } from '@/lib/toast';

import { useFinanceiroConfiguracoes } from './useFinanceiroConfiguracoes';

const configuracoes = vi.fn();
const salvarConfiguracoes = vi.fn();

vi.mock('@/lib/api/financeiro', () => ({
  financeiroApi: {
    configuracoes: (...args: unknown[]) => configuracoes(...args),
    salvarConfiguracoes: (...args: unknown[]) => salvarConfiguracoes(...args),
  },
}));

const CONFIG: ConfiguracaoFinanceiraDto = {
  analisePorProjetoAtiva: false,
  custoHoraPadraoCentavos: null,
  tempoEntraNoDre: false,
  imobilizadoRoiAtivo: false,
  taxaDescontoAnualBps: null,
  inicioOperacao: null,
};

function wrapper({ children }: { children: ReactNode }) {
  return <ToastProvider>{children}</ToastProvider>;
}

describe('useFinanceiroConfiguracoes', () => {
  beforeEach(() => {
    configuracoes.mockReset().mockResolvedValue(CONFIG);
    salvarConfiguracoes.mockReset();
  });

  it('começa carregando e preenche com os dois toggles desligados', async () => {
    const { result } = renderHook(() => useFinanceiroConfiguracoes(), { wrapper });

    expect(result.current.config.carregando).toBe(true);

    await waitFor(() => expect(result.current.config.carregando).toBe(false));
    expect(result.current.config.dado?.analisePorProjetoAtiva).toBe(false);
    expect(result.current.config.dado?.imobilizadoRoiAtivo).toBe(false);
  });

  it('alternar() é otimista: muda o estado na hora, antes do PUT resolver', async () => {
    let resolveSalvar!: (v: ConfiguracaoFinanceiraDto) => void;
    salvarConfiguracoes.mockReturnValue(new Promise<ConfiguracaoFinanceiraDto>((res) => (resolveSalvar = res)));

    const { result } = renderHook(() => useFinanceiroConfiguracoes(), { wrapper });
    await waitFor(() => expect(result.current.config.carregando).toBe(false));

    act(() => {
      result.current.alternar('analisePorProjetoAtiva');
    });

    // Otimista: já reflete `true` mesmo com o PUT ainda pendente.
    expect(result.current.config.dado?.analisePorProjetoAtiva).toBe(true);
    expect(result.current.salvando).toBe('analisePorProjetoAtiva');

    await act(async () => {
      resolveSalvar({ ...CONFIG, analisePorProjetoAtiva: true });
    });

    expect(result.current.config.dado?.analisePorProjetoAtiva).toBe(true);
    expect(result.current.salvando).toBeNull();
  });

  it('alternar() só inverte o campo pedido — envia a config INTEIRA no PUT (nunca patch parcial)', async () => {
    salvarConfiguracoes.mockResolvedValue({ ...CONFIG, imobilizadoRoiAtivo: true });

    const { result } = renderHook(() => useFinanceiroConfiguracoes(), { wrapper });
    await waitFor(() => expect(result.current.config.carregando).toBe(false));

    await act(async () => {
      await result.current.alternar('imobilizadoRoiAtivo');
    });

    expect(salvarConfiguracoes).toHaveBeenCalledWith({ ...CONFIG, imobilizadoRoiAtivo: true });
    expect(result.current.config.dado?.analisePorProjetoAtiva).toBe(false); // campo não tocado
    expect(result.current.config.dado?.imobilizadoRoiAtivo).toBe(true);
  });

  it('erro no PUT faz rollback pro valor anterior (não fica preso no estado otimista)', async () => {
    salvarConfiguracoes.mockRejectedValue(new ApiError('erro_interno', 'Não foi possível salvar', 500));

    const { result } = renderHook(() => useFinanceiroConfiguracoes(), { wrapper });
    await waitFor(() => expect(result.current.config.carregando).toBe(false));

    await act(async () => {
      await result.current.alternar('analisePorProjetoAtiva');
    });

    expect(result.current.config.dado?.analisePorProjetoAtiva).toBe(false); // voltou
    expect(result.current.salvando).toBeNull();
  });

  it('chamada concorrente enquanto já está salvando é ignorada (sem race de dois PUTs)', async () => {
    let resolveSalvar!: (v: ConfiguracaoFinanceiraDto) => void;
    salvarConfiguracoes.mockReturnValue(new Promise<ConfiguracaoFinanceiraDto>((res) => (resolveSalvar = res)));

    const { result } = renderHook(() => useFinanceiroConfiguracoes(), { wrapper });
    await waitFor(() => expect(result.current.config.carregando).toBe(false));

    // Primeiro clique dispara o PUT e commita `salvando` no estado (fora do act síncrono, pra o
    // React de fato flushar antes do segundo clique — é essa flush que faz o guard `if (salvando)`
    // valer). Um segundo clique só DEPOIS disso é o cenário real de duplo-clique acidental.
    act(() => {
      result.current.alternar('analisePorProjetoAtiva');
    });
    expect(result.current.salvando).toBe('analisePorProjetoAtiva');

    act(() => {
      result.current.alternar('imobilizadoRoiAtivo');
    });

    expect(salvarConfiguracoes).toHaveBeenCalledTimes(1);

    await act(async () => {
      resolveSalvar({ ...CONFIG, analisePorProjetoAtiva: true });
    });
  });

  it('recarregar() reseta para carregando e refaz o GET', async () => {
    const { result } = renderHook(() => useFinanceiroConfiguracoes(), { wrapper });
    await waitFor(() => expect(result.current.config.carregando).toBe(false));

    expect(configuracoes).toHaveBeenCalledTimes(1);

    act(() => {
      result.current.recarregar();
    });

    expect(result.current.config.carregando).toBe(true);

    await waitFor(() => expect(result.current.config.carregando).toBe(false));
    expect(configuracoes).toHaveBeenCalledTimes(2);
  });
});
