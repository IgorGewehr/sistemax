import { ConsultorInsight } from '@/components/shared';

import { MoneyValue } from './MoneyValue';
import type { ConsultorFornecedoresData } from './types';

interface ConsultorFornecedoresProps {
  data: ConsultorFornecedoresData;
  onVerDetalhe: () => void;
}

/**
 * Super Consultor (Lei 2 — read-only): observa o desvio de gasto com Fornecedores e explica; o
 * clique de "Ver os N →" é navegação/drill (filtra a Linha do tempo), nunca uma ação da IA.
 */
export function ConsultorFornecedores({ data, onVerDetalhe }: ConsultorFornecedoresProps) {
  return (
    <ConsultorInsight action={{ label: `Ver os ${data.qtdPagamentos} →`, onClick: onVerDetalhe }}>
      <b>Gasto com Fornecedores subiu {data.deltaPct}%</b> vs sua média de <MoneyValue centavos={data.mediaHistoricaCentavos} /> — os{' '}
      {data.qtdPagamentos} pagamentos deste mês somam <MoneyValue centavos={data.totalMesCentavos} />.
    </ConsultorInsight>
  );
}
