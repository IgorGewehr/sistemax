import { useCallback, useEffect, useState } from 'react';

import { deContasEmAbertoDto, deDreCompetenciaDto, deReceitaRecorrenteParaMrr } from '@/lib/api/adapters/financeiro/relatorios';
import { ApiError } from '@/lib/api/client';
import { financeiroApi } from '@/lib/api/financeiro';
import { addDays, todayIso } from '@/lib/date';

import type { AbertoViewModel, DreRegimeBlock, MrrViewModel } from './types';

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

const MESES_PT_MIN = ['janeiro', 'fevereiro', 'março', 'abril', 'maio', 'junho', 'julho', 'agosto', 'setembro', 'outubro', 'novembro', 'dezembro'];

function periodoMesAtual(): { de: string; ate: string } {
  const hoje = todayIso();
  const [ano, mes] = hoje.split('-');
  const primeiroDoProximoMes = new Date(Number(ano), Number(mes), 1);
  const ultimoDoMes = addDays(`${primeiroDoProximoMes.getFullYear()}-${String(primeiroDoProximoMes.getMonth() + 1).padStart(2, '0')}-01`, -1);
  return { de: `${hoje.slice(0, 7)}-01`, ate: ultimoDoMes };
}

function periodoMesAnterior(atual: { de: string }): { de: string; ate: string } {
  const fimDoMesAnterior = addDays(atual.de, -1);
  return { de: `${fimDoMesAnterior.slice(0, 7)}-01`, ate: fimDoMesAnterior };
}

function nomeMesDeIso(iso: string): string {
  const mesIndex = Number(iso.slice(5, 7)) - 1;
  return MESES_PT_MIN[mesIndex] ?? iso.slice(5, 7);
}

/**
 * Blocos REAIS de Relatórios (docs/wiring/financeiro-telas-restantes.md §5, task #33): MRR
 * (reusa `receita-recorrente`, já ligado noutra tela), Contas em aberto (`relatorios/contas-em-aberto`)
 * e DRE competência (`relatorios/dre`, mês atual + anterior p/ o delta). Regime de caixa e
 * pacote/extrato/histórico continuam ilustrativos — sem read-model ainda.
 */
export function useRelatoriosReais() {
  const [mrr, setMrr] = useState<Recurso<MrrViewModel>>(inicial);
  const [aberto, setAberto] = useState<Recurso<AbertoViewModel>>(inicial);
  const [dreCompetencia, setDreCompetencia] = useState<Recurso<DreRegimeBlock>>(inicial);

  const carregar = useCallback(() => {
    setMrr(inicial());
    setAberto(inicial());
    setDreCompetencia(inicial());

    financeiroApi
      .receitaRecorrente()
      .then((dto) => setMrr({ dado: deReceitaRecorrenteParaMrr(dto), erro: null, carregando: false }))
      .catch((e: unknown) => setMrr({ dado: null, erro: mensagemDeErro(e), carregando: false }));

    financeiroApi
      .relatoriosContasEmAberto()
      .then((dto) => setAberto({ dado: deContasEmAbertoDto(dto), erro: null, carregando: false }))
      .catch((e: unknown) => setAberto({ dado: null, erro: mensagemDeErro(e), carregando: false }));

    const atual = periodoMesAtual();
    const anterior = periodoMesAnterior(atual);
    Promise.all([financeiroApi.relatoriosDre(atual.de, atual.ate), financeiroApi.relatoriosDre(anterior.de, anterior.ate)])
      .then(([dreAtual, dreAnterior]) => {
        setDreCompetencia({
          dado: deDreCompetenciaDto(dreAtual, dreAnterior.resultadoOperacional.centavos, nomeMesDeIso(anterior.de)),
          erro: null,
          carregando: false,
        });
      })
      .catch((e: unknown) => setDreCompetencia({ dado: null, erro: mensagemDeErro(e), carregando: false }));
  }, []);

  useEffect(() => {
    carregar();
  }, [carregar]);

  return { mrr, aberto, dreCompetencia, recarregar: carregar };
}
