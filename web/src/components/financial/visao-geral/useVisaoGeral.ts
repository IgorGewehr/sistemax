import { useCallback, useEffect, useState } from 'react';

import {
  deAbertoResumoDto,
  deDreResumoDto,
  deGaugeDto,
  deInvestimentoDto,
  deSimplesDto,
  deTileAssinaturasDto,
  deTimelineDto,
} from '@/lib/api/adapters/financeiro/visaoGeral';
import { calcularKpisAberto, deExtratoLinhas } from '@/lib/api/adapters/financeiro/entradasSaidas';
import { ApiError } from '@/lib/api/client';
import { financeiroApi, type ConfiguracaoFinanceiraDto } from '@/lib/api/financeiro';
import { addDays, endOfMonthIso, startOfMonthIso, todayIso } from '@/lib/date';

import type {
  AbertoResumoViewModel,
  DreResumoViewModel,
  GaugeViewModel,
  InvestimentoViewModel,
  SimplesViewModel,
  TileAssinaturasViewModel,
  TimelineViewModel,
} from './types';

export interface Recurso<T> {
  dado: T | null;
  erro: string | null;
  carregando: boolean;
}

function inicial<T>(): Recurso<T> {
  return { dado: null, erro: null, carregando: true };
}

function mensagemDeErro(e: unknown): string {
  return e instanceof ApiError ? e.message : 'Não foi possível carregar.';
}

/** "Tudo em aberto" — mesmo horizonte largo (10 anos pra frente, desde 2015) já usado pelos KPIs
 * REAIS de Entradas & Saídas (`useEntradasSaidas.ts`) — reusar garante que "A receber"/"A pagar"
 * aqui batem com os mesmos números que o usuário vê ao dar drill nessa tela. */
const HORIZONTE_ABERTO_DE = '2015-01-01';
function horizonteAbertoAte(): string {
  return addDays(todayIso(), 365 * 10);
}

const MESES_PT_MIN = [
  'janeiro', 'fevereiro', 'março', 'abril', 'maio', 'junho',
  'julho', 'agosto', 'setembro', 'outubro', 'novembro', 'dezembro',
];

/** "2026-07-18" → "Julho 2026" — mesmo rótulo do pill de período do `PageHeader`. */
function periodoLabelDeIso(iso: string): string {
  const nome = MESES_PT_MIN[Number(iso.slice(5, 7)) - 1] ?? iso.slice(5, 7);
  return `${nome.charAt(0).toUpperCase()}${nome.slice(1)} ${iso.slice(0, 4)}`;
}

/**
 * Todo o estado de dado REAL da Visão Geral v3 vive aqui — um `Recurso<T>` por bloco (mesmo padrão
 * de `useEstoque`/`useEntradasSaidas`): um endpoint fora do ar não derruba os outros. `gauge` junta
 * `previsao-caixa` + `disponivel-retirada` (viram UM card no mockup); `abertoResumo` junta os dois
 * tiles "A receber"/"A pagar" (mesma chamada de extrato); `dre` junta o tile "Resultado" e o mix
 * das 3 correntes (mesma chamada `relatorios/dre`). `investimento` só é buscado quando
 * `configuracao.imobilizadoRoiAtivo` está ligado — desligado, o card "some elegante" (mesmo
 * comportamento do toggle de demo do mockup), sem 404 ruidoso no console.
 */
