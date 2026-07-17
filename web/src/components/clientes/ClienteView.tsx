import { MoveLeft } from 'lucide-react';

import { SectionCard } from '@/components/shared';
import { Button } from '@/components/ui/Button';

import { STATUS_LABEL, STATUS_TONE } from './calc';
import { Chip, TagChip } from './chips';
import { ClienteStats } from './ClienteStats';
import { HistoricoTable } from './HistoricoTable';
import type { Cliente } from './types';
import type { ClientesVm } from './useClientes';

interface ClienteViewProps {
  vm: ClientesVm;
  cliente: Cliente;
}

/** Ficha — drill de 1 cliente: cabeçalho + stats + histórico de compras/OS. */
export function ClienteView({ vm, cliente }: ClienteViewProps) {
  const historico = vm.historicoPorCliente[cliente.id] ?? [];

  return (
    <div className="mx-auto max-w-6xl px-4 py-6 sm:px-6 lg:py-8">
      <div className="mb-4 inline-flex items-center gap-2.5">
        <button
          type="button"
          onClick={vm.irParaHome}
          aria-label="Voltar"
          className="grid h-[26px] w-[26px] flex-none place-items-center rounded-lg bg-surface-2 text-foreground transition-colors hover:bg-primary-soft hover:text-primary-600 active:brightness-95"
        >
          <MoveLeft className="h-3.5 w-3.5" />
        </button>
        <div className="flex flex-col gap-0.5">
          <div className="text-xs font-semibold text-muted-foreground">CLIENTE</div>
          <h1 className="text-xl font-bold tracking-tight">{cliente.nome}</h1>
        </div>
      </div>

      <div className="mb-4 flex flex-wrap items-start justify-between gap-4">
        <div className="flex flex-wrap items-center gap-2 text-[13px] text-muted-foreground">
          <Chip tone={STATUS_TONE[cliente.status]}>{STATUS_LABEL[cliente.status]}</Chip>
          {cliente.tags.map((tag) => (
            <TagChip key={tag}>{tag}</TagChip>
          ))}
        </div>
        <div className="flex items-center gap-2.5">
          <Button variant="outline" size="sm" onClick={() => vm.onAbrirEditar(cliente.id)}>
            Editar
          </Button>
          <Button variant={cliente.status === 'ativo' ? 'danger' : 'outline'} size="sm" onClick={() => vm.onAbrirConfirmStatus(cliente.id)}>
            {cliente.status === 'ativo' ? 'Desativar' : 'Reativar'}
          </Button>
        </div>
      </div>

      <ClienteStats cliente={cliente} hojeLabel={vm.hojeLabel} />

      <SectionCard title="Histórico de compras & OS" bodyClassName="overflow-x-auto">
        <HistoricoTable itens={historico} />
      </SectionCard>
    </div>
  );
}
