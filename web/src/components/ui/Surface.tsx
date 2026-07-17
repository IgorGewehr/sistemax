import type { HTMLAttributes, ReactNode } from 'react';

import { cn } from '@/lib/utils';

interface SurfaceProps extends HTMLAttributes<HTMLDivElement> {
  children: ReactNode;
  hoverable?: boolean;
  padding?: 'none' | 'sm' | 'md' | 'lg';
  rounded?: 'xl' | '2xl';
}

const PADDING_MAP = {
  none: '',
  sm: 'p-4',
  md: 'p-5',
  lg: 'p-6',
};

/** Card base do design system — sombra suave, borda quase invisível, dark mode. */
export function Surface({ children, className, hoverable, padding = 'md', rounded = 'xl', ...rest }: SurfaceProps) {
  return (
    <div
      className={cn(
        'surface',
        rounded === '2xl' ? 'rounded-2xl' : 'rounded-xl',
        PADDING_MAP[padding],
        hoverable && 'surface-hover',
        className,
      )}
      {...rest}
    >
      {children}
    </div>
  );
}
