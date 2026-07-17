/**
 * Super Consultor do resumo da lente Assinaturas — mockup: `painelAssinaturas > .consultor`.
 * Read-only: só observa/explica/aconselha; o link é navegação (drill), nunca ação (Lei 2).
 */
import { ConsultorInsight } from '@/components/shared';
import type { Centavos } from '@/lib/money';

import { MoneyText } from './primitives';

interface AssinConsultorProps {
  concentracaoClienteNome: string;
  concentracaoPct: number;
  churnMesTotal: Centavos;
  churnClientesMesTotal: number;
  onVerConcentracao: () => void;
}

export function AssinConsultor({
  concentracaoClienteNome,
  concentracaoPct,
  churnMesTotal,
  churnClientesMesTotal,
  onVerConcentracao,
}: AssinConsultorProps) {
  return (
    <ConsultorInsight className="mb-4" action={{ label: 'Ver a concentração →', onClick: onVerConcentracao }}>
      <strong className="font-bold text-primary-600">Prioridade da semana:</strong> sua receita está concentrada demais — a{' '}
      <strong className="font-bold text-primary-600">
        {concentracaoClienteNome} é {Math.round(concentracaoPct)}% do MRR
      </strong>
      . Se ela sair, você perde quase metade. Blinde esse contrato antes de pensar em crescer. (O churn deste mês,{' '}
      <MoneyText centavos={churnMesTotal} />, foi {churnClientesMesTotal} clientes pequenos — dói menos que a dependência de um grande.)
    </ConsultorInsight>
  );
}
