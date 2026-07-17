import { ConsultorInsight, MoneyWhole } from '@/components/shared';
import type { Centavos } from '@/lib/money';

interface ClientesConsultorProps {
  /** Soma real de `totalGastoVidaCentavos` do segmento "sem comprar 90d+" — sempre derivada em
   *  `calc.ts` (`somaGastoVidaCentavos`), nunca hardcoded aqui (ver JSDoc abaixo). */
  totalGastoVidaSemComprarCentavos: Centavos;
  /** Só filtra a lista (Lei 2: read-only, nunca age) — mesmo padrão de `ComprasConsultor`. */
  onVerSumidos: () => void;
}

/**
 * Super Consultor da Home de Clientes — só observa/explica (Lei 2: read-only, nunca age). A
 * quantidade ("4 clientes"), o nome e os dias da cliente mais parada são texto fixo (calibrado pro
 * cenário de `mocks/clientes.ts`, igual a `components/compras/ComprasConsultor.tsx`); a cifra em R$
 * NÃO é — ela é sempre a soma real do array (`totalGastoVidaSemComprarCentavos`, calculada em
 * `useClientes.ts` via `somaGastoVidaCentavos`), porque um valor hardcoded aqui já divergiu do mock
 * no passado (o card chegou a afirmar quase o dobro do total real do segmento).
 */
export function ClientesConsultor({ totalGastoVidaSemComprarCentavos, onVerSumidos }: ClientesConsultorProps) {
  return (
    <ConsultorInsight className="mb-4" action={{ label: 'Ver quem sumiu →', onClick: onVerSumidos }}>
      <b className="font-bold text-primary-600">4 clientes ativos</b> não compram há mais de{' '}
      <span className="font-bold text-warn">90 dias</span> — juntos, já gastaram{' '}
      <span className="font-bold text-foreground">
        <MoneyWhole centavos={totalGastoVidaSemComprarCentavos} />
      </span>{' '}
      com você ao longo do tempo.{' '}
      <b className="font-bold text-primary-600">Vanessa Cristina Duarte</b> é quem está parada há mais tempo
      (<span className="font-bold text-crit">198 dias</span>) — pode valer uma ligação ou mensagem de reengajamento.
    </ConsultorInsight>
  );
}
