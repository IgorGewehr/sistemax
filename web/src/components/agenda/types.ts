import type { Centavos } from '@/lib/money';

/**
 * View-model de "Agenda" (SDD) — espelha o agregado `Appointment` do saas-erp
 * (lib/types/index.ts) e a FSM de `lib/contracts/fsm/appointment.ts`, adaptado:
 * dinheiro em centavos, catálogos (cliente/profissional/serviço) auto-contidos
 * no mock (não existem módulos Clientes/Profissionais/Serviços no SistemaX
 * ainda), e sem os campos de turma/grupo (Fase 2 — ver README).
 */

export type AgendamentoStatus =
  | 'agendado'
  | 'confirmado'
  | 'em_andamento'
  | 'concluido'
  | 'cancelado'
  | 'nao_compareceu';

export type ViewMode = 'dia' | 'semana' | 'mes';

export type RecorrenciaFrequencia = 'nenhuma' | 'diaria' | 'semanal' | 'quinzenal' | 'mensal';

export interface Cliente {
  id: string;
  nome: string;
  telefone: string;
}

export interface Profissional {
  id: string;
  nome: string;
}

export interface Servico {
  id: string;
  nome: string;
  duracaoMin: number;
  precoCentavos: Centavos;
  /** Cor de IDENTIDADE do catálogo (bolinha do serviço) — decorativa, não é
   *  estado. Nunca reusar pos/crit/warn aqui (aquilo é vocabulário de status). */
  cor: string;
  categoria: string | null;
  ativo: boolean;
  /**
   * Fase 2 (turmas/academia): capacidade de vagas por horário. Ausente/1 =
   * agendamento exclusivo (v1). Campo aberto pra não fechar a porta — ver
   * blueprint §10 e README.
   */
  capacidade?: number;
}

export interface Agendamento {
  id: string;
  clienteId: string;
  clienteNome: string;
  clienteTelefone: string | null;
  servicoId: string | null;
  servicoNome: string | null;
  data: string; // yyyy-MM-dd
  horaInicio: string; // HH:mm
  horaFim: string; // HH:mm (derivado de horaInicio + duracaoMin)
  duracaoMin: number;
  /** 1+ profissionais atribuídos. Vazio = agendamento "da casa" (qualquer um cobre). */
  profissionalIds: string[];
  profissionalNomes: string[];
  status: AgendamentoStatus;
  precoCentavos: Centavos;
  observacoes: string | null;
  /** Presente quando o agendamento faz parte de uma série (criado via "Repetir"). */
  recorrenciaId?: string;
}

export interface AgendaMock {
  clientes: Cliente[];
  profissionais: Profissional[];
  servicos: Servico[];
  agendamentos: Agendamento[];
}

// ── Formulário (criar/editar) — estado local do dialog, distinto do Agendamento persistido ──

export interface AgendamentoFormData {
  clienteId: string;
  clienteNome: string;
  clienteTelefone: string;
  servicoId: string;
  servicoNome: string;
  data: string;
  horaInicio: string;
  duracaoMin: number;
  profissionalIds: string[];
  profissionalNomes: string[];
  status: AgendamentoStatus;
  precoCentavos: Centavos;
  observacoes: string;
  recorrenciaFrequencia: RecorrenciaFrequencia;
  recorrenciaOcorrencias: number;
}

// ── Derivados (calc.ts) ──

export interface ConflitoResultado {
  temConflito: boolean;
  mensagem: string;
}

export interface ConsultorInsightData {
  hojeCount: number;
  /** Buracos livres de 45min+ na grade de hoje (janela útil 06h–22h). */
  gapsHoje: { inicio: string; fim: string; minutos: number }[];
  naoConfirmadosHoje: number;
  proximoNaoConfirmado: { clienteNome: string; horaInicio: string } | null;
}

/** Navegação de dialogs — uma única union em vez de 4 booleans soltos
 *  (mesmo espírito do `ComprasView`/`Rota` de `useCompras.ts`). */
export type AgendaDialogState =
  | { kind: 'fechado' }
  | { kind: 'novo'; data: string; horaInicio: string }
  | { kind: 'editar'; agendamento: Agendamento }
  | { kind: 'ver'; agendamento: Agendamento }
  | { kind: 'excluir'; agendamento: Agendamento };

/** Mapa de transições válidas da FSM (mirror do canTransitionAppointment do saas-erp,
 *  restrito às transições que a UI expõe). Terminal: concluido, cancelado, nao_compareceu. */
export const AGENDAMENTO_TRANSICOES: Record<AgendamentoStatus, AgendamentoStatus[]> = {
  agendado: ['confirmado', 'em_andamento', 'cancelado', 'nao_compareceu'],
  confirmado: ['em_andamento', 'cancelado', 'concluido', 'nao_compareceu'],
  em_andamento: ['concluido', 'cancelado'],
  concluido: [],
  cancelado: [],
  nao_compareceu: [],
};
