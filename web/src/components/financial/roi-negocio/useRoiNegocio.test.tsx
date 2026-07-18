// @vitest-environment jsdom
import { act, renderHook, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { ApiError } from '@/lib/api/client';
import type { AporteDeCapitalDto, AtivoDeCapitalDto, ConfiguracaoFinanceiraDto, RoiDoNegocioDto } from '@/lib/api/financeiro';

import { useRoiNegocio } from './useRoiNegocio';

const configuracoes = vi.fn();
const roiNegocio = vi.fn();
const imobilizado = vi.fn();
const aportes = vi.fn();

vi.mock('@/lib/api/financeiro', () => ({
  financeiroApi: {
    configuracoes: (...args: unknown[]) => configuracoes(...args),
    roiNegocio: (...args: unknown[]) => roiNegocio(...args),
    imobilizado: (...args: unknown[]) => imobilizado(...args),
    aportes: (...args: unknown[]) => aportes(...args),
  },
}));

const CONFIG_ATIVA: ConfiguracaoFinanceiraDto = {
  analisePorProjetoAtiva: false,
  custoHoraPadraoCentavos: null,
  tempoEntraNoDre: false,
  imobilizadoRoiAtivo: true,
  taxaDescontoAnualBps: null,
  inicioOperacao: null,
};

const CONFIG_DESLIGADA: ConfiguracaoFinanceiraDto = { ...CONFIG_ATIVA, imobilizadoRoiAtivo: false };

const ROI_DTO: RoiDoNegocioDto = {
  marcoInicial: '2026-01-01',
  taxaDescontoAnualBps: null,
  investimento: {
    capexCentavos: 100000,
    aportesCentavos: 0,
    totalCentavos: 100000,
    giroConsumidoObservadoCentavos: 0,
    bens: 1,
    porCategoria: [],
    resultadoAlienacaoTotalCentavos: 0,
  },
  recuperacao: { fluxoOperacionalAcumuladoCentavos: 40000, recuperadoCentavos: 40000, faltamCentavos: 60000, percentRecuperado: 40 },
  payback: { simplesRealizadoEm: null, descontadoRealizadoEm: null, projetadoMeses: 6, descontadoProjetadoMeses: null, metodo: 'simples' },
  tir: { mensalPercent: null, anualizadaPercent: null, motivoIndefinida: 'dados insuficientes' },
  roi: { caixaPercent: 40, competenciaPercent: 40, mesesAteRoiCompleto: 6 },
  serie: [],
};

const BEM: AtivoDeCapitalDto = {
  id: 'a1',
  projetoId: null,
  nome: 'Notebook',
  natureza: 'tangivel',
  categoria: 'equipamento',
  custoAquisicaoCentavos: 100000,
  valorResidualCentavos: 0,
  dataAquisicao: '2026-01-01',
  inicioDepreciacao: '2026-01-01',
  vidaUtilMeses: 36,
  quantidadeUnidades: 1,
  contaAPagarId: null,
  status: 'ativo',
  ultimaCompetenciaReconhecida: null,
  encerradoEm: null,
  baixadoEm: null,
  motivoBaixa: null,
  valorContabilAtualCentavos: 97000,
  amortizacaoMensalCentavos: 2777,
  valorVendaCentavos: null,
  resultadoAlienacaoCentavos: null,
};

const APORTE: AporteDeCapitalDto = { id: 'ap1', valorCentavos: 50000, data: '2026-01-01', descricao: 'Capital de giro', criadoEm: '2026-01-01T00:00:00Z' };

describe('useRoiNegocio', () => {
  beforeEach(() => {
    configuracoes.mockReset().mockResolvedValue(CONFIG_ATIVA);
    roiNegocio.mockReset().mockResolvedValue(ROI_DTO);
    imobilizado.mockReset().mockResolvedValue([BEM]);
    aportes.mockReset().mockResolvedValue([APORTE]);
  });

  it('começa carregando os 4 recursos e preenche ao resolver', async () => {
    const { result } = renderHook(() => useRoiNegocio());

    expect(result.current.configuracao.carregando).toBe(true);
    expect(result.current.roi.carregando).toBe(true);
    expect(result.current.imobilizado.carregando).toBe(true);
    expect(result.current.aportes.carregando).toBe(true);

    await waitFor(() => expect(result.current.roi.carregando).toBe(false));
    await waitFor(() => expect(result.current.imobilizado.carregando).toBe(false));
    await waitFor(() => expect(result.current.aportes.carregando).toBe(false));

    expect(result.current.configuracao.dado?.imobilizadoRoiAtivo).toBe(true);
    expect(result.current.roi.dado?.payback.projetadoMeses).toBe(6);
    expect(result.current.imobilizado.dado).toEqual([BEM]);
    expect(result.current.aportes.dado).toEqual([APORTE]);
  });

  it('toggle desligado: imobilizado/aportes voltam [] sem erro', async () => {
    configuracoes.mockReset().mockResolvedValue(CONFIG_DESLIGADA);
    imobilizado.mockReset().mockResolvedValue([]);
    aportes.mockReset().mockResolvedValue([]);

    const { result } = renderHook(() => useRoiNegocio());
    await waitFor(() => expect(result.current.imobilizado.carregando).toBe(false));
    await waitFor(() => expect(result.current.aportes.carregando).toBe(false));

    expect(result.current.configuracao.dado?.imobilizadoRoiAtivo).toBe(false);
    expect(result.current.imobilizado.erro).toBeNull();
    expect(result.current.imobilizado.dado).toEqual([]);
    expect(result.current.aportes.erro).toBeNull();
    expect(result.current.aportes.dado).toEqual([]);
  });

  it('roi-negocio 404 (toggle desligado no backend) NÃO vira erro — dado fica null silenciosamente', async () => {
    roiNegocio.mockReset().mockRejectedValue(new ApiError('nao_encontrado', 'Painel de ROI não encontrado', 404));

    const { result } = renderHook(() => useRoiNegocio());

    await waitFor(() => expect(result.current.roi.carregando).toBe(false));
    expect(result.current.roi.erro).toBeNull();
    expect(result.current.roi.dado).toBeNull();
  });

  it('erro não-404 do painel de ROI vira mensagem visível (painel, não listagem)', async () => {
    roiNegocio.mockReset().mockRejectedValue(new ApiError('erro_interno', 'Serviço fora do ar', 500));

    const { result } = renderHook(() => useRoiNegocio());

    await waitFor(() => expect(result.current.roi.carregando).toBe(false));
    expect(result.current.roi.erro).toBe('Serviço fora do ar');
    expect(result.current.roi.dado).toBeNull();
  });

  it('erro genérico (não ApiError) em imobilizado cai na mensagem padrão', async () => {
    imobilizado.mockReset().mockRejectedValue(new Error('boom'));

    const { result } = renderHook(() => useRoiNegocio());

    await waitFor(() => expect(result.current.imobilizado.carregando).toBe(false));
    expect(result.current.imobilizado.erro).toBe('Não foi possível carregar.');
  });

  it('um recurso falhando (aportes) não derruba os outros 3', async () => {
    aportes.mockReset().mockRejectedValue(new ApiError('erro_interno', 'Serviço fora do ar', 500));

    const { result } = renderHook(() => useRoiNegocio());

    await waitFor(() => expect(result.current.aportes.carregando).toBe(false));
    expect(result.current.aportes.erro).toBe('Serviço fora do ar');

    await waitFor(() => expect(result.current.roi.carregando).toBe(false));
    expect(result.current.roi.erro).toBeNull();
    expect(result.current.roi.dado).not.toBeNull();
  });

  it('recarregar() reseta os 4 recursos para "carregando" e refaz as chamadas', async () => {
    const { result } = renderHook(() => useRoiNegocio());
    await waitFor(() => expect(result.current.aportes.carregando).toBe(false));

    expect(configuracoes).toHaveBeenCalledTimes(1);

    act(() => {
      result.current.recarregar();
    });

    expect(result.current.configuracao.carregando).toBe(true);
    expect(result.current.roi.dado).toBeNull();

    await waitFor(() => expect(result.current.aportes.carregando).toBe(false));
    expect(configuracoes).toHaveBeenCalledTimes(2);
  });
});
