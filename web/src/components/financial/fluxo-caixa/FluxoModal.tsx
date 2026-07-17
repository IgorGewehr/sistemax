import { AnimatePresence, motion } from 'framer-motion';
import { X } from 'lucide-react';
import type { ReactNode } from 'react';

import { Eyebrow } from '@/components/shared';
import { cn } from '@/lib/utils';

interface FluxoModalProps {
  open: boolean;
  onClose: () => void;
  /** Rótulo pequeno acima do título (ex.: "Fechamento cego") — vem ANTES do título, como no
   * `.m-head` do mockup (`<span class="eyebrow">` seguido de `<h3>`). */
  eyebrow: string;
  title: string;
  children: ReactNode;
  className?: string;
}

/**
 * Modal local a esta tela — réplica do `Modal` compartilhado (`components/ui/Modal`), mas com o
 * `eyebrow` renderizado ANTES do título. O `Modal` compartilhado põe o título no cabeçalho e
 * qualquer `Eyebrow` só apareceria dentro de `children` (depois do título) — ordem invertida em
 * relação ao mockup. Reimplementado aqui em vez de alterar o `Modal` compartilhado pra não mudar o
 * comportamento de outras telas (ex.: Estoque) que também o usam.
 */
export function FluxoModal({ open, onClose, eyebrow, title, children, className }: FluxoModalProps) {
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
            className={cn('relative z-10 w-full max-w-md rounded-2xl border border-border/60 bg-card p-5 shadow-xl', className)}
            role="dialog"
            aria-modal="true"
            aria-label={title}
          >
            <Eyebrow className="mb-1">{eyebrow}</Eyebrow>
            <div className="mb-4 flex items-center justify-between">
              <h2 className="font-display text-base font-bold text-foreground">{title}</h2>
              <button
                type="button"
                onClick={onClose}
                aria-label="Fechar"
                className="flex h-8 w-8 items-center justify-center rounded-lg text-muted-foreground hover:bg-secondary hover:text-foreground"
              >
                <X className="h-4 w-4" />
              </button>
            </div>
            {children}
          </motion.div>
        </div>
      )}
    </AnimatePresence>
  );
}
