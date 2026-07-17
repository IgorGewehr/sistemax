import { AgendaCalendarCard } from '@/components/agenda/AgendaCalendarCard';
import { AppointmentFormDialog } from '@/components/agenda/AppointmentFormDialog';
import { AppointmentViewDialog } from '@/components/agenda/AppointmentViewDialog';
import { ConsultorSection } from '@/components/agenda/ConsultorSection';
import { DeleteConfirmDialog } from '@/components/agenda/DeleteConfirmDialog';
import { useAgenda } from '@/components/agenda/useAgenda';
import { PageHeader } from '@/components/shared';

/**
 * Agenda — calendário de agendamentos (dia/semana/mês). Página fina: todo o estado/lógica vive
 * em `useAgenda` (hook); aqui só se compõe o calendário + o Super Consultor + os 3 dialogs,
 * mesmo contrato de `Compras`/`OrdemServico`. Porte 1:1 do módulo Agenda do saas-erp — ver
 * `components/agenda/README.md` pro mapa completo de decisões de adaptação.
 */
export function Agenda() {
  const vm = useAgenda();

  return (
    <div className="mx-auto max-w-[1400px] px-4 py-6 sm:px-6 lg:py-8">
      <PageHeader subtitle="Sua grade de compromissos, dia a dia." />

      <AgendaCalendarCard vm={vm} />

      <div className="mt-4">
        <ConsultorSection vm={vm} />
      </div>

      <AppointmentViewDialog vm={vm} />
      <AppointmentFormDialog vm={vm} />
      <DeleteConfirmDialog vm={vm} />
    </div>
  );
}
