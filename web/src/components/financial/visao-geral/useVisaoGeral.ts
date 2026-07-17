import { useCallback, useEffect, useState } from 'react';

import { deConsultorDtos } from '@/lib/api/adapters/financeiro/consultor';
import { deBreakevenDto, deInadimplenciaDto, deRadarSimplesDto, deRunwayDto } from '@/lib/api/adapters/financeiro/sobrevivencia';
import { deDisponivelDto, deTimelineDto } from '@/lib/api/adapters/financeiro/visaoGeral';
import { ApiError } from '@/lib/api/client';
import { financeiroApi } from '@/lib/api/financeiro';
import { visaoGeralMock } from '@/mocks/financeiro/visao-geral';

import type {
  BreakevenCardData,
  InadimplenciaCardData,
  RadarSimplesCardData,
  RunwayCardData,
} from './sobrevivencia/types';
import type { ConsultorViewModel, DisponivelViewModel, TimelineViewModel } from './types';

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

/**
 * Todo o estado de dado REAL de "Visão Geral" vive aqui — mesmo padrão de `useEstoque` (F1c),
 * mas com um `Recurso<T>` por bloco em vez de um `carregando`/`erroCarregamento` únicos: um card
 * quebrado (ex.: `/financeiro/inadimplencia` fora do ar) não deve derrubar os outros 5.
 *
 * `lucroDoMes`/`proximosVencimentos` continuam vindo do MOCK — não têm read-model exposto ainda
 * (`GET /financeiro/dre` não existe, ver docs/wiring/financeiro-api-contract.md §3); a página marca
 * esses blocos com `MockBadge`. O `consultor` (bloco ⑤) JÁ é real: `GET /financeiro/consultor`.
 */
export function useVisaoGeral() {
  const [disponivel, setDisponivel] = useState<Recurso<DisponivelViewModel>>(inicial);
  const [timeline, setTimeline] = useState<Recurso<TimelineViewModel>>(inicial);
  const [runway, setRunway] = useState<Recurso<RunwayCardData>>(inicial);
  const [breakeven, setBreakeven] = useState<Recurso<BreakevenCardData>>(inicial);
  const [inadimplencia, setInadimplencia] = useState<Recurso<InadimplenciaCardData>>(inicial);
  const [radarSimples, setRadarSimples] = useState<Recurso<RadarSimplesCardData>>(inicial);
  const [consultor, setConsultor] = useState<Recurso<ConsultorViewModel>>(inicial);

  const carregar = useCallback(() => {
    setDisponivel(inicial());
    setTimeline(inicial());
    setRunway(inicial());
    setBreakeven(inicial());
    setInadimplencia(inicial());
    setRadarSimples(inicial());
    setConsultor(inicial());

    financeiroApi
      .disponivelParaRetirada()
      .then((dto) => setDisponivel({ dado: deDisponivelDto(dto), erro: null, carregando: false }))
      .catch((e) => setDisponivel({ dado: null, erro: mensagemDeErro(e), carregando: false }));

    financeiroApi
      .fluxo()
      .then((dto) => setTimeline({ dado: deTimelineDto(dto), erro: null, carregando: false }))
      .catch((e) => setTimeline({ dado: null, erro: mensagemDeErro(e), carregando: false }));

    financeiroApi
      .previsaoCaixa()
      .then((dto) => setRunway({ dado: deRunwayDto(dto), erro: null, carregando: false }))
      .catch((e) => setRunway({ dado: null, erro: mensagemDeErro(e), carregando: false }));

    financeiroApi
      .pontoEquilibrio()
      .then((dto) => setBreakeven({ dado: deBreakevenDto(dto), erro: null, carregando: false }))
      .catch((e) => setBreakeven({ dado: null, erro: mensagemDeErro(e), carregando: false }));

    financeiroApi
      .inadimplencia()
      .then((dto) => setInadimplencia({ dado: deInadimplenciaDto(dto), erro: null, carregando: false }))
      .catch((e) => setInadimplencia({ dado: null, erro: mensagemDeErro(e), carregando: false }));

    financeiroApi
      .radarSimples()
      .then((dto) => setRadarSimples({ dado: deRadarSimplesDto(dto), erro: null, carregando: false }))
      .catch((e) => setRadarSimples({ dado: null, erro: mensagemDeErro(e), carregando: false }));

    financeiroApi
      .consultor()
      .then((dto) => setConsultor({ dado: deConsultorDtos(dto), erro: null, carregando: false }))
      .catch((e) => setConsultor({ dado: null, erro: mensagemDeErro(e), carregando: false }));
  }, []);

  useEffect(() => {
    carregar();
  }, [carregar]);

  return {
    periodoLabel: visaoGeralMock.periodoLabel,
    disponivel,
    timeline,
    // MOCK — sem endpoint ainda (GET /financeiro/dre não existe).
    lucroDoMes: visaoGeralMock.lucroDoMes,
    // MOCK — sem endpoint ainda (precisa juntar ContaAPagar+ContaAReceber por vencimento).
    proximosVencimentos: visaoGeralMock.proximosVencimentos,
    // REAL — GET /financeiro/consultor (insights narrados/rankeados pelo backend).
    consultor,
    sobrevivencia: { runway, breakeven, inadimplencia, radarSimples },
    recarregar: carregar,
  };
}

export type VisaoGeralVm = ReturnType<typeof useVisaoGeral>;
