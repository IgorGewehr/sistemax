import { ConsultorInsight } from '@/components/shared';

import { MoneyWhole } from './MoneyWhole';
import type { ConsultorInsightMock } from './types';

interface ConsultorSectionProps {
  insight: ConsultorInsightMock;
  onVerQuintas: () => void;
}

/** O Super Consultor aqui só observa e aponta o padrão — quem decide pôr um segundo conferente é
 * o usuário. O link é navegação (drill até as quintas no gráfico), nunca uma ação da IA. */
export function ConsultorSection({ insight, onVerQuintas }: ConsultorSectionProps) {
  return (
    <ConsultorInsight className="mb-4" action={{ label: insight.acaoLabel, onClick: onVerQuintas }}>
      <span className="font-bold text-primary-600">Prioridade da semana:</span> as faltas do mês somam{' '}
      <MoneyWhole centavos={insight.faltasMesCentavos} className="font-bold text-crit" /> (parcialmente compensadas por{' '}
      <MoneyWhole centavos={insight.sobrasMesCentavos} className="font-bold text-pos" /> de sobra) e se concentram nas{' '}
      <span className="font-bold text-primary-600">{insight.diaCriticoLabel}</span> — média{' '}
      <MoneyWhole centavos={insight.diaCriticoMediaCentavos} signed className="font-bold text-crit" />, sempre no
      fechamento sozinho da {insight.operadorCritico}. Ponha um segundo conferente nesse horário antes de desconfiar
      de alguém.
    </ConsultorInsight>
  );
}
