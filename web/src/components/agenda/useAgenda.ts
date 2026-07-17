import { useMemo, useState } from 'react';

import { useToast } from '@/lib/toast';
import { AGENDA_MOCK, ANCHOR_HOJE } from '@/mocks/agenda';

import {
  addDias,
  addMeses,
  addSemanas,
  buildConsultorInsight,
  buildMonthGrid,
  buildWeekDays,
  checkConflito,
  computeStatusSummary,
  addDuracao,
  endOfWeek,
  filterByProfissional,
  formatPeriodo,
  generateRecorrenciaDatas,
  groupByData,
  isMesmoMes,
  parseISODate,
  podeTransitar,
  startOfWeek,
  toISODate,
  uid,
} from './calc';
import type { Agendamento, AgendamentoFormData, AgendamentoStatus, AgendaDialogState, ViewMode } from './types';

/**
 * Todo o estado/lógica da Agenda vive aqui — `pages/Agenda.tsx` e os componentes de
 * `components/agenda/*` permanecem finos, só compondo seções a partir do que este hook devolve
 * (mesmo contrato de `useCompras`/`useOrdemServico`). Sem `businessId`/Firestore: `salvar` /
 * `mudarStatus` / `excluir` só fazem `setAgendamentos(prev => …)` — suficiente pro protótipo
 * navegável; quando a API existir, essas funções trocam de corpo sem a página mudar.
 */
