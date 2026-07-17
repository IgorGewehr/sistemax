import { AnimatePresence, motion } from 'framer-motion';
import { AlertTriangle, Check, Search, Trash2 } from 'lucide-react';
import { useEffect, useMemo, useRef, useState, type ButtonHTMLAttributes, type ReactNode } from 'react';

import { Button } from '@/components/ui/Button';
import { cn } from '@/lib/utils';

import { AgendaDialogShell } from './AgendaDialogShell';
import { DURATION_OPTIONS, STATUS_LABEL, TIME_OPTIONS, addDuracao, parseCentavosDigitados, toISODate } from './calc';
import type { AgendamentoFormData, AgendamentoStatus, RecorrenciaFrequencia } from './types';
import type { AgendaVm } from './useAgenda';

interface AppointmentFormDialogProps {
  vm: AgendaVm;
}

// ── Primitivos de formulário (mesma receita visual em todo o SistemaX — ver `os/StepBodies.tsx`) ──

const inputClass =
  'w-full rounded-xl border border-border bg-card px-3.5 py-2.5 text-sm text-foreground outline-none placeholder:text-muted-foreground focus:border-primary-400 focus:ring-2 focus:ring-primary-100 dark:focus:ring-primary-500/20';

function FieldRow({ label, required, children }: { label: string; required?: boolean; children: ReactNode }) {
  return (
    <div className="flex flex-col gap-1.5">
      <label className="text-xs font-medium text-muted-foreground">
        {label}
        {required && ' *'}
      </label>
      {children}
    </div>
  );
}

function FieldTwo({ children }: { children: ReactNode }) {
  return <div className="grid grid-cols-2 gap-3">{children}</div>;
}

function ChipBtn({ selected, className, ...rest }: ButtonHTMLAttributes<HTMLButtonElement> & { selected: boolean }) {
  return (
    <button
      type="button"
      className={cn(
        'rounded-lg border px-3 py-1.5 text-xs font-medium transition-colors active:brightness-95',
        selected
          ? 'border-primary-600 bg-primary-600 text-white'
          : 'border-border bg-card text-muted-foreground hover:border-primary-300 hover:text-foreground',
        className,
      )}
      {...rest}
    />
  );
}

const STATUS_ORDEM: AgendamentoStatus[] = ['agendado', 'confirmado', 'em_andamento', 'concluido', 'cancelado', 'nao_compareceu'];
const RECORRENCIA_OPCOES: { value: RecorrenciaFrequencia; label: string }[] = [
  { value: 'nenhuma', label: 'Não' },
  { value: 'diaria', label: 'Diário' },
  { value: 'semanal', label: 'Semanal' },
  { value: 'quinzenal', label: 'Quinzenal' },
  { value: 'mensal', label: 'Mensal' },
];

function formVazio(data: string, horaInicio: string): AgendamentoFormData {
  return {
    clienteId: '',
    clienteNome: '',
    clienteTelefone: '',
    servicoId: '',
    servicoNome: '',
    data,
    horaInicio,
    duracaoMin: 60,
    profissionalIds: [],
    profissionalNomes: [],
    status: 'agendado',
    precoCentavos: 0,
    observacoes: '',
    recorrenciaFrequencia: 'nenhuma',
    recorrenciaOcorrencias: 4,
  };
}

