import { useCallback, useEffect, useState } from 'react';

import { deRecorrentesDetalheDto, type AssinaturaDetalheReal } from '@/lib/api/adapters/financeiro/recorrentes';
import { ApiError } from '@/lib/api/client';
import { financeiroApi } from '@/lib/api/financeiro';

export interface Recurso<T> {
  dado: T | null;
  erro: string | null;
  carregando: boolean;
}

export interface UseAssinaturasDetalheResult extends Recurso<AssinaturaDetalheReal[]> {
  recarregar: () => void;
}

/**
 * Detalhe nominal REAL de "Todas as assinaturas" — `GET /financeiro/recorrentes/detalhe`
 * (docs/wiring/financeiro-telas-restantes.md §2/§C, task #33). Complementa o resumo agregado de
 * `useReceitaRecorrente` (que não devolve a lista nominal de propósito).
 */
export function useAssinaturasDetalhe(): UseAssinaturasDetalheResult {
  const [dado, setDado] = useState<AssinaturaDetalheReal[] | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(true);

  const carregar = useCallback(() => {
    setCarregando(true);
    setErro(null);
    setDado(null);

    financeiroApi
      .recorrentesDetalhe()
      .then((dtos) => {
        setDado(deRecorrentesDetalheDto(dtos));
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
