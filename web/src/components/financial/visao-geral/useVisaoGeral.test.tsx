// @vitest-environment jsdom
import { act, renderHook, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { ApiError } from '@/lib/api/client';
import type {
  ConsultorInsightDto,
  ContasEmAbertoDto,
  DisponivelParaRetiradaDto,
  DreDto,
  ExtratoDto,
  FluxoDeCaixaDto,
  InadimplenciaDto,
  PontoDeEquilibrioDto,
  PrevisaoDeCaixaDto,
  RadarDoSimplesDto,
} from '@/lib/api/financeiro';

import { useVisaoGeral } from './useVisaoGeral';

const disponivelParaRetirada = vi.fn();
const fluxo = vi.fn();
const relatoriosDre = vi.fn();
const relatoriosContasEmAberto = vi.fn();
const extrato = vi.fn();
const previsaoCaixa = vi.fn();
const pontoEquilibrio = vi.fn();
const inadimplencia = vi.fn();
const radarSimples = vi.fn();
const consultor = vi.fn();

vi.mock('@/lib/api/financeiro', () => ({
  financeiroApi: {
    disponivelParaRetirada: (...args: unknown[]) => disponivelParaRetirada(...args),
    fluxo: (...args: unknown[]) => fluxo(...args),
    relatoriosDre: (...args: unknown[]) => relatoriosDre(...args),
    relatoriosContasEmAberto: (...args: unknown[]) => relatoriosContasEmAberto(...args),
    extrato: (...args: unknown[]) => extrato(...args),
    previsaoCaixa: (...args: unknown[]) => previsaoCaixa(...args),
    pontoEquilibrio: (...args: unknown[]) => pontoEquilibrio(...args),
    inadimplencia: (...args: unknown[]) => inadimplencia(...args),
    radarSimples: (...args: unknown[]) => radarSimples(...args),
    consultor: (...args: unknown[]) => consultor(...args),
  },
}));

function money(centavos: number) {
  return { centavos, moeda: 'BRL' };
}

const DISPONIVEL_DTO: DisponivelParaRetiradaDto = {
  saldoEmCaixa: money(500000),
  jaTemDono: money(100000),
  podeTirar: money(400000),
};

const FLUXO_DTO: FluxoDeCaixaDto = {
  primeiroDiaNegativo: null,
  pontos: [{ data: '2026-07-10', entradas: money(0), saidas: money(0), saldoAcumulado: money(1000), projetado: false }],
};

const PREVISAO_DTO: PrevisaoDeCaixaDto = {
  bandas: [],
  probabilidadeSaldoNegativoEm30Dias: 0.1,
  primeiroDiaP50Negativo: null,
  diasRunwayBruto: 90,
  diasRunwayRealista: 60,
};

const BREAKEVEN_DTO: PontoDeEquilibrioDto = {
  custosFixosMensaisCentavos: 100000,
  margemContribuicaoPercentual: 0.5,
  receitaNecessariaMensalCentavos: 200000,
  receitaNecessariaDiariaCentavos: 6666,
  receitaAcumuladaNoMesCentavos: 100000,
  diaDoEquilibrio: 15,
  jaAtingiuNoMes: false,
};

const INADIMPLENCIA_DTO: InadimplenciaDto = {
  valorTotalEmAbertoCentavos: 0,
  provisaoEsperadaCentavos: 0,
  valorLiquidoEsperadoCentavos: 0,
  porFaixa: [],
};

const RADAR_DTO: RadarDoSimplesDto = {
  rbt12Centavos: 0,
  faixaAtual: 1,
  aliquotaEfetiva: 0.04,
  aliquotaNominalFaixaAtual: 0.06,
  distanciaAoProximoDegrauCentavos: 0,
  mesesProjetadosAteOProximoDegrau: null,
};

const DRE_ATUAL_DTO: DreDto = {
  receitaBruta: money(1000000),
  custoDireto: money(400000),
  despesaOperacional: money(300000),
  resultadoOperacional: money(300000),
};

const DRE_ANTERIOR_DTO: DreDto = {
  receitaBruta: money(900000),
  custoDireto: money(380000),
  despesaOperacional: money(280000),
  resultadoOperacional: money(240000),
};

const CONTAS_EM_ABERTO_DTO: ContasEmAbertoDto = {
  receberEmAberto: money(500000),
  receberAtrasado: money(100000),
  pagarEmAberto: money(300000),
  agingBuckets: [],
};

const EXTRATO_DTO: ExtratoDto = {
  linhas: [
    {
      id: 'l1',
      data: '2026-07-18',
      descricao: 'Mercado São João',
      categoriaId: 'servicos',
      tipo: 'entrada',
      status: 'previsto',
      valor: money(89000),
    },
  ],
  kpis: { totalEntradas: money(89000), totalSaidas: money(0), saldoPeriodo: money(89000) },
};

const CONSULTOR_DTO: ConsultorInsightDto[] = [
  {
    modulo: 'financeiro',
    ruleId: 'fin.inadimplencia',
    tela: 'entradas-saidas',
    score: 4200,
    frase: 'Da sua carteira de R$ 10.000 a receber, a expectativa é perder cerca de R$ 800.',
    origem: 0,
    facts: { valorEmAberto: 'R$ 10.000,00', provisaoEsperada: 'R$ 800,00' },
    drill: { tela: 'entradas-saidas' },
  },
  {
    modulo: 'financeiro',
    ruleId: 'fin.runway',
    tela: 'visao-geral',
    score: 100,
    frase: 'No ritmo atual, seu caixa aguenta cerca de 40 dias sem novas vendas.',
    origem: 0,
    facts: { runwayDias: '40', runwayOrigem: 'realista' },
    drill: { tela: 'visao-geral' },
  },
];

describe('useVisaoGeral', () => {
  beforeEach(() => {
    disponivelParaRetirada.mockReset().mockResolvedValue(DISPONIVEL_DTO);
    fluxo.mockReset().mockResolvedValue(FLUXO_DTO);
    relatoriosDre.mockReset().mockResolvedValueOnce(DRE_ATUAL_DTO).mockResolvedValue(DRE_ANTERIOR_DTO);
    relatoriosContasEmAberto.mockReset().mockResolvedValue(CONTAS_EM_ABERTO_DTO);
    extrato.mockReset().mockResolvedValue(EXTRATO_DTO);
    previsaoCaixa.mockReset().mockResolvedValue(PREVISAO_DTO);
    pontoEquilibrio.mockReset().mockResolvedValue(BREAKEVEN_DTO);
    inadimplencia.mockReset().mockResolvedValue(INADIMPLENCIA_DTO);
    radarSimples.mockReset().mockResolvedValue(RADAR_DTO);
    consultor.mockReset().mockResolvedValue(CONSULTOR_DTO);
  });

  it('começa carregando todos os recursos e preenche com os adapters ao resolver', async () => {
    const { result } = renderHook(() => useVisaoGeral());

    expect(result.current.disponivel.carregando).toBe(true);
    expect(result.current.timeline.carregando).toBe(true);
    expect(result.current.lucroDoMes.carregando).toBe(true);
    expect(result.current.proximosVencimentos.carregando).toBe(true);
    expect(result.current.sobrevivencia.runway.carregando).toBe(true);
    expect(result.current.consultor.carregando).toBe(true);

    await waitFor(() => expect(result.current.disponivel.carregando).toBe(false));
    await waitFor(() => expect(result.current.timeline.carregando).toBe(false));
    await waitFor(() => expect(result.current.lucroDoMes.carregando).toBe(false));
    await waitFor(() => expect(result.current.proximosVencimentos.carregando).toBe(false));
    await waitFor(() => expect(result.current.sobrevivencia.runway.carregando).toBe(false));
    await waitFor(() => expect(result.current.sobrevivencia.breakeven.carregando).toBe(false));
    await waitFor(() => expect(result.current.sobrevivencia.inadimplencia.carregando).toBe(false));
    await waitFor(() => expect(result.current.sobrevivencia.radarSimples.carregando).toBe(false));
    await waitFor(() => expect(result.current.consultor.carregando).toBe(false));

    expect(result.current.disponivel.dado?.livreDeVerdadeCentavos).toBe(400000);
    expect(result.current.disponivel.erro).toBeNull();
    expect(result.current.timeline.dado?.valoresDiarios).toEqual([1000]);
    expect(result.current.lucroDoMes.dado?.lucroCentavos).toBe(300000);
    expect(result.current.lucroDoMes.dado?.aReceberCentavos).toBe(500000);
    expect(result.current.proximosVencimentos.dado).toHaveLength(1);
    expect(result.current.proximosVencimentos.dado?.[0].descricao).toBe('Mercado São João');
    expect(result.current.sobrevivencia.runway.dado?.diasRunwayRealista).toBe(60);
    expect(result.current.sobrevivencia.breakeven.dado?.progressoPercentual).toBe(50);
    expect(result.current.consultor.dado?.insights).toHaveLength(2);
    expect(result.current.consultor.dado?.insights[0].frase).toContain('carteira de R$ 10.000');
    // Insight de `entradas-saidas` mapeia pra rota real; `visao-geral` (a própria tela) fica sem drill.
    expect(result.current.consultor.dado?.insights[0].drill?.rota).toBe('/financeiro/entradas-saidas');
    expect(result.current.consultor.dado?.insights[1].drill).toBeNull();
  });

  it('lucroDoMes/proximosVencimentos agora vêm da rede (extrato + relatorios/dre + contas-em-aberto)', async () => {
    const { result } = renderHook(() => useVisaoGeral());
    await waitFor(() => expect(result.current.lucroDoMes.carregando).toBe(false));
    await waitFor(() => expect(result.current.proximosVencimentos.carregando).toBe(false));

    expect(result.current.lucroDoMes.erro).toBeNull();
    expect(result.current.lucroDoMes.dado).not.toBeNull();
    expect(result.current.proximosVencimentos.erro).toBeNull();
    expect(relatoriosDre).toHaveBeenCalledTimes(2);
    expect(relatoriosContasEmAberto).toHaveBeenCalledTimes(1);
    expect(extrato).toHaveBeenCalledTimes(1);
  });

  it('consultor fora do ar não derruba os outros blocos e cai numa mensagem de erro', async () => {
    consultor.mockReset().mockRejectedValue(new ApiError('erro_interno', 'Consultor fora do ar', 500));

    const { result } = renderHook(() => useVisaoGeral());

    await waitFor(() => expect(result.current.consultor.carregando).toBe(false));
    expect(result.current.consultor.erro).toBe('Consultor fora do ar');
    expect(result.current.consultor.dado).toBeNull();

    await waitFor(() => expect(result.current.disponivel.carregando).toBe(false));
    expect(result.current.disponivel.erro).toBeNull();
    expect(result.current.disponivel.dado).not.toBeNull();
  });

  it('um bloco falhando (ex.: inadimplência fora do ar) não derruba os outros 5', async () => {
    inadimplencia.mockReset().mockRejectedValue(new ApiError('erro_interno', 'Serviço fora do ar', 500));

    const { result } = renderHook(() => useVisaoGeral());

    await waitFor(() => expect(result.current.sobrevivencia.inadimplencia.carregando).toBe(false));
    expect(result.current.sobrevivencia.inadimplencia.erro).toBe('Serviço fora do ar');
    expect(result.current.sobrevivencia.inadimplencia.dado).toBeNull();

    await waitFor(() => expect(result.current.disponivel.carregando).toBe(false));
    expect(result.current.disponivel.erro).toBeNull();
    expect(result.current.disponivel.dado).not.toBeNull();

    await waitFor(() => expect(result.current.sobrevivencia.runway.carregando).toBe(false));
    expect(result.current.sobrevivencia.runway.erro).toBeNull();
  });

  it('erro genérico (não ApiError) cai na mensagem padrão', async () => {
    disponivelParaRetirada.mockReset().mockRejectedValue(new Error('boom'));

    const { result } = renderHook(() => useVisaoGeral());

    await waitFor(() => expect(result.current.disponivel.carregando).toBe(false));
    expect(result.current.disponivel.erro).toBe('Não foi possível carregar.');
  });

  it('recarregar() reseta todos os recursos para "carregando" e refaz as 6 chamadas', async () => {
    const { result } = renderHook(() => useVisaoGeral());
    await waitFor(() => expect(result.current.disponivel.carregando).toBe(false));

    expect(disponivelParaRetirada).toHaveBeenCalledTimes(1);

    act(() => {
      result.current.recarregar();
    });

    expect(result.current.disponivel.carregando).toBe(true);
    expect(result.current.disponivel.dado).toBeNull();

    await waitFor(() => expect(result.current.disponivel.carregando).toBe(false));
    expect(disponivelParaRetirada).toHaveBeenCalledTimes(2);
  });
});