export function useAgenda() {
  const { toast } = useToast();
  const mock = AGENDA_MOCK;
  const [agendamentos, setAgendamentos] = useState<Agendamento[]>(mock.agendamentos);

  const [viewMode, setViewMode] = useState<ViewMode>('semana');
  const [currentDate, setCurrentDate] = useState<Date>(ANCHOR_HOJE);
  const [profissionalFiltro, setProfissionalFiltro] = useState<string | 'todos'>('todos');
  const [dialog, setDialog] = useState<AgendaDialogState>({ kind: 'fechado' });
  const [saving, setSaving] = useState(false);

  // ───────────────────────── Navegação ─────────────────────────

  function irParaHoje() {
    setCurrentDate(ANCHOR_HOJE);
  }
  function irParaData(d: Date) {
    setCurrentDate(d);
  }
  function navegarAnterior() {
    setCurrentDate((d) => {
      if (viewMode === 'dia') return addDias(d, -1);
      if (viewMode === 'semana') return addSemanas(d, -1);
      return addMeses(d, -1);
    });
  }
  function navegarProximo() {
    setCurrentDate((d) => {
      if (viewMode === 'dia') return addDias(d, 1);
      if (viewMode === 'semana') return addSemanas(d, 1);
      return addMeses(d, 1);
    });
  }

  // ───────────────────────── Derivados ─────────────────────────

  const weekDays = useMemo(() => buildWeekDays(currentDate), [currentDate]);
  const monthDays = useMemo(() => buildMonthGrid(currentDate), [currentDate]);
  const periodoLabel = useMemo(() => formatPeriodo(viewMode, currentDate), [viewMode, currentDate]);

  const agendamentosFiltrados = useMemo(
    () => filterByProfissional(agendamentos, profissionalFiltro),
    [agendamentos, profissionalFiltro],
  );
  const agendamentosPorData = useMemo(() => groupByData(agendamentosFiltrados), [agendamentosFiltrados]);

  const agendamentosVisiveis = useMemo(() => {
    if (viewMode === 'dia') {
      const iso = toISODate(currentDate);
      return agendamentosFiltrados.filter((a) => a.data === iso);
    }
    if (viewMode === 'semana') {
      const inicioISO = toISODate(startOfWeek(currentDate));
      const fimISO = toISODate(endOfWeek(currentDate));
      return agendamentosFiltrados.filter((a) => a.data >= inicioISO && a.data <= fimISO);
    }
    return agendamentosFiltrados.filter((a) => isMesmoMes(parseISODate(a.data), currentDate));
  }, [agendamentosFiltrados, viewMode, currentDate]);

  const statusSummary = useMemo(() => computeStatusSummary(agendamentosVisiveis), [agendamentosVisiveis]);
  const consultorInsight = useMemo(
    () => buildConsultorInsight(agendamentosFiltrados, toISODate(ANCHOR_HOJE)),
    [agendamentosFiltrados],
  );

  // ───────────────────────── Dialogs ─────────────────────────

  function abrirNovo(data?: string, horaInicio?: string) {
    setDialog({ kind: 'novo', data: data ?? toISODate(currentDate), horaInicio: horaInicio ?? '09:00' });
  }
  function abrirVer(agendamento: Agendamento) {
    setDialog({ kind: 'ver', agendamento });
  }
  function abrirEditar(agendamento: Agendamento) {
    setDialog({ kind: 'editar', agendamento });
  }
  function abrirExcluir(agendamento: Agendamento) {
    setDialog({ kind: 'excluir', agendamento });
  }
  function fecharDialog() {
    setDialog({ kind: 'fechado' });
  }

  // ───────────────────────── Conflito (exposto pro AppointmentFormDialog) ─────────────────────────

  function checarConflito(profissionalId: string, data: string, horaInicio: string, horaFim: string, excludeId?: string) {
    return checkConflito(agendamentos, profissionalId, data, horaInicio, horaFim, excludeId);
  }

  // ───────────────────────── CRUD (local, sem persistência real) ─────────────────────────

  function salvar(dados: AgendamentoFormData): { ok: true } | { ok: false; motivo: string } {
    if (!dados.clienteNome.trim()) return { ok: false, motivo: 'Selecione ou digite o nome do cliente.' };
    if (!dados.data || !dados.horaInicio) return { ok: false, motivo: 'Informe data e horário.' };

    const isEditing = dialog.kind === 'editar';
    const editId = isEditing ? dialog.agendamento.id : undefined;
    const horaFim = addDuracao(dados.horaInicio, dados.duracaoMin);

    setSaving(true);
    try {
      // Bloqueia o salvar se QUALQUER profissional atribuído tem conflito no slot — mesmo
      // hard-block do source (checkAppointmentConflict).
      for (const profissionalId of dados.profissionalIds) {
        const conflito = checkConflito(agendamentos, profissionalId, dados.data, dados.horaInicio, horaFim, editId);
        if (conflito.temConflito) return { ok: false, motivo: conflito.mensagem };
      }

      if (isEditing) {
        const idAlvo = dialog.agendamento.id;
        setAgendamentos((prev) =>
          prev.map((a) =>
            a.id === idAlvo
              ? {
                  ...a,
                  clienteId: dados.clienteId,
                  clienteNome: dados.clienteNome,
                  clienteTelefone: dados.clienteTelefone || null,
                  servicoId: dados.servicoId || null,
                  servicoNome: dados.servicoNome || null,
                  data: dados.data,
                  horaInicio: dados.horaInicio,
                  horaFim,
                  duracaoMin: dados.duracaoMin,
                  profissionalIds: dados.profissionalIds,
                  profissionalNomes: dados.profissionalNomes,
                  status: dados.status,
                  precoCentavos: dados.precoCentavos,
                  observacoes: dados.observacoes || null,
                }
              : a,
          ),
        );
        // TODO(fase-2): evento appointment.completed (comissão/fidelidade/GCal) quando status
        // vira 'concluido' — dependem de módulos que não existem no SistemaX ainda.
        toast(`Agendamento de ${dados.clienteNome} atualizado.`);
        fecharDialog();
        return { ok: true };
      }

      // Criação — série recorrente ou ocorrência única.
      if (dados.recorrenciaFrequencia !== 'nenhuma') {
        const datas = generateRecorrenciaDatas(dados.data, dados.recorrenciaFrequencia, dados.recorrenciaOcorrencias);
        // Valida conflito em TODAS as datas antes de commitar qualquer uma (tudo ou nada).
        for (const profissionalId of dados.profissionalIds) {
          for (const data of datas) {
            const conflito = checkConflito(agendamentos, profissionalId, data, dados.horaInicio, horaFim);
            if (conflito.temConflito) return { ok: false, motivo: `${data}: ${conflito.mensagem}` };
          }
        }
        const recorrenciaId = uid();
        const novos: Agendamento[] = datas.map((data) => ({
          id: uid(),
          clienteId: dados.clienteId,
          clienteNome: dados.clienteNome,
          clienteTelefone: dados.clienteTelefone || null,
          servicoId: dados.servicoId || null,
          servicoNome: dados.servicoNome || null,
          data,
          horaInicio: dados.horaInicio,
          horaFim,
          duracaoMin: dados.duracaoMin,
          profissionalIds: dados.profissionalIds,
          profissionalNomes: dados.profissionalNomes,
          status: dados.status,
          precoCentavos: dados.precoCentavos,
          observacoes: dados.observacoes || null,
          recorrenciaId,
        }));
        setAgendamentos((prev) => [...prev, ...novos]);
        toast(`${novos.length} agendamentos criados para ${dados.clienteNome}.`);
        fecharDialog();
        return { ok: true };
      }

      const novo: Agendamento = {
        id: uid(),
        clienteId: dados.clienteId,
        clienteNome: dados.clienteNome,
        clienteTelefone: dados.clienteTelefone || null,
        servicoId: dados.servicoId || null,
        servicoNome: dados.servicoNome || null,
        data: dados.data,
        horaInicio: dados.horaInicio,
        horaFim,
        duracaoMin: dados.duracaoMin,
        profissionalIds: dados.profissionalIds,
        profissionalNomes: dados.profissionalNomes,
        status: dados.status,
        precoCentavos: dados.precoCentavos,
        observacoes: dados.observacoes || null,
      };
      setAgendamentos((prev) => [...prev, novo]);
      toast(`Agendamento de ${dados.clienteNome} criado para ${dados.data} às ${dados.horaInicio}.`);
      fecharDialog();
      return { ok: true };
    } finally {
      setSaving(false);
    }
  }

  function mudarStatus(id: string, novoStatus: AgendamentoStatus) {
    const atual = agendamentos.find((a) => a.id === id);
    if (!atual || !podeTransitar(atual.status, novoStatus)) return;
    const atualizado: Agendamento = { ...atual, status: novoStatus };
    setAgendamentos((prev) => prev.map((a) => (a.id === id ? atualizado : a)));
    setDialog((prev) => (prev.kind === 'ver' && prev.agendamento.id === id ? { kind: 'ver', agendamento: atualizado } : prev));
    // TODO(fase-2): side-effects cross-módulo (comissão, fidelidade, baixa de estoque, sync
    // GCal, lembrete WhatsApp) — ver APPOINTMENT_TRANSITION_EFFECTS em
    // lib/contracts/fsm/appointment.ts no saas-erp. Dependem de módulos que ainda não existem
    // no SistemaX; não fingimos que rodam.
    toast(`Status atualizado para "${STATUS_TOAST_LABEL[novoStatus]}".`);
  }

  function cancelar(id: string) {
    mudarStatus(id, 'cancelado');
    fecharDialog();
  }

  function excluir(id: string) {
    setAgendamentos((prev) => prev.filter((a) => a.id !== id));
    toast('Agendamento excluído.', 'warning');
    fecharDialog();
  }

  function excluirSerie(recorrenciaId: string) {
    setAgendamentos((prev) => prev.filter((a) => a.recorrenciaId !== recorrenciaId));
    toast('Série completa excluída.', 'warning');
    fecharDialog();
  }

  return {
    // Catálogos (read-only, auto-contidos no mock)
    clientes: mock.clientes,
    profissionais: mock.profissionais,
    servicos: mock.servicos,

    // Navegação / visualização
    viewMode,
    setViewMode,
    currentDate,
    irParaHoje,
    irParaData,
    navegarAnterior,
    navegarProximo,
    weekDays,
    monthDays,
    periodoLabel,

    // Filtro
    profissionalFiltro,
    setProfissionalFiltro,

    // Dados derivados
    agendamentos,
    agendamentosFiltrados,
    agendamentosPorData,
    agendamentosVisiveis,
    statusSummary,
    consultorInsight,

    // Dialogs
    dialog,
    abrirNovo,
    abrirVer,
    abrirEditar,
    abrirExcluir,
    fecharDialog,

    // Conflito
    checarConflito,

    // CRUD
    saving,
    salvar,
    mudarStatus,
    cancelar,
    excluir,
    excluirSerie,
  };
}

/** Rótulo curto pro toast de transição de status — sem acento/underscore cru na mensagem. */
const STATUS_TOAST_LABEL: Record<AgendamentoStatus, string> = {
  agendado: 'agendado',
  confirmado: 'confirmado',
  em_andamento: 'em andamento',
  concluido: 'concluído',
  cancelado: 'cancelado',
  nao_compareceu: 'não compareceu',
};

export type AgendaVm = ReturnType<typeof useAgenda>;
