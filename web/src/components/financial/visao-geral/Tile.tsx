import type { ReactNode } from 'react';

import { cn } from '@/lib/utils';

interface TileProps {
  onClick: () => void;
  ariaLabel?: string;
  className?: string;
  children: ReactNode;
}

/**
 * Casca comum das tiles/mini-cards clicáveis das filas ② e ③ (`.tile`/`.mix`/`.mini` do mockup) —
 * seta "→" que aparece no hover, elevação leve, borda em destaque. Cada bloco (A receber, A pagar,
 * Resultado, Assinaturas, mix, Investimento, Simples) só precisa do próprio conteúdo.
 */
export function Tile({ onClick, ariaLabel, className, children }: TileProps) {
  return (
    <button
      type="button"
      onClick={onClick}
      aria-label={ariaLabel}
      className={cn(
        'surface group relative flex flex-col gap-2.5 rounded-xl p-4 pb-3.5 text-left transition-all',
        'hover:-translate-y-0.5 hover:border-primary-600/45 hover:shadow-lg',
        'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
        className,
      )}
    >
      <span
        aria-hidden="true"
        className="pointer-events-none absolute right-3.5 top-3.5 text-[13px] font-bold text-primary-600 opacity-0 transition-opacity group-hover:opacity-100"
      >
        →
      </span>
      {children}
    </button>
  );
}
