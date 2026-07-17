import { AnimatePresence, motion } from 'framer-motion';
import { X } from 'lucide-react';
import type { ReactNode } from 'react';

import { cn } from '@/lib/utils';

interface AgendaDialogShellProps {
  open: boolean;
  onClose: () => void;
  /** Rótulo acessível do dialog (`aria-label`) — não precisa aparecer visualmente. */
  ariaLabel: string;
  /** Barra fina colorida no topo (ex.: cor do status do agendamento) — opcional. */
  accentClassName?: string;
  /** Conteúdo livre do topo (avatar+nome+chip, ou um título simples) — o caller decide. */
  header?: ReactNode;
  children: ReactNode;
  /** Footer livre (grupos de botões, divisores) — o caller monta como precisar. */
  footer?: ReactNode;
  maxWidthClassName?: string;
}

/**
 * Shell de modal local — substitui `@mui/material/Dialog` (ausente do SistemaX). Replica a
 * mesma linguagem visual de `components/ui/Modal.tsx` (overlay, motion sem `blur` no exit,
 * `rounded-2xl`, borda), mas com `header`/`footer` livres: `Modal` genérico só aceita título+X+
 * `children` soltos, sem footer dedicado nem header colorido — o que `AppointmentViewDialog`
 * (barra de status + footer em 2 grupos) e `AppointmentFormDialog` (footer com "Excluir" à
 * esquerda, "Cancelar/Salvar" à direita) precisam. Como a instrução do módulo proíbe editar
 * `components/ui/*`, a solução é este shell local — mesmo raciocínio que motivou a extração de
 * `AppointmentFormDialog.tsx` pra `shared.ts` no saas-erp.
 */
export function AgendaDialogShell({
  open,
  onClose,
  ariaLabel,
  accentClassName,
  header,
  children,
  footer,
  maxWidthClassName = 'max-w-md',
}: AgendaDialogShellProps) {
  return (
    <AnimatePresence>
      {open && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
          <motion.div
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            transition={{ duration: 0.15 }}
            className="absolute inset-0 bg-black/40"
            onClick={onClose}
          />
          <motion.div
            initial={{ opacity: 0, y: 12, scale: 0.98 }}
            animate={{ opacity: 1, y: 0, scale: 1 }}
            exit={{ opacity: 0, y: 8, scale: 0.98 }}
            transition={{ duration: 0.2, ease: [0, 0, 0.2, 1] }}
            className={cn(
              'relative z-10 flex max-h-[90vh] w-full flex-col overflow-hidden rounded-2xl border border-border/60 bg-card shadow-xl',
              maxWidthClassName,
            )}
            role="dialog"
            aria-modal="true"
            aria-label={ariaLabel}
          >
            {accentClassName && <div className={cn('h-1.5 flex-none', accentClassName)} />}

            <button
              type="button"
              onClick={onClose}
              aria-label="Fechar"
              className="absolute right-4 top-4 z-10 flex h-8 w-8 items-center justify-center rounded-lg text-muted-foreground transition-colors hover:bg-secondary hover:text-foreground"
            >
              <X className="h-4 w-4" />
            </button>

            <div className="flex-1 overflow-y-auto px-5 pb-2 pt-5">
              {header && <div className="mb-4 pr-8">{header}</div>}
              {children}
            </div>

            {footer && <div className="flex-none border-t border-border/60 px-5 py-3">{footer}</div>}
          </motion.div>
        </div>
      )}
    </AnimatePresence>
  );
}
