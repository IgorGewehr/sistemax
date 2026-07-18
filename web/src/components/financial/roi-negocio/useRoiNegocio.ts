import { useCallback, useEffect, useState } from 'react';

import { ApiError } from '@/lib/api/client';
import {
  financeiroApi,
  type AporteDeCapitalDto,
  type AtivoDeCapitalDto,
  type ConfiguracaoFinanceiraDto,
  type RoiDoNegocioDto,
} from '@/lib/api/financeiro';

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
 * Financeiro › Investimento & ROI — dado REAL, 1:1 com `docs/ui/mockups/roi-negocio.html`:
 * `GET /financeiro/configuracoes` decide o estado opt-in (`imobilizadoRoiAtivo`);
 * `GET /financeiro/roi-negocio` (painel de KPIs/curva/payback/TIR — 404 com o toggle desligado,
 * nunca um `[]` silencioso: é painel, não listagem); `GET /financeiro/imobilizado` (tabela de
 * bens) e `GET /financeiro/aportes` (capital de giro), ambos `[]` com o toggle desligado.
 */
export function useRoiNegocio() {
  const [configuracao, setConfiguracao] = useState<Recurso<ConfiguracaoFinanceiraDto>>(inicial);
  const [roi, setRoi] = useState<Recurso<RoiDoNegocioDto>>(inicial);
  const [imobilizado, setImobilizado] = useState<Recurso<AtivoDeCapitalDto[]>>(inicial);
  const [aportes, setAportes] = useState<Recurso<AporteDeCapitalDto[]>>(inicial);

  const carregar = useCallback(() => {
    setConfiguracao(inicial());
    setRoi(inicial());
    setImobilizado(inicial());
    setAportes(inicial());

    financeiroApi
      .configuracoes()
      .then((dto) => setConfiguracao({ dado: dto, erro: null, carregando: false }))
      .catch((e) => setConfiguracao({ dado: null, erro: mensagemDeErro(e), carregando: false }));

    financeiroApi
      .roiNegocio()
      .then((dto) => setRoi({ dado: dto, erro: null, carregando: false }))
      .catch((e) => setRoi({ dado: null, erro: e instanceof ApiError && e.status === 404 ? null : mensagemDeErro(e), carregando: false }));

    financeiroApi
      .imobilizado()
      .then((dto) => setImobilizado({ dado: dto, erro: null, carregando: false }))
      .catch((e) => setImobilizado({ dado: null, erro: mensagemDeErro(e), carregando: false }));

    financeiroApi
      .aportes()
      .then((dto) => setAportes({ dado: dto, erro: null, carregando: false }))
      .catch((e) => setAportes({ dado: null, erro: mensagemDeErro(e), carregando: false }));
  }, []);

  useEffect(() => {
    carregar();
  }, [carregar]);

  return { configuracao, roi, imobilizado, aportes, recarregar: carregar };
}

export type RoiNegocioVm = ReturnType<typeof useRoiNegocio>;
