import { AnimatePresence, motion } from 'framer-motion';
import { Info } from 'lucide-react';
import { useId, useState } from 'react';

import { cn } from '@/lib/utils';

interface InfoTooltipProps {
  children: string;
  className?: string;
}

/**
 * `(ⓘ)` — todo termo técnico ou número tem uma explicação em 1 frase a um
 * toque de distância (princípio de design #2/#6 do financeiro-ux.md).
 */
export function InfoTooltip({ children, className }: InfoTooltipProps) {
  const [open, setOpen] = useState(false);
  const id = useId();

  return (
    <span className="relative inline-flex">
      <button
        type="button"
        aria-describedby={id}
        onClick={() => setOpen((v) => !v)}
        onMouseEnter={() => setOpen(true)}
        onMouseLeave={() => setOpen(false)}
        onBlur={() => setOpen(false)}
        className={cn(
          'inline-flex h-4 w-4 items-center justify-center rounded-full text-muted-foreground/70 hover:text-primary-600 transition-colors',
          className,
        )}
      >
        <Info className="h-3.5 w-3.5" />
      </button>
      <AnimatePresence>
        {open && (
          <motion.span
            id={id}
            role="tooltip"
            initial={{ opacity: 0, y: 4, scale: 0.97 }}
            animate={{ opacity: 1, y: 0, scale: 1 }}
            exit={{ opacity: 0, y: 4, scale: 0.97 }}
            transition={{ duration: 0.15 }}
            className="absolute left-1/2 top-full z-50 mt-2 w-56 -translate-x-1/2 rounded-lg border border-border/60 bg-card p-2.5 text-xs leading-snug text-card-foreground shadow-lg"
          >
            {children}
          </motion.span>
        )}
      </AnimatePresence>
    </span>
  );
}
