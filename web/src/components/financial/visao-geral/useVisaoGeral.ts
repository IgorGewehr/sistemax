import { useCallback, useEffect, useState } from 'react';

import { deConsultorDtos } from '@/lib/api/adapters/financeiro/consultor';
import { deBreakevenDto, deInadimplenciaDto, deRadarSimplesDto, deRunwayDto } from '@/lib/api/adapters/financeiro/sobrevivencia';
import {
  deDisponivelDto,
  deLucroDoMesDto,
  deProximosVencimentosDeExtrato,
  deTimelineDto,
} from '@/lib/api/adapters/financeiro/visaoGeral';
import { ApiError } from '@/lib/api/client';
import { financeiroApi } from '@/lib/api/financeiro';
import { addDays, endOfMonthIso, startOfMonthIso, todayIso } from '@/lib/date';
import { visaoGeralMock } from '@/mocks/financeiro/visao-geral';

import type {
  BreakevenCardData,
  InadimplenciaCardData,
  RadarSimplesCardData,
  RunwayCardData,
} from './sobrevivencia/types';
import type { ConsultorViewModel, DisponivelViewModel, LucroDoMesViewModel, ProximoVencimento, TimelineViewModel } from './types';

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
 * quebrado (ex.: `/financeiro/inadimplencia` fora do ar) não deve derrubar os outros.
 *
 * `lucroDoMes` (DRE mês atual + anterior, `relatorios/dre`, + `relatorios/contas-em-aberto` pro
 * "ainda por receber") e `proximosVencimentos` (`GET /financeiro/extrato`, linhas não pagas dos
 * próximos 7 dias) viraram REAIS nesta reconciliação (docs/wiring/financeiro-telas-restantes.md
 * §33) — nenhum dos dois usa mock nem precisa de `MockBadge` mais. O `consultor` (bloco ⑤) já era
 * real: `GET /financeiro/consultor`.
 */
export function useVisaoGeral() {
  const [disponivel, setDisponivel] = useState<Recurso<DisponivelViewModel>>(inicial);
  const [timeline, setTimeline] = useState<Recurso<TimelineViewModel>>(inicial);
  const [lucroDoMes, setLucroDoMes] = useState<Recurso<LucroDoMesViewModel>>(inicial);
  const [proximosVencimentos, setProximosVencimentos] = useState<Recurso<ProximoVencimento[]>>(inicial);
  const [runway, setRunway] = useState<Recurso<RunwayCardData>>(inicial);
  const [breakeven, setBreakeven] = useState<Recurso<BreakevenCardData>>(inicial);
  const [inadimplencia, setInadimplencia] = useState<Recurso<InadimplenciaCardData>>(inicial);
  const [radarSimples, setRadarSimples] = useState<Recurso<RadarSimplesCardData>>(inicial);
  const [consultor, setConsultor] = useState<Recurso<ConsultorViewModel>>(inicial);

  const carregar = useCallback(() => {
    setDisponivel(inicial());
    setTimeline(inicial());
    setLucroDoMes(inicial());
    setProximosVencimentos(inicial());
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

    const hoje = todayIso();
    const mesAtual = { de: startOfMonthIso(hoje), ate: endOfMonthIso(hoje) };
    const fimDoMesAnterior = addDays(mesAtual.de, -1);
    const mesAnterior = { de: startOfMonthIso(fimDoMesAnterior), ate: fimDoMesAnterior };

    Promise.all([
      financeiroApi.relatoriosDre(mesAtual.de, mesAtual.ate),
      financeiroApi.relatoriosDre(mesAnterior.de, mesAnterior.ate),
      financeiroApi.relatoriosContasEmAberto(),
    ])
      .then(([dreAtual, dreAnterior, contasEmAberto]) => {
        setLucroDoMes({
          dado: deLucroDoMesDto(dreAtual, dreAnterior.resultadoOperacional.centavos, contasEmAberto),
          erro: null,
          carregando: false,
        });
      })
      .catch((e) => setLucroDoMes({ dado: null, erro: mensagemDeErro(e), carregando: false }));

    financeiroApi
      .extrato(hoje, addDays(hoje, 7))
      .then((dto) => setProximosVencimentos({ dado: deProximosVencimentosDeExtrato(dto.linhas), erro: null, carregando: false }))
      .catch((e) => setProximosVencimentos({ dado: null, erro: mensagemDeErro(e), carregando: false }));

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
    // REAL — relatorios/dre (atual + anterior) + relatorios/contas-em-aberto.
    lucroDoMes,
    // REAL — GET /financeiro/extrato (linhas não pagas dos próximos 7 dias).
    proximosVencimentos,
    // REAL — GET /financeiro/consultor (insights narrados/rankeados pelo backend).
    consultor,
    sobrevivencia: { runway, breakeven, inadimplencia, radarSimples },
    recarregar: carregar,
  };
}

export type VisaoGeralVm = ReturnType<typeof useVisaoGeral>;
