import type { ComponentType } from 'react';

import type { MetodoPagamento } from '@/lib/api/vendas';

/**
 * As "telas" do PDV — troca de conteúdo na mesma rota (`display`, não navegação), igual ao
 * padrão de Compras/Ordem de Serviço. `sucesso` é DERIVADO de `venda.status === 'Concluida'`
 * (não é escolha do operador), então nunca aparece na lista de telas que o operador navega.
 */
export type PdvScreen = 'venda' | 'pagamento' | 'sucesso';

/**
 * Caixa = busca por nome/SKU em typeahead (bipe rápido). Balcão = grade de produtos por
 * categoria pro operador tocar direto. Mesmas 2 superfícies do mockup `pdv.html` — só que aqui
 * o alternador mora FORA dos 2 cards (no mockup fonte ele vive dentro do card de busca, que
 * some no modo Balcão: um beco sem saída pra voltar ao Caixa. Ver README, "Divergências do
 * mockup corrigidas").
 */
export type TerminalMode = 'caixa' | 'balcao';

export interface MetodoOpcao {
  key: MetodoPagamento;
  label: string;
  icon: ComponentType<{ className?: string }>;
}
