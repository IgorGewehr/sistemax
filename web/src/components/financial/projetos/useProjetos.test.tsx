// @vitest-environment jsdom
import { act, renderHook, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { ApiError } from '@/lib/api/client';
import type { ConfiguracaoFinanceiraDto, PainelDoProjetoDto, ProjetoDto } from '@/lib/api/financeiro';

import { useProjetos } from './useProjetos';

const configuracoes = vi.fn();
const projetos = vi.fn();
const projetoPainel = vi.fn();

vi.mock('@/lib/api/financeiro', () => ({
  financeiroApi: {
    configuracoes: (...args: unknown[]) => configuracoes(...args),
    projetos: (...args: unknown[]) => projetos(...args),
    projetoPainel: (...args: unknown[]) => projetoPainel(...args),
  },
}));

function money(centavos: number) {
  return { centavos, moeda: 'BRL' };
}

const CONFIG_ATIVA: ConfiguracaoFinanceiraDto = {
  analisePorProjetoAtiva: true,
  custoHoraPadraoCentavos: null,
  tempoEntraNoDre: false,
  imobilizadoRoiAtivo: false,
  taxaDescontoAnualBps: null,
  inicioOperacao: null,
};

const CONFIG_DESLIGADA: ConfiguracaoFinanceiraDto = { ...CONFIG_ATIVA, analisePorProjetoAtiva: false };

function projeto(overrides: Partial<ProjetoDto> = {}): ProjetoDto {
  return {
    id: 'p1',
    nome: 'Consultoria',
    descricao: null,
    status: 'ativo',
    criadoEm: '2026-01-01T00:00:00Z',
    arquivadoEm: null,
    ...overrides,
  };
}

function painel(overrides: Partial<PainelDoProjetoDto> = {}): PainelDoProjetoDto {
  return {
    projeto: projeto(),
    receita: { mrr: money(500000), arr: money(6000000), assinaturasAtivas: 3, ticketMedio: money(166666) },
    churn: { cancelamentos12m: 0, exposicaoAssinaturaMeses12m: 12, churnMensalPercent: 0, vidaEsperadaMeses: null },
    ltv: { ltv: null, limiteInferior: money(0), metodo: 'observado', observacao: null },
    margem: {
      competencia: '2026-07-01',
      receita: money(500000),
      custoDireto: money(100000),
      mc1: money(400000),
      mc1Percent: 80,
      amortizacaoMes: money(0),
      mc2: money(400000),
      mc2Percent: 80,
      custoTempoMes: null,
      mc3: null,
      mc3Percent: null,
    },
    capacidade: { unidadesTotais: 0, unidadesUtilizadas: 0, utilizacaoPercent: 0, custoOciosidadeMesCentavos: 0 },
    payback: { investimentoTotalCentavos: 0, fluxoCaixaAcumuladoCentavos: 0, paybackRealizadoEm: null, paybackProjetadoMeses: null, metodo: 'simples' },
    roi: { realizadoPercent: null, roiSobreInvestimentoPercent: null, runRateAnualizadoPercent: null },
    tempo: { minutosJanela: 0, custoJanelaCentavos: null, porCliente: [] },
    ...overrides,
  };
}

describe('useProjetos', () => {
  beforeEach(() => {
    configuracoes.mockReset().mockResolvedValue(CONFIG_ATIVA);
    projetos.mockReset().mockResolvedValue([]);
    projetoPainel.mockReset().mockResolvedValue(painel());
  });

  it('começa carregando e preenche configuracao/projetos ao resolver', async () => {
    const { result } = renderHook(() => useProjetos());

    expect(result.current.configuracao.carregando).toBe(true);
    expect(result.current.projetos.carregando).toBe(true);

    await waitFor(() => expect(result.current.configuracao.carregando).toBe(false));
    await waitFor(() => expect(result.current.projetos.carregando).toBe(false));

    expect(result.current.configuracao.dado?.analisePorProjetoAtiva).toBe(true);
    expect(result.current.projetos.dado).toEqual([]);
  });

  it('toggle desligado: projetos volta [] sem erro (nunca dado fabricado)', async () => {
    configuracoes.mockReset().mockResolvedValue(CONFIG_DESLIGADA);
    projetos.mockReset().mockResolvedValue([]);

    const { result } = renderHook(() => useProjetos());
    await waitFor(() => expect(result.current.configuracao.carregando).toBe(false));

    expect(result.current.configuracao.dado?.analisePorProjetoAtiva).toBe(false);
    expect(result.current.projetos.erro).toBeNull();
    expect(result.current.projetos.dado).toEqual([]);
  });

  it('seleciona automaticamente o primeiro projeto assim que a lista chega', async () => {
    const p1 = projeto({ id: 'p1', nome: 'Consultoria' });
    const p2 = projeto({ id: 'p2', nome: 'SaaS' });
    projetos.mockReset().mockResolvedValue([p1, p2]);

    const { result } = renderHook(() => useProjetos());

    await waitFor(() => expect(result.current.selecionadoId).toBe('p1'));
  });

  it('busca o painel de CADA projeto listado e expõe painelAtivo do selecionado', async () => {
    const p1 = projeto({ id: 'p1' });
    const p2 = projeto({ id: 'p2' });
    projetos.mockReset().mockResolvedValue([p1, p2]);
    projetoPainel.mockReset().mockImplementation((id: string) => Promise.resolve(painel({ projeto: projeto({ id }) })));

    const { result } = renderHook(() => useProjetos());

    await waitFor(() => expect(result.current.paineis['p1']?.carregando).toBe(false));
    await waitFor(() => expect(result.current.paineis['p2']?.carregando).toBe(false));

    expect(projetoPainel).toHaveBeenCalledWith('p1');
    expect(projetoPainel).toHaveBeenCalledWith('p2');
    expect(result.current.painelAtivo.dado?.projeto.id).toBe('p1');
  });

  it('selecionar() troca o painelAtivo para o projeto escolhido', async () => {
    const p1 = projeto({ id: 'p1' });
    const p2 = projeto({ id: 'p2' });
    projetos.mockReset().mockResolvedValue([p1, p2]);
    projetoPainel.mockReset().mockImplementation((id: string) => Promise.resolve(painel({ projeto: projeto({ id }) })));

    const { result } = renderHook(() => useProjetos());
    await waitFor(() => expect(result.current.selecionadoId).toBe('p1'));
    await waitFor(() => expect(result.current.paineis['p2']?.carregando).toBe(false));

    act(() => {
      result.current.selecionar('p2');
    });

    expect(result.current.selecionadoId).toBe('p2');
    expect(result.current.painelAtivo.dado?.projeto.id).toBe('p2');
  });

  it('erro da API de configuração vira mensagem no estado, sem quebrar o hook', async () => {
    configuracoes.mockReset().mockRejectedValue(new ApiError('erro_interno', 'Serviço fora do ar', 500));

    const { result } = renderHook(() => useProjetos());

    await waitFor(() => expect(result.current.configuracao.carregando).toBe(false));
    expect(result.current.configuracao.erro).toBe('Serviço fora do ar');
    expect(result.current.configuracao.dado).toBeNull();
  });

  it('erro genérico (não ApiError) na lista de projetos cai na mensagem padrão', async () => {
    projetos.mockReset().mockRejectedValue(new Error('boom'));

    const { result } = renderHook(() => useProjetos());

    await waitFor(() => expect(result.current.projetos.carregando).toBe(false));
    expect(result.current.projetos.erro).toBe('Não foi possível carregar.');
  });

  it('recarregar() reseta tudo (config, projetos, seleção, painéis) e refaz as chamadas', async () => {
    const p1 = projeto({ id: 'p1' });
    projetos.mockReset().mockResolvedValue([p1]);

    const { result } = renderHook(() => useProjetos());
    await waitFor(() => expect(result.current.selecionadoId).toBe('p1'));
    await waitFor(() => expect(result.current.paineis['p1']?.carregando).toBe(false));

    expect(configuracoes).toHaveBeenCalledTimes(1);

    act(() => {
      result.current.recarregar();
    });

    expect(result.current.configuracao.carregando).toBe(true);
    expect(result.current.selecionadoId).toBeNull();
    expect(result.current.paineis).toEqual({});

    await waitFor(() => expect(result.current.selecionadoId).toBe('p1'));
    expect(configuracoes).toHaveBeenCalledTimes(2);
  });
});
