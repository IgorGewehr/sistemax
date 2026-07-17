import { ConsultorInsight } from '@/components/shared';

/**
 * Super Consultor da Home de Compras — só observa/explica (Lei 2: read-only, nunca age).
 * Copy fixa do mockup (`docs/ui/mockups/compras.html`), sem link de ação — é só o card de análise.
 */
export function ComprasConsultor() {
  return (
    <ConsultorInsight className="mb-4">
      <b className="font-bold text-primary-600">Filé de tilápia</b> subiu <span className="font-bold text-crit">14%</span> nas últimas 3 notas
      da Pescados Sul — impacto estimado de <span className="font-bold text-crit">R$ 182/mês</span> na margem. A Peixaria Norte cotou{' '}
      <span className="font-bold text-pos">R$ 2,10/kg abaixo</span> em maio — vale uma cotação nova.
    </ConsultorInsight>
  );
}