export function useVisaoGeral() {
  const [gauge, setGauge] = useState<Recurso<GaugeViewModel>>(inicial);
  const [timeline, setTimeline] = useState<Recurso<TimelineViewModel>>(inicial);
  const [abertoResumo, setAbertoResumo] = useState<Recurso<AbertoResumoViewModel>>(inicial);
  const [dre, setDre] = useState<Recurso<DreResumoViewModel>>(inicial);
  const [recorrente, setRecorrente] = useState<Recurso<TileAssinaturasViewModel>>(inicial);
  const [radar, setRadar] = useState<Recurso<SimplesViewModel>>(inicial);
  const [configuracao, setConfiguracao] = useState<Recurso<ConfiguracaoFinanceiraDto>>(inicial);
  const [investimento, setInvestimento] = useState<Recurso<InvestimentoViewModel>>(inicial);

  const carregar = useCallback(() => {
    setGauge(inicial());
    setTimeline(inicial());
    setAbertoResumo(inicial());
    setDre(inicial());
    setRecorrente(inicial());
    setRadar(inicial());
    setConfiguracao(inicial());
    setInvestimento(inicial());

    Promise.all([financeiroApi.previsaoCaixa(), financeiroApi.disponivelParaRetirada()])
      .then(([previsaoDto, disponivelDto]) => setGauge({ dado: deGaugeDto(previsaoDto, disponivelDto), erro: null, carregando: false }))
      .catch((e: unknown) => setGauge({ dado: null, erro: mensagemDeErro(e), carregando: false }));

    financeiroApi
      .fluxo(14, 30)
      .then((dto) => setTimeline({ dado: deTimelineDto(dto), erro: null, carregando: false }))
      .catch((e: unknown) => setTimeline({ dado: null, erro: mensagemDeErro(e), carregando: false }));

    const hoje = todayIso();
    financeiroApi
      .extrato(HORIZONTE_ABERTO_DE, horizonteAbertoAte())
      .then((dto) => {
        const linhas = deExtratoLinhas(dto.linhas);
        setAbertoResumo({ dado: deAbertoResumoDto(calcularKpisAberto(linhas), linhas, hoje), erro: null, carregando: false });
      })
      .catch((e: unknown) => setAbertoResumo({ dado: null, erro: mensagemDeErro(e), carregando: false }));

    const mesAtual = { de: startOfMonthIso(hoje), ate: endOfMonthIso(hoje) };
    const fimDoMesAnterior = addDays(mesAtual.de, -1);
    const mesAnterior = { de: startOfMonthIso(fimDoMesAnterior), ate: fimDoMesAnterior };

    Promise.all([financeiroApi.relatoriosDre(mesAtual.de, mesAtual.ate), financeiroApi.relatoriosDre(mesAnterior.de, mesAnterior.ate)])
      .then(([atual, anterior]) => setDre({ dado: deDreResumoDto(atual, anterior.resultadoOperacional.centavos), erro: null, carregando: false }))
      .catch((e: unknown) => setDre({ dado: null, erro: mensagemDeErro(e), carregando: false }));

    financeiroApi
      .receitaRecorrente()
      .then((dto) => setRecorrente({ dado: deTileAssinaturasDto(dto), erro: null, carregando: false }))
      .catch((e: unknown) => setRecorrente({ dado: null, erro: mensagemDeErro(e), carregando: false }));

    financeiroApi
      .radarSimples()
      .then((dto) => setRadar({ dado: deSimplesDto(dto), erro: null, carregando: false }))
      .catch((e: unknown) => setRadar({ dado: null, erro: mensagemDeErro(e), carregando: false }));

    financeiroApi
      .configuracoes()
      .then((cfgDto) => {
        setConfiguracao({ dado: cfgDto, erro: null, carregando: false });
        if (!cfgDto.imobilizadoRoiAtivo) {
          setInvestimento({ dado: null, erro: null, carregando: false });
          return;
        }
        financeiroApi
          .roiNegocio()
          .then((roiDto) => setInvestimento({ dado: deInvestimentoDto(roiDto), erro: null, carregando: false }))
          .catch((e: unknown) => setInvestimento({ dado: null, erro: mensagemDeErro(e), carregando: false }));
      })
      .catch((e: unknown) => {
        setConfiguracao({ dado: null, erro: mensagemDeErro(e), carregando: false });
        setInvestimento({ dado: null, erro: null, carregando: false });
      });
  }, []);

  useEffect(() => {
    carregar();
  }, [carregar]);

  return {
    periodoLabel: periodoLabelDeIso(todayIso()),
    gauge,
    timeline,
    abertoResumo,
    dre,
    recorrente,
    radar,
    configuracao,
    investimento,
    recarregar: carregar,
  };
}

export type VisaoGeralVm = ReturnType<typeof useVisaoGeral>;
