import { useCallback, useEffect, useState } from 'react';

import { deRecorrentesFixasDto, type ContaFixaResumoReal } from '@/lib/api/adapters/financeiro/recorrentes';
import { ApiError } from '@/lib/api/client';
import { financeiroApi } from '@/lib/api/financeiro';

export interface Recurso<T> {
  dado: T | null;
  erro: string | null;
  carregando: boolean;
}

export interface UseContasFixasRealResult extends Recurso<ContaFixaResumoReal[]> {
  recarregar: () => void;
}

/**
 * Template REAL de "Todas as recorrências" (lente Contas fixas) — `GET /financeiro/recorrentes/fixas`
 * (docs/wiring/financeiro-telas-restantes.md §2/§C, task #33). Só o template — histórico de 12
 * meses/variação/`emAlerta` do painel `PainelContasFixas` continua ilustrativo (`RECORRENTES_MOCK`),
 * o read-model de cruzamento ainda não existe (ver XML doc de `ContasFixasService`).
 */
export function useContasFixasReal(): UseContasFixasRealResult {
  const [dado, setDado] = useState<ContaFixaResumoReal[] | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(true);

  const carregar = useCallback(() => {
    setCarregando(true);
    setErro(null);
    setDado(null);

    financeiroApi
      .recorrentesFixas()
      .then((dtos) => {
        setDado(deRecorrentesFixasDto(dtos));
        setCarregando(false);
      })
      .catch((e: unknown) => {
        setErro(e instanceof ApiError ? e.message : 'Não foi possível carregar.');
        setCarregando(false);
      });
  }, []);

  useEffect(() => {
    carregar();
  }, [carregar]);

  return { dado, erro, carregando, recarregar: carregar };
}
