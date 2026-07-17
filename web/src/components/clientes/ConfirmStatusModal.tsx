import { Button } from '@/components/ui/Button';
import { Modal } from '@/components/ui/Modal';

import type { Cliente } from './types';

interface ConfirmStatusModalProps {
  open: boolean;
  cliente: Cliente | undefined;
  onClose: () => void;
  onConfirmar: (clienteId: string) => void;
}

/** Confirma desativar (soft-delete cadastral/LGPD) ou reativar um cliente — acionado só na Ficha. */
export function ConfirmStatusModal({ open, cliente, onClose, onConfirmar }: ConfirmStatusModalProps) {
  if (!cliente) return null;
  const vaiDesativar = cliente.status === 'ativo';

  return (
    <Modal open={open} onClose={onClose} title={vaiDesativar ? 'Desativar cliente' : 'Reativar cliente'}>
      <p className="mb-4 text-[13px] leading-relaxed text-muted-foreground">
        {vaiDesativar ? (
          <>
            <b className="font-semibold text-foreground">{cliente.nome}</b> deixa de aparecer nos segmentos de engajamento
            (aniversariantes, sem comprar 90d+) e some da busca ativa. O histórico de compras é preservado — reative quando quiser.
          </>
        ) : (
          <>
            <b className="font-semibold text-foreground">{cliente.nome}</b> volta a contar nos KPIs e segmentos de engajamento.
          </>
        )}
      </p>
      <div className="flex justify-end gap-2.5">
        <Button variant="outline" size="sm" onClick={onClose}>
          Cancelar
        </Button>
        <Button variant={vaiDesativar ? 'danger' : 'primary'} size="sm" onClick={() => onConfirmar(cliente.id)}>
          {vaiDesativar ? 'Desativar' : 'Reativar'}
        </Button>
      </div>
    </Modal>
  );
}
