import { useCallback, useEffect, useState } from 'react';

import { deReceitaRecorrenteDto, type ResumoRealAssinaturas } from '@/lib/api/adapters/financeiro/recorrentes';
import { ApiError } from '@/lib/api/client';
import { financeiroApi } from '@/lib/api/financeiro';

export interface Recurso<T> {
  dado: T | null;
  erro: string | null;
  carregando: boolean;
}

export interface UseReceitaRecorrenteResult extends Recurso<ResumoRealAssinaturas> {
  recarregar: () => void;
}

/**
 * Único dado REAL do resumo agregado de Assinaturas: `GET /financeiro/receita-recorrente` (existe,
 * em DI, ver `FinanceiroEndpointsModule.cs`). O restante da lente Assinaturas (`servicos[]`,
 * `carteira`, `mrrMesAnterior`) continua em `mocks/financeiro/recorrentes.ts` porque o read-model
 * de origem ainda não expõe granularidade por serviço nem `referencia` (mês anterior) — ver
 * docs/wiring/financeiro-api-contract.md §5. A lente "Contas fixas" já tem endpoint REAL próprio
 * (`GET /financeiro/recorrentes/fixas`, ver `useContasFixasReal.ts`).
 */
export function useReceitaRecorrente(): UseReceitaRecorrenteResult {
  const [dado, setDado] = useState<ResumoRealAssinaturas | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(true);

  const carregar = useCallback(() => {
    setCarregando(true);
    setErro(null);
    setDado(null);

    financeiroApi
      .receitaRecorrente()
      .then((dto) => {
        setDado(deReceitaRecorrenteDto(dto));
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
