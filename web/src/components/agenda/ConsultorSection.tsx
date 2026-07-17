import { ConsultorInsight } from '@/components/shared';

import type { AgendaVm } from './useAgenda';

interface ConsultorSectionProps {
  vm: AgendaVm;
}

/**
 * Super Consultor da Agenda — só observa e explica (Lei 2: read-only). Não existe no source
 * (Agenda do saas-erp não tem IA); novo, seguindo o padrão que `ConsultorSection` (OS) e
 * `SuperConsultorSection` (Financeiro) já estabeleceram no SistemaX. Cita quem ainda não
 * confirmou presença hoje e a maior janela livre da grade — o único link é um drill de
 * navegação ("Ver hoje →"), nunca uma ação que muda dados.
 */
export function ConsultorSection({ vm }: ConsultorSectionProps) {
  const { hojeCount, gapsHoje, naoConfirmadosHoje, proximoNaoConfirmado } = vm.consultorInsight;

  function verHoje() {
    vm.irParaHoje();
    vm.setViewMode('dia');
  }

  if (hojeCount === 0) {
    return (
      <ConsultorInsight action={{ label: 'Ver hoje →', onClick: verHoje }}>
        Nenhum agendamento hoje — a agenda está livre. Bom momento pra encaixar manutenções
        preventivas ou retornos de clientes.
      </ConsultorInsight>
    );
  }

  const maiorGap = gapsHoje.slice().sort((a, b) => b.minutos - a.minutos)[0] ?? null;

  return (
    <ConsultorInsight action={{ label: 'Ver hoje →', onClick: verHoje }}>
      {hojeCount} agendamento{hojeCount > 1 ? 's' : ''} hoje.{' '}
      {naoConfirmadosHoje > 0 && proximoNaoConfirmado ? (
        <>
          <b>
            {naoConfirmadosHoje} ainda não confirmou{naoConfirmadosHoje > 1 ? 'ram' : ''} presença
          </b>{' '}
          — o próximo é <b>{proximoNaoConfirmado.clienteNome}</b> às <b>{proximoNaoConfirmado.horaInicio}</b>. Vale confirmar antes do
          horário chegar.
        </>
      ) : (
        'Todos os agendamentos de hoje já estão confirmados.'
      )}{' '}
      {maiorGap && (
        <>
          Maior janela livre: <b>{maiorGap.inicio}–{maiorGap.fim}</b> ({maiorGap.minutos} min) — dá pra encaixar um atendimento extra.
        </>
      )}
    </ConsultorInsight>
  );
}
