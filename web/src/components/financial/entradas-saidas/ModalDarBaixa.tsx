import { useEffect, useState } from 'react';

import { Button } from '@/components/ui/Button';
import { formatDateShort } from '@/lib/format';
import { cn } from '@/lib/utils';

import { ModalShell } from './ModalShell';
import { MoneyValue } from './MoneyValue';
import type { ContaDisponivel, LancamentoRow } from './types';

interface ModalDarBaixaProps {
  open: boolean;
  row: LancamentoRow | null;
  contas: ContaDisponivel[];
  onClose: () => void;
  onConfirmar: (rowId: string, conta: string) => void;
}

/** Modal "Dar baixa" — o usuário escolhe a conta por onde o dinheiro entrou/saiu e confirma. */
export function ModalDarBaixa({ open, row, contas, onClose, onConfirmar }: ModalDarBaixaProps) {
  const [contaSelecionada, setContaSelecionada] = useState<string | null>(null);

  useEffect(() => {
    setContaSelecionada(null);
  }, [row?.id]);

  if (!row) return null;

  const contexto = row.status === 'atrasado' ? `atrasado há ${row.diasAtraso} dias.` : `vence em ${formatDateShort(row.data)}.`;
  const verbo = row.tipo === 'entrada' ? 'entrou' : 'saiu';

  return (
    <ModalShell
      open={open}
      onClose={onClose}
      eyebrow="Dar baixa"
      title={row.desc}
      description={
        <>
          <MoneyValue centavos={row.valorCentavos} /> · {contexto} Confirme por qual conta isso {verbo}.
        </>
      }
      footer={
        <>
          <Button variant="secondary" size="sm" onClick={onClose}>
            Cancelar
          </Button>
          <Button
            variant="primary"
            size="sm"
            disabled={!contaSelecionada}
            onClick={() => contaSelecionada && onConfirmar(row.id, contaSelecionada)}
          >
            Confirmar baixa
          </Button>
        </>
      }
    >
      <div className="flex flex-col gap-2">
        {contas.map((conta) => {
          const selecionada = conta.nome === contaSelecionada;
          return (
            <button
              key={conta.nome}
              type="button"
              onClick={() => setContaSelecionada(conta.nome)}
              className={cn(
                'flex w-full items-center justify-between rounded-xl border-[1.5px] px-3.5 py-2.5 text-left text-[13.5px] font-semibold transition-colors active:brightness-95',
                selecionada
                  ? 'border-primary-600 bg-primary-soft text-primary-600'
                  : 'border-border bg-surface-2 text-foreground hover:border-primary-600/40',
              )}
            >
              {conta.nome}
              <span className={cn('text-xs font-semibold', selecionada ? 'text-primary-600' : 'text-muted-foreground')}>{conta.tag}</span>
            </button>
          );
        })}
      </div>
    </ModalShell>
  );
}
