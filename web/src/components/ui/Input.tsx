import { forwardRef, type InputHTMLAttributes } from 'react';

import { cn } from '@/lib/utils';

/**
 * Input base do design system. Extraído porque a MESMA classe estava copiada literal em 6 modais
 * (auditoria arquitetural 2026-07): abstração faltando, não duplicação sutil. Passe `className`
 * pra estender/sobrescrever pontualmente; todo o resto é `...rest` (value, onChange, type, etc.).
 */
/** Classe base do input — FONTE ÚNICA (antes copiada em 6 modais). Importe onde usar `<input>` cru. */
export const INPUT_CLASS =
  'w-full rounded-xl border border-border bg-surface-2 px-3 py-2.5 text-sm text-foreground outline-none focus:ring-2 focus:ring-ring';

export const Input = forwardRef<HTMLInputElement, InputHTMLAttributes<HTMLInputElement>>(
  function Input({ className, ...rest }, ref) {
    return <input ref={ref} className={cn(INPUT_CLASS, className)} {...rest} />;
  },
);
