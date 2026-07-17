import { ConsultorInsight, MoneyValue } from '@/components/shared';

import type { ConsultorInsightData } from './types';

interface ConsultorSectionProps {
  data: ConsultorInsightData;
}

/**
 * Super Consultor da fila — só observa e explica (Lei 2: read-only). Cita nominalmente o
 * orçamento mais valioso parado há 5+ dias; se não houver nenhum, aponta a prateleira de prontas.
 * Sem link de ação — o mockup não tem CTA nesta seção, só o texto.
 */
export function ConsultorSection({ data }: ConsultorSectionProps) {
  const { qtdEsperaLonga, valorParadoCentavos, maiorAguardando, prontasCount, prontasValorCentavos } = data;

  return (
    <ConsultorInsight>
      {qtdEsperaLonga > 0 && maiorAguardando ? (
        <>
          <b>
            {qtdEsperaLonga} orçamento{qtdEsperaLonga > 1 ? 's' : ''} esperam resposta há mais de 5 dias
          </b>{' '}
          — <MoneyValue centavos={valorParadoCentavos} /> parados. O de maior valor é a {maiorAguardando.equipamentoLower} da{' '}
          <b>
            {maiorAguardando.clientePrimeiroNome} ({maiorAguardando.numero}, <MoneyValue centavos={maiorAguardando.valorCentavos} />)
          </b>
          ; o orçamento vence {maiorAguardando.venceDiaSemanaLower}. Mande o lembrete antes de abrir OS nova.
        </>
      ) : (
        <>
          Nenhum orçamento parado há mais de 5 dias — a fila de aprovação está saudável. Foco da semana: {prontasCount} OS prontas
          esperando retirada, <MoneyValue centavos={prontasValorCentavos} /> em dinheiro parado na prateleira.
        </>
      )}
    </ConsultorInsight>
  );
}
