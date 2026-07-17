import { AnimatePresence, motion } from 'framer-motion';
import { X } from 'lucide-react';
import { useEffect, type ReactNode } from 'react';

import { Eyebrow } from '@/components/shared';

interface ModalShellProps {
  open: boolean;
  onClose: () => void;
  eyebrow: string;
  title: string;
  description?: ReactNode;
  children: ReactNode;
  footer: ReactNode;
}

/**
 * Chrome dos 3 modais desta tela (Dar baixa / Lançamento rápido / Detalhe) — cabeçalho
 * eyebrow + título + descrição, corpo com scroll próprio, rodapé de ações. Fica local à tela
 * porque o `Modal` compartilhado (`@/components/ui/Modal`) só tem um título de uma linha; o
 * `.m-head` do mockup pede eyebrow + h3 + parágrafo, então construímos o chrome aqui em vez de
 * forçar esse formato dentro do componente genérico. Escape fecha (mesmo listener do mockup).
 */
export function ModalShell({ open, onClose, eyebrow, title, description, children, footer }: ModalShellProps) {
  useEffect(() => {
    if (!open) return;
    function onKeyDown(e: KeyboardEvent) {
      if (e.key === 'Escape') onClose();
    }
    document.addEventListener('keydown', onKeyDown);
    return () => document.removeEventListener('keydown', onKeyDown);
  }, [open, onClose]);

  return (
    <AnimatePresence>
      {open && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4" role="presentation">
          <motion.div
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            transition={{ duration: 0.15 }}
            className="absolute inset-0 bg-black/40 backdrop-blur-[2px]"
            onClick={onClose}
          />
          <motion.div
            initial={{ opacity: 0, y: 8, scale: 0.98 }}
            animate={{ opacity: 1, y: 0, scale: 1 }}
            exit={{ opacity: 0, y: 8, scale: 0.98 }}
            transition={{ duration: 0.18, ease: [0, 0, 0.2, 1] }}
            className="relative z-10 w-full max-w-[440px] rounded-2xl border border-border bg-card shadow-2xl"
            role="dialog"
            aria-modal="true"
            aria-label={title}
          >
            <button
              type="button"
              onClick={onClose}
              aria-label="Fechar"
              className="absolute right-3.5 top-3.5 flex h-8 w-8 items-center justify-center rounded-lg text-muted-foreground transition-colors hover:bg-surface-2 hover:text-foreground active:brightness-95"
            >
              <X className="h-4 w-4" />
            </button>
            <div className="px-5 pb-1 pt-[18px]">
              <Eyebrow className="mb-1">{eyebrow}</Eyebrow>
              <h3 className="pr-8 text-[17px] font-bold tracking-tight text-foreground">{title}</h3>
              {description && <p className="mt-1.5 text-[13px] leading-relaxed text-muted-foreground">{description}</p>}
            </div>
            <div className="flex max-h-[60vh] flex-col gap-3.5 overflow-y-auto px-5 py-4">{children}</div>
            <div className="flex justify-end gap-2.5 px-5 pb-5 pt-1">{footer}</div>
          </motion.div>
        </div>
      )}
    </AnimatePresence>
  );
}
