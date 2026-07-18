import { useCallback, useEffect, useState } from 'react';

import { ApiError } from '@/lib/api/client';
import { financeiroApi, type ConfiguracaoFinanceiraDto } from '@/lib/api/financeiro';
import { useToast } from '@/lib/toast';

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

export type ToggleFinanceiro = 'analisePorProjetoAtiva' | 'imobilizadoRoiAtivo';

/**
 * Financeiro › Configurações — os dois toggles opt-in do módulo (`ConfiguracaoFinanceiraTenant`).
 * `GET/PUT /financeiro/configuracoes`: o `PUT` regrava a config INTEIRA (nunca um PATCH parcial —
 * ver `FinanceiroEndpointsModule`), então `alternar` sempre parte do `dado` atual e só inverte o
 * campo pedido. Otimista com rollback: a tela muda na hora, e volta se o servidor rejeitar.
 */
export function useFinanceiroConfiguracoes() {
  const [config, setConfig] = useState<Recurso<ConfiguracaoFinanceiraDto>>(inicial);
  const [salvando, setSalvando] = useState<ToggleFinanceiro | null>(null);
  const { toast } = useToast();

  const carregar = useCallback(() => {
    setConfig(inicial());
    financeiroApi
      .configuracoes()
      .then((dto) => setConfig({ dado: dto, erro: null, carregando: false }))
      .catch((e) => setConfig({ dado: null, erro: mensagemDeErro(e), carregando: false }));
  }, []);

  useEffect(() => {
    carregar();
  }, [carregar]);

  const alternar = useCallback(
    async (campo: ToggleFinanceiro) => {
      const atual = config.dado;
      if (!atual || salvando) return;

      const anterior = atual;
      const proximo: ConfiguracaoFinanceiraDto = { ...atual, [campo]: !atual[campo] };
      setConfig({ dado: proximo, erro: null, carregando: false });
      setSalvando(campo);

      try {
        const salvo = await financeiroApi.salvarConfiguracoes(proximo);
        setConfig({ dado: salvo, erro: null, carregando: false });
        const rotulo = campo === 'analisePorProjetoAtiva' ? 'Análise por Projeto' : 'Imobilizado & ROI';
        toast(`${rotulo} ${salvo[campo] ? 'ativada' : 'desligada'}.`, 'success');
      } catch (e) {
        setConfig({ dado: anterior, erro: null, carregando: false });
        toast(mensagemDeErro(e), 'warning');
      } finally {
        setSalvando(null);
      }
    },
    [config.dado, salvando, toast],
  );

  return { config, salvando, alternar, recarregar: carregar };
}

export type FinanceiroConfiguracoesVm = ReturnType<typeof useFinanceiroConfiguracoes>;
