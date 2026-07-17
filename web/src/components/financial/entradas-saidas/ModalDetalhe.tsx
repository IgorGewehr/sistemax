import type { ReactNode } from 'react';

import { StatusChip } from '@/components/shared';
import { Button } from '@/components/ui/Button';
import { formatDateShort } from '@/lib/format';

import { categoriaLabel } from './calc';
import { ModalShell } from './ModalShell';
import { formatSignedCentavosWhole } from './money';
import type { LancamentoRow } from './types';

interface ModalDetalheProps {
  open: boolean;
  row: LancamentoRow | null;
  onClose: () => void;
}

/** Modal "Detalhe do lançamento" — só leitura (o mockup não deixa editar um lançamento já pago). */
export function ModalDetalhe({ open, row, onClose }: ModalDetalheProps) {
  if (!row) return null;

  const sinal = row.tipo === 'entrada' ? 1 : -1;

  return (
    <ModalShell
      open={open}
      onClose={onClose}
      eyebrow="Lançamento realizado"
      title={row.desc}
      description={`${formatDateShort(row.data)} · ${formatSignedCentavosWhole(row.valorCentavos * sinal)}`}
      footer={
        <Button variant="primary" size="sm" onClick={onClose}>
          Fechar
        </Button>
      }
    >
      <div className="flex flex-col gap-2 rounded-xl bg-surface-2 p-3.5">
        <Linha label="Categoria" valor={categoriaLabel(row.categoria)} />
        <Linha label="Conta" valor={row.conta ?? '—'} />
        <Linha label="Origem" valor={row.origem ?? '—'} />
        <div className="flex items-center justify-between gap-2.5 text-[13.5px]">
          <span className="text-muted-foreground">Conciliação</span>
          <StatusChip tone="sobra">✓ Conciliado</StatusChip>
        </div>
      </div>
    </ModalShell>
  );
}

function Linha({ label, valor }: { label: string; valor: ReactNode }) {
  return (
    <div className="flex items-center justify-between gap-2.5 text-[13.5px]">
      <span className="text-muted-foreground">{label}</span>
      <b className="font-bold text-foreground">{valor}</b>
    </div>
  );
}
