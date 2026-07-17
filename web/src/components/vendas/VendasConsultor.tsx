import { ConsultorInsight } from '@/components/shared';

interface VendasConsultorProps {
  onVerSabados: () => void;
}

/**
 * Super Consultor de Vendas — só observa/aconselha (Lei 2: read-only, nunca age). Copy fixa
 * (mesmo espírito estático de `components/compras/ComprasConsultor.tsx`), com 1 link de drill que
 * rola até a tabela — ver JSDoc de `useVendas.aplicarFiltroSabados` sobre por que é só navegação.
 */
export function VendasConsultor({ onVerSabados }: VendasConsultorProps) {
  return (
    <ConsultorInsight className="mb-4" action={{ label: 'Ver sábados →', onClick: onVerSabados }}>
      <b className="font-bold text-primary-600">Pix</b> já responde por{' '}
      <span className="font-bold text-pos">41% do faturamento</span> do mês, ultrapassando o Débito pela primeira vez.
      As vendas de sábado à tarde (14h–18h) têm ticket <span className="font-bold text-pos">22% acima</span> da média
      do período — vale reforçar o caixa nesse horário.
    </ConsultorInsight>
  );
}
