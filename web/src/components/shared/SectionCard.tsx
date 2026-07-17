import type { ReactNode } from 'react';

import { Surface } from '@/components/ui/Surface';
import { cn } from '@/lib/utils';

interface SectionCardProps {
  title: ReactNode;
  /** Dica cinza à direita do título (`.hint` do mockup). */
  hint?: ReactNode;
  /** Ações à direita no header da seção. */
  actions?: ReactNode;
  children: ReactNode;
  className?: string;
  /** Classe do corpo (padrão sem padding — a seção controla o próprio interior). */
  bodyClassName?: string;
}

/**
 * Card de seção com header padronizado (`h2.sec` do mockup): título + hint/ações. O corpo é livre
 * (tabela, gráfico, lista). Base de quase toda seção grande das telas do Financeiro.
 */
export function SectionCard({ title, hint, actions, children, className, bodyClassName }: SectionCardProps) {
  return (
    <Surface padding="none" className={cn('overflow-hidden', className)}>
      <div className="flex flex-wrap items-center justify-between gap-2.5 px-[18px] pt-[15px]">
        <h2 className="flex items-center gap-2.5 text-[13px] font-bold tracking-tight text-foreground">
          {title}
          {hint && <span className="text-xs font-medium text-muted-foreground">{hint}</span>}
        </h2>
        {actions && <div className="flex items-center gap-2">{actions}</div>}
      </div>
      <div className={cn('mt-3', bodyClassName)}>{children}</div>
    </Surface>
  );
}