/** Centavos → "123,45" pra um input de texto (sem "R$", editável em linha). Vazio quando zero. */
function formatValorInput(centavos: number): string {
  if (!centavos) return '';
  return (centavos / 100).toLocaleString('pt-BR', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
}

/**
 * Criar/editar agendamento — porte de `AppointmentFormDialog.tsx` inteiro do saas-erp. Omite o
 * branch `hasGrid`/turma (Fase 2 — ver README) e a busca de cliente já hidrata direto do catálogo
 * mock (`vm.clientes`), auto-contido (não existe módulo Clientes no SistemaX ainda).
 */
export function AppointmentFormDialog({ vm }: AppointmentFormDialogProps) {
  const { dialog } = vm;
  const open = dialog.kind === 'novo' || dialog.kind === 'editar';
  const isEditing = dialog.kind === 'editar';

  const [formData, setFormData] = useState<AgendamentoFormData>(() => formVazio(toISODate(vm.currentDate), '09:00'));
  const [clienteBusca, setClienteBusca] = useState('');
  const [showClienteDropdown, setShowClienteDropdown] = useState(false);
  const [erro, setErro] = useState<string | null>(null);
  const clienteRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (dialog.kind === 'editar') {
      const a = dialog.agendamento;
      setFormData({
        clienteId: a.clienteId,
        clienteNome: a.clienteNome,
        clienteTelefone: a.clienteTelefone ?? '',
        servicoId: a.servicoId ?? '',
        servicoNome: a.servicoNome ?? '',
        data: a.data,
        horaInicio: a.horaInicio,
        duracaoMin: a.duracaoMin,
        profissionalIds: a.profissionalIds,
        profissionalNomes: a.profissionalNomes,
        status: a.status,
        precoCentavos: a.precoCentavos,
        observacoes: a.observacoes ?? '',
        recorrenciaFrequencia: 'nenhuma',
        recorrenciaOcorrencias: 4,
      });
      setClienteBusca(a.clienteNome);
      setErro(null);
    } else if (dialog.kind === 'novo') {
      setFormData(formVazio(dialog.data, dialog.horaInicio));
      setClienteBusca('');
      setErro(null);
    }
  }, [dialog]);

  useEffect(() => {
    if (!showClienteDropdown) return;
    function aoClicarFora(e: MouseEvent) {
      if (clienteRef.current && !clienteRef.current.contains(e.target as Node)) setShowClienteDropdown(false);
    }
    document.addEventListener('mousedown', aoClicarFora);
    return () => document.removeEventListener('mousedown', aoClicarFora);
  }, [showClienteDropdown]);

  const clientesFiltrados = useMemo(() => {
    const q = clienteBusca.trim().toLowerCase();
    if (!q) return vm.clientes.slice(0, 20);
    return vm.clientes.filter((c) => c.nome.toLowerCase().includes(q) || c.telefone.includes(clienteBusca)).slice(0, 20);
  }, [clienteBusca, vm.clientes]);

  const servicosAtivos = useMemo(() => vm.servicos.filter((s) => s.ativo), [vm.servicos]);

  const horaFim = addDuracao(formData.horaInicio, formData.duracaoMin);
  const editId = isEditing && dialog.kind === 'editar' ? dialog.agendamento.id : undefined;

  // Conflito — checa CADA profissional selecionado; se qualquer um bate, avisa (mesmo hard-block do source).
  const formConflito = useMemo(() => {
    if (formData.profissionalIds.length === 0 || !formData.data || !formData.horaInicio) {
      return { temConflito: false, mensagem: '' };
    }
    const mensagens: string[] = [];
    for (let i = 0; i < formData.profissionalIds.length; i++) {
      const pid = formData.profissionalIds[i];
      const c = vm.checarConflito(pid, formData.data, formData.horaInicio, horaFim, editId);
      if (c.temConflito) mensagens.push(`${formData.profissionalNomes[i] || pid}: ${c.mensagem}`);
    }
    return { temConflito: mensagens.length > 0, mensagem: mensagens.join(' · ') };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [vm.checarConflito, formData.profissionalIds, formData.profissionalNomes, formData.data, formData.horaInicio, formData.duracaoMin, editId]);

  function handleClienteSelect(clienteId: string) {
    const c = vm.clientes.find((x) => x.id === clienteId);
    if (!c) return;
    setFormData((prev) => ({ ...prev, clienteId: c.id, clienteNome: c.nome, clienteTelefone: c.telefone }));
    setClienteBusca(c.nome);
    setShowClienteDropdown(false);
  }

  function handleServicoChange(servicoId: string) {
    const s = vm.servicos.find((x) => x.id === servicoId);
    if (!s) return;
    setFormData((prev) => ({ ...prev, servicoId: s.id, servicoNome: s.nome, duracaoMin: s.duracaoMin, precoCentavos: s.precoCentavos }));
  }

  function toggleProfissional(id: string) {
    setFormData((prev) => {
      const idx = prev.profissionalIds.indexOf(id);
      if (idx >= 0) {
        const ids = prev.profissionalIds.filter((x) => x !== id);
        const nomes = prev.profissionalNomes.filter((_, i) => prev.profissionalIds[i] !== id);
        return { ...prev, profissionalIds: ids, profissionalNomes: nomes };
      }
      const p = vm.profissionais.find((x) => x.id === id);
      return { ...prev, profissionalIds: [...prev.profissionalIds, id], profissionalNomes: [...prev.profissionalNomes, p?.nome ?? ''] };
    });
  }

  function handleSubmit() {
    const resultado = vm.salvar(formData);
    if (!resultado.ok) {
      setErro(resultado.motivo);
      return;
    }
    setErro(null);
  }

  return (
    <AgendaDialogShell
      open={open}
      onClose={vm.fecharDialog}
      ariaLabel={isEditing ? 'Editar agendamento' : 'Novo agendamento'}
      maxWidthClassName="max-w-lg"
      header={<h2 className="font-display text-base font-bold text-foreground">{isEditing ? 'Editar agendamento' : 'Novo agendamento'}</h2>}
      footer={
        <div className={cn('flex items-center gap-2', isEditing ? 'justify-between' : 'justify-end')}>
          {isEditing && dialog.kind === 'editar' && (
            <button
              type="button"
              onClick={() => vm.abrirExcluir(dialog.agendamento)}
              className="flex items-center gap-1.5 rounded-xl px-3 py-2 text-sm font-medium text-crit transition-colors hover:bg-crit-soft active:brightness-95"
            >
              <Trash2 className="h-4 w-4" />
              Excluir
            </button>
          )}
          <div className="flex gap-2">
            <button
              type="button"
              onClick={vm.fecharDialog}
              className="rounded-xl border border-border px-4 py-2.5 text-sm font-medium text-muted-foreground transition-colors hover:bg-secondary active:brightness-95"
            >
              Cancelar
            </button>
            <Button size="md" disabled={!formData.clienteNome.trim() || vm.saving} onClick={handleSubmit}>
              {vm.saving ? 'Salvando…' : isEditing ? 'Salvar alterações' : 'Agendar'}
            </Button>
          </div>
        </div>
      }
    >
      <div className="space-y-4">
        {/* Cliente */}
        <div ref={clienteRef} className="relative">
          <FieldRow label="Cliente" required>
            <div className="relative">
              <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
              <input
                type="text"
                value={clienteBusca}
                onFocus={() => setShowClienteDropdown(true)}
                onChange={(e) => {
                  setClienteBusca(e.target.value);
                  setShowClienteDropdown(true);
                  setFormData((prev) => ({ ...prev, clienteNome: e.target.value, clienteId: '' }));
                }}
                placeholder="Buscar cliente..."
                className={cn(inputClass, 'pl-10')}
              />
            </div>
          </FieldRow>
          <AnimatePresence>
            {showClienteDropdown && clientesFiltrados.length > 0 && (
              <motion.div
                initial={{ opacity: 0, y: -4 }}
                animate={{ opacity: 1, y: 0 }}
                exit={{ opacity: 0, y: -4 }}
                className="absolute left-0 right-0 z-50 mt-1 max-h-48 overflow-y-auto rounded-xl border border-border bg-card shadow-xl"
              >
                {clientesFiltrados.map((c) => (
                  <button
                    key={c.id}
                    type="button"
                    onClick={() => handleClienteSelect(c.id)}
                    className="flex w-full items-center gap-3 px-4 py-2.5 text-left transition-colors first:rounded-t-xl last:rounded-b-xl hover:bg-secondary/60"
                  >
                    <div className="flex h-8 w-8 flex-none items-center justify-center rounded-full bg-surface-2 text-xs font-semibold text-muted-foreground">
                      {c.nome.split(' ').map((n) => n[0]).filter(Boolean).slice(0, 2).join('').toUpperCase()}
                    </div>
                    <div className="min-w-0">
                      <div className="truncate text-sm font-medium text-foreground">{c.nome}</div>
                      <div className="truncate text-xs text-muted-foreground">{c.telefone}</div>
                    </div>
                  </button>
                ))}
              </motion.div>
            )}
          </AnimatePresence>
        </div>

        {/* Serviço */}
        <FieldRow label="Serviço">
          <select value={formData.servicoId} onChange={(e) => handleServicoChange(e.target.value)} className={cn(inputClass, 'appearance-none')}>
            <option value="">Selecionar serviço</option>
            {servicosAtivos.map((s) => (
              <option key={s.id} value={s.id}>
                {s.nome} ({s.duracaoMin} min)
              </option>
            ))}
          </select>
        </FieldRow>

        {/* Data + horário */}
        <FieldTwo>
          <FieldRow label="Data" required>
            <input
              type="date"
              value={formData.data}
              onChange={(e) => setFormData((prev) => ({ ...prev, data: e.target.value }))}
              className={inputClass}
            />
          </FieldRow>
          <FieldRow label="Horário início" required>
            <select
              value={formData.horaInicio}
              onChange={(e) => setFormData((prev) => ({ ...prev, horaInicio: e.target.value }))}
              className={cn(inputClass, 'appearance-none')}
            >
              {TIME_OPTIONS.map((t) => (
                <option key={t} value={t}>
                  {t}
                </option>
              ))}
            </select>
          </FieldRow>
        </FieldTwo>

        {/* Duração + término */}
        <FieldTwo>
          <FieldRow label="Duração">
            <select
              value={formData.duracaoMin}
              onChange={(e) => setFormData((prev) => ({ ...prev, duracaoMin: Number(e.target.value) }))}
              className={cn(inputClass, 'appearance-none')}
            >
              {DURATION_OPTIONS.map((d) => (
                <option key={d} value={d}>
                  {d} min
                </option>
              ))}
            </select>
          </FieldRow>
          <FieldRow label="Término">
            <div className={cn(inputClass, 'num bg-surface-2 text-muted-foreground')}>{horaFim}</div>
          </FieldRow>
        </FieldTwo>

        {/* Profissionais */}
        <div>
          <label className="mb-1.5 block text-xs font-medium text-muted-foreground">
            Profissionais
            {formData.profissionalIds.length > 0 && <span className="ml-1.5 text-[10px] font-semibold text-primary-600">({formData.profissionalIds.length})</span>}
          </label>
          <div
            className={cn(
              'flex min-h-[44px] flex-wrap gap-2 rounded-xl border bg-card p-2',
              formConflito.temConflito ? 'border-warn/50' : 'border-border',
            )}
          >
            {vm.profissionais.length === 0 ? (
              <span className="px-1 py-1 text-xs text-muted-foreground">Nenhum profissional cadastrado.</span>
            ) : (
              vm.profissionais.map((p) => {
                const selecionado = formData.profissionalIds.includes(p.id);
                return (
                  <ChipBtn key={p.id} selected={selecionado} onClick={() => toggleProfissional(p.id)}>
                    {selecionado && <Check className="mr-1 inline h-3 w-3" />}
                    {p.nome}
                  </ChipBtn>
                );
              })
            )}
          </div>
          {formData.profissionalIds.length === 0 && vm.profissionais.length > 0 && (
            <p className="mt-1 text-[10px] text-muted-foreground">Sem profissional = agendamento da casa. Clique pra atribuir 1 ou mais.</p>
          )}
        </div>

        {/* Aviso de conflito */}
        <AnimatePresence>
          {formConflito.temConflito && (
            <motion.div initial={{ opacity: 0, height: 0 }} animate={{ opacity: 1, height: 'auto' }} exit={{ opacity: 0, height: 0 }} transition={{ duration: 0.2 }}>
              <div className="flex items-start gap-2.5 rounded-xl border border-warn/30 bg-warn-soft px-3.5 py-2.5">
                <AlertTriangle className="mt-0.5 h-4 w-4 flex-none text-warn" />
                <div>
                  <div className="text-xs font-semibold text-warn">Conflito de agenda</div>
                  <div className="mt-0.5 text-[11px] text-warn/90">{formConflito.mensagem}</div>
                </div>
              </div>
            </motion.div>
          )}
        </AnimatePresence>

        {/* Status + valor */}
        <FieldTwo>
          <FieldRow label="Status">
            <select
              value={formData.status}
              onChange={(e) => setFormData((prev) => ({ ...prev, status: e.target.value as AgendamentoStatus }))}
              className={cn(inputClass, 'appearance-none')}
            >
              {STATUS_ORDEM.map((s) => (
                <option key={s} value={s}>
                  {STATUS_LABEL[s]}
                </option>
              ))}
            </select>
          </FieldRow>
          <FieldRow label="Valor (R$)">
            <input
              type="text"
              inputMode="numeric"
              value={formatValorInput(formData.precoCentavos)}
              onChange={(e) => setFormData((prev) => ({ ...prev, precoCentavos: parseCentavosDigitados(e.target.value) }))}
              placeholder="0,00"
              className={cn(inputClass, 'num')}
            />
          </FieldRow>
        </FieldTwo>

        {/* Repetir (só criação) */}
        {!isEditing && (
          <div>
            <label className="mb-1.5 block text-xs font-medium text-muted-foreground">Repetir</label>
            <div className="flex flex-wrap gap-2">
              {RECORRENCIA_OPCOES.map((opt) => (
                <ChipBtn
                  key={opt.value}
                  selected={formData.recorrenciaFrequencia === opt.value}
                  onClick={() => setFormData((prev) => ({ ...prev, recorrenciaFrequencia: opt.value }))}
                >
                  {opt.label}
                </ChipBtn>
              ))}
            </div>
            {formData.recorrenciaFrequencia !== 'nenhuma' && (
              <div className="mt-2.5 flex items-center gap-2">
                <span className="text-xs text-muted-foreground">Ocorrências:</span>
                <input
                  type="number"
                  min={2}
                  max={52}
                  value={formData.recorrenciaOcorrencias}
                  onChange={(e) =>
                    setFormData((prev) => ({ ...prev, recorrenciaOcorrencias: Math.max(2, Math.min(52, Number(e.target.value) || 2)) }))
                  }
                  className={cn(inputClass, 'num w-20 py-1.5')}
                />
                <span className="text-[11px] text-muted-foreground">agendamentos vinculados (máx. 52)</span>
              </div>
            )}
          </div>
        )}

        {/* Observações */}
        <FieldRow label="Observações">
          <textarea
            value={formData.observacoes}
            onChange={(e) => setFormData((prev) => ({ ...prev, observacoes: e.target.value }))}
            rows={3}
            placeholder="Observações adicionais..."
            className={cn(inputClass, 'resize-none')}
          />
        </FieldRow>

        {erro && <p className="text-xs font-semibold text-crit">{erro}</p>}
      </div>
    </AgendaDialogShell>
  );
}
