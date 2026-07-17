import { useCallback } from 'react';
import { useNavigate } from 'react-router-dom';

import { useToast } from '@/lib/toast';

import type { DrillTarget } from './types';

/**
 * Clique de "drill" desta tela (Lei 2 do contrato: link de navegação, nunca ação da IA) — navega
 * de verdade pra aba do Financeiro e mostra a mensagem de contexto do mockup. A aba de destino
 * ainda é um stub sem o filtro aplicado (outras fatias do workflow), então o toast comunica o que
 * o usuário veria lá enquanto a navegação real já acontece.
 */
export function useDrillNav(): (target: DrillTarget) => void {
  const navigate = useNavigate();
  const { toast } = useToast();

  return useCallback(
    (target: DrillTarget) => {
      navigate(target.rota);
      toast(target.mensagem, 'info');
    },
    [navigate, toast],
  );
}
