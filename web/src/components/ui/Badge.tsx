import type { HTMLAttributes, ReactNode } from 'react';

import { cn } from '@/lib/utils';

type Tone = 'neutral' | 'critico' | 'atencao' | 'info' | 'good' | 'primary';

interface BadgeProps extends HTMLAttributes<HTMLSpanElement> {
  children: ReactNode;
  tone?: Tone;
}

const TONE_CLASSES: Record<Tone, string> = {
  neutral: 'bg-secondary text-secondary-foreground',
  critico: 'bg-red-50 text-red-700 dark:bg-red-500/10 dark:text-red-400',
  atencao: 'bg-amber-50 text-amber-700 dark:bg-amber-500/10 dark:text-amber-400',
  info: 'bg-blue-50 text-blue-700 dark:bg-blue-500/10 dark:text-blue-400',
  good: 'bg-emerald-50 text-emerald-700 dark:bg-emerald-500/10 dark:text-emerald-400',
  primary: 'bg-primary-50 text-primary-700 dark:bg-primary-500/15 dark:text-primary-300',
};

export function Badge({ children, tone = 'neutral', className, ...rest }: BadgeProps) {
  return (
    <span
      className={cn(
        'inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-2xs font-semibold uppercase tracking-wide',
        TONE_CLASSES[tone],
        className,
      )}
      {...rest}
    >
      {children}
    </span>
  );
}
