import { AlertTriangle } from 'lucide-react';
import { useEffect, useState } from 'react';

import { cn } from '@/lib/utils';

import { AgendaDialogShell } from './AgendaDialogShell';
import type { Agendamento } from './types';
import type { AgendaVm } from './useAgenda';

interface DeleteConfirmDialogProps {
  vm: AgendaVm;
}

/**
 * Confirmação de exclusão — 3 ações: Cancelar (mantém registro) / Excluir permanentemente /
 * Excluir série completa (se `recorrenciaId`). Porte do `DeleteConfirmDialog` (L1456-1535) do
 * saas-erp. "Voltar" reabre o form de edição (`abrirEditar`) em vez de só fechar — nosso
 * `AgendaDialogState` é uma união única (só um dialog aberto por vez), então a tela de origem
 * (form) precisa ser reconstruída explicitamente, não estava "por baixo" como no MUI empilhado.
 */
export function DeleteConfirmDialog({ vm }: DeleteConfirmDialogProps) {
  const [ultimo, setUltimo] = useState<Agendamento | null>(null);

  useEffect(() => {
    if (vm.dialog.kind === 'excluir') setUltimo(vm.dialog.agendamento);
  }, [vm.dialog]);

  const open = vm.dialog.kind === 'excluir';
  const agendamento = vm.dialog.kind === 'excluir' ? vm.dialog.agendamento : ultimo;
  if (!agendamento) return null;

  return (
    <AgendaDialogShell open={open} onClose={vm.fecharDialog} ariaLabel="Confirmar exclusão do agendamento" maxWidthClassName="max-w-sm">
      <div className="py-2 text-center">
        <div className="mx-auto mb-4 flex h-12 w-12 items-center justify-center rounded-xl bg-crit-soft">
          <AlertTriangle className="h-6 w-6 text-crit" />
        </div>
        <h3 className="mb-2 text-lg font-semibold text-foreground">Excluir agendamento</h3>
        <p className="mb-1 text-sm text-muted-foreground">Deseja excluir o agendamento de {agendamento.clienteNome}?</p>
        <p className="mb-6 text-xs text-muted-foreground/80">Você pode cancelar o agendamento ou excluí-lo permanentemente.</p>

        <div className="flex flex-col gap-2">
          <button
            type="button"
            onClick={() => vm.cancelar(agendamento.id)}
            className="w-full rounded-xl bg-warn-soft px-4 py-2.5 text-sm font-medium text-warn transition-colors hover:bg-warn-soft/70 active:brightness-95"
          >
            Cancelar agendamento (manter registro)
          </button>
          <button
            type="button"
            onClick={() => vm.excluir(agendamento.id)}
            className="w-full rounded-xl bg-crit px-4 py-2.5 text-sm font-semibold text-white shadow-sm transition-colors hover:brightness-110 active:brightness-95"
          >
            Excluir permanentemente
          </button>
          {agendamento.recorrenciaId && (
            <button
              type="button"
              onClick={() => vm.excluirSerie(agendamento.recorrenciaId!)}
              className={cn(
                'w-full rounded-xl border border-crit/30 bg-crit-soft px-4 py-2.5 text-sm font-semibold text-crit transition-colors hover:bg-crit-soft/70 active:brightness-95',
              )}
            >
              Excluir série completa
            </button>
          )}
          <button
            type="button"
            onClick={() => vm.abrirEditar(agendamento)}
            className="w-full rounded-xl px-4 py-2.5 text-sm font-medium text-muted-foreground transition-colors hover:bg-secondary active:brightness-95"
          >
            Voltar
          </button>
        </div>
      </div>
    </AgendaDialogShell>
  );
}
