import { Calendar, Check, Clock, DollarSign, Edit3, FileText, Phone, User as UserIcon, X as XIcon } from 'lucide-react';
import { useEffect, useState, type ReactNode } from 'react';

import { MoneyValue } from '@/components/shared';
import { cn } from '@/lib/utils';

import { AgendaDialogShell } from './AgendaDialogShell';
import { AgendaStatusChip } from './AgendaStatusChip';
import { parseISODate, STATUS_TONE_CLASSES } from './calc';
import type { Agendamento, AgendamentoStatus } from './types';
import type { AgendaVm } from './useAgenda';

interface AppointmentViewDialogProps {
  vm: AgendaVm;
}

const DATA_FORMATTER = new Intl.DateTimeFormat('pt-BR', { weekday: 'long', day: '2-digit', month: 'long', year: 'numeric' });

function iniciais(nome: string): string {
  return nome.split(' ').map((n) => n[0]).filter(Boolean).slice(0, 2).join('').toUpperCase();
}

/** Botões de transição de status aplicáveis ao status atual — só os válidos pela FSM aparecem. */
const TRANSICAO_BOTOES: { de: AgendamentoStatus[]; para: AgendamentoStatus; label: string; icon: typeof Check; classe: string }[] = [
  { de: ['agendado'], para: 'confirmado', label: 'Confirmar', icon: Check, classe: 'text-pos bg-pos-soft hover:bg-pos-soft/70' },
  { de: ['agendado', 'confirmado'], para: 'em_andamento', label: 'Iniciar', icon: Clock, classe: 'text-warn bg-warn-soft hover:bg-warn-soft/70' },
  {
    de: ['confirmado', 'em_andamento'],
    para: 'concluido',
    label: 'Concluir',
    icon: Check,
    classe: 'text-primary-600 bg-primary-soft hover:bg-primary-soft/70',
  },
  { de: ['agendado', 'confirmado'], para: 'cancelado', label: 'Cancelar', icon: XIcon, classe: 'text-crit bg-crit-soft hover:bg-crit-soft/70' },
  {
    de: ['agendado', 'confirmado'],
    para: 'nao_compareceu',
    label: 'Não compareceu',
    icon: XIcon,
    classe: 'text-faint bg-surface-2 hover:bg-surface-2/70',
  },
];

/**
 * Detalhe completo do agendamento + ações de transição de status. Porte do `ViewAppointmentDialog`
 * (L1556-1843) do saas-erp — omite o botão "Conversa" (não há módulo de Conversas no SistemaX).
 *
 * Guarda o último agendamento exibido em estado local: quando `vm.dialog` fecha, o `kind` vira
 * `'fechado'` e não carrega mais o agendamento — sem esse "último lembrado", o conteúdo sumiria
 * abruptamente em vez de esmaecer junto com a animação de saída do `AgendaDialogShell`.
 */
export function AppointmentViewDialog({ vm }: AppointmentViewDialogProps) {
  const [ultimo, setUltimo] = useState<Agendamento | null>(null);

  useEffect(() => {
    if (vm.dialog.kind === 'ver') setUltimo(vm.dialog.agendamento);
  }, [vm.dialog]);

  const open = vm.dialog.kind === 'ver';
  const agendamento = vm.dialog.kind === 'ver' ? vm.dialog.agendamento : ultimo;
  if (!agendamento) return null;

  const tone = STATUS_TONE_CLASSES[agendamento.status];
  const botoesAplicaveis = TRANSICAO_BOTOES.filter((b) => b.de.includes(agendamento.status));

  return (
    <AgendaDialogShell
      open={open}
      onClose={vm.fecharDialog}
      ariaLabel={`Agendamento de ${agendamento.clienteNome}`}
      accentClassName={tone.borda.replace('border-l-', 'bg-')}
      header={
        <div className="flex items-start gap-4">
          <div className={cn('flex h-12 w-12 flex-none items-center justify-center rounded-xl text-lg font-bold', tone.chip)}>
            {iniciais(agendamento.clienteNome)}
          </div>
          <div className="min-w-0 flex-1">
            <h3 className="truncate text-lg font-semibold text-foreground">{agendamento.clienteNome}</h3>
            {agendamento.servicoNome && <p className="mt-0.5 truncate text-sm text-muted-foreground">{agendamento.servicoNome}</p>}
            <AgendaStatusChip status={agendamento.status} className="mt-1.5" />
          </div>
        </div>
      }
      footer={
        <div className="flex flex-wrap items-center gap-2">
          <button
            type="button"
            onClick={() => vm.abrirEditar(agendamento)}
            className="flex items-center gap-1.5 rounded-xl bg-secondary px-4 py-2 text-sm font-medium text-secondary-foreground transition-colors hover:bg-secondary/70 active:brightness-95"
          >
            <Edit3 className="h-3.5 w-3.5" />
            Editar
          </button>

          {botoesAplicaveis.length > 0 && <div className="mx-1 hidden h-6 w-px bg-border sm:block" aria-hidden="true" />}

          {botoesAplicaveis.map(({ para, label, icon: Icon, classe }) => (
            <button
              key={para}
              type="button"
              onClick={() => vm.mudarStatus(agendamento.id, para)}
              className={cn('flex items-center gap-1.5 rounded-xl px-4 py-2 text-sm font-medium transition-colors active:brightness-95', classe)}
            >
              <Icon className="h-3.5 w-3.5" />
              {label}
            </button>
          ))}
        </div>
      }
    >
      <div className="space-y-3">
        <DetalheRow icon={<Calendar className="h-4 w-4" />} label="Data" valor={DATA_FORMATTER.format(parseISODate(agendamento.data))} />
        <DetalheRow
          icon={<Clock className="h-4 w-4" />}
          label="Horário"
          valor={`${agendamento.horaInicio} - ${agendamento.horaFim} (${agendamento.duracaoMin} min)`}
        />
        {agendamento.clienteTelefone && <DetalheRow icon={<Phone className="h-4 w-4" />} label="Telefone" valor={agendamento.clienteTelefone} />}
        {agendamento.profissionalNomes.length > 0 && (
          <DetalheRow
            icon={<UserIcon className="h-4 w-4" />}
            label={agendamento.profissionalNomes.length === 1 ? 'Profissional' : `Profissionais (${agendamento.profissionalNomes.length})`}
            valor={agendamento.profissionalNomes.join(', ')}
          />
        )}
        <DetalheRow icon={<DollarSign className="h-4 w-4" />} label="Valor" valor={<MoneyValue centavos={agendamento.precoCentavos} />} />
        {agendamento.observacoes && <DetalheRow icon={<FileText className="h-4 w-4" />} label="Observações" valor={agendamento.observacoes} />}
      </div>
    </AgendaDialogShell>
  );
}

function DetalheRow({ icon, label, valor }: { icon: ReactNode; label: string; valor: ReactNode }) {
  return (
    <div className="flex items-start gap-3 rounded-xl bg-surface-2/60 px-3 py-2.5">
      <span className="mt-0.5 flex-none text-muted-foreground">{icon}</span>
      <div className="min-w-0">
        <div className="text-xs text-muted-foreground">{label}</div>
        <div className="text-sm font-medium text-foreground">{valor}</div>
      </div>
    </div>
  );
}
