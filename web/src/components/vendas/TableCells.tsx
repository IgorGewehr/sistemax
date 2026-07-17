import type { ReactNode } from 'react';

import { cn } from '@/lib/utils';

/** Células de tabela de `VendasTable` — mesmo recorte visual de `components/compras/TableCells.tsx`. */
export function Th({ children, align = 'left' }: { children: ReactNode; align?: 'left' | 'right' }) {
  return (
    <th
      className={cn(
        'whitespace-nowrap border-b border-border px-4 py-3 text-[11px] font-semibold uppercase tracking-wide text-muted-foreground',
        align === 'right' ? 'text-right' : 'text-left',
      )}
    >
      {children}
    </th>
  );
}

export function Td({
  children,
  align = 'left',
  mono = false,
  className,
}: {
  children: ReactNode;
  align?: 'left' | 'right';
  mono?: boolean;
  className?: string;
}) {
  return (
    <td
      className={cn(
        'border-b border-border/60 px-4 py-3 text-[13.5px]',
        align === 'right' ? 'text-right' : 'text-left',
        mono && 'num',
        className,
      )}
    >
      {children}
    </td>
  );
}
