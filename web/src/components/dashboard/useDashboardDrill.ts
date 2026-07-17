import { useCallback } from 'react';
import { useNavigate } from 'react-router-dom';

import { useToast } from '@/lib/toast';

import type { DrillTarget } from './types';

/**
 * Clique de "drill" do Dashboard — mesmo princípio do `useDrillNav` do Financeiro: é sempre
 * navegação de verdade, nunca uma ação que o Consultor executa (Lei 2). A diferença é
 * `disponivel`: o Dashboard atravessa módulos que nem todos têm rota registrada ainda (ex.:
 * Vendas — task de desenhar/construir ainda em aberto). Quando `disponivel === false`, o clique só
 * mostra o toast; navegar cairia no catch-all de `App.tsx` e redirecionaria pro Financeiro sem
 * aviso, o que pareceria bug em vez de "em breve".
 */
export function useDashboardDrill(): (target: DrillTarget) => void {
  const navigate = useNavigate();
  const { toast } = useToast();

  return useCallback(
    (target: DrillTarget) => {
      if (target.disponivel === false) {
        toast(target.mensagem, 'info');
        return;
      }
      navigate(target.rota);
      toast(target.mensagem, 'info');
    },
    [navigate, toast],
  );
}
