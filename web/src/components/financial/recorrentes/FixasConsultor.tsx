/**
 * Super Consultor da lente Contas fixas — mockup: `#consultorFixas` (`renderConsultorFixas`).
 * Read-only: só observa/explica/aconselha; o link é navegação (drill), nunca ação (Lei 2).
 */
import { ConsultorInsight } from '@/components/shared';
import type { Centavos } from '@/lib/money';

import { MoneyText } from './primitives';
import type { ContaFixaDerivada } from './types';

interface FixasConsultorProps {
  luz: ContaFixaDerivada;
  deltaAnualLuz: Centavos;
  notaPrazoConsultor: string;
  onVerConta: () => void;
}

export function FixasConsultor({ luz, deltaAnualLuz, notaPrazoConsultor, onVerConta }: FixasConsultorProps) {
  return (
    <ConsultorInsight className="mb-4" action={{ label: 'Ver a conta →', onClick: onVerConta }}>
      <strong className="font-bold text-primary-600">Prioridade da semana:</strong> a conta de{' '}
      <strong className="font-bold text-primary-600">luz vence dia {luz.diaVencimento}</strong> ({notaPrazoConsultor}) e veio{' '}
      <strong className="font-bold text-crit">{Math.round(luz.variacaoPct)}% acima</strong> da sua média de 6 meses —{' '}
      <MoneyText centavos={luz.atual} className="font-bold text-primary-600" /> vs{' '}
      <MoneyText centavos={luz.media6m} className="font-bold text-primary-600" />. Se virou o novo normal, seu custo fixo sobe{' '}
      <MoneyText centavos={deltaAnualLuz} className="font-bold text-crit" /> por ano.
    </ConsultorInsight>
  );
}
